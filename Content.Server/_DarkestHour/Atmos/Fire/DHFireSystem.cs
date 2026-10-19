using Content.Server.Atmos.EntitySystems;
using Content.Shared._DarkestHour.Atmos.Fire;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Temperature;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Atmos.Fire;

/// <summary>
/// Огонь на тайлах: горит заданное время, поджигает и обжигает стоящих в нём, прожигает конструкции (термит),
/// поджигает горючие лужи (в том числе соседние) и атмосферу тайла. Гасится водой.
/// По образцу SharedRMCFlammableSystem из RMC-14 (MIT), без привязки к системам RMC.
/// </summary>
public sealed class DHFireSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly PointLightSystem _pointLight = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public static readonly EntProtoId DefaultFire = "DHTileFire";
    private static readonly ProtoId<DamageTypePrototype> HeatType = "Heat";
    private static readonly ProtoId<DamageTypePrototype> StructuralType = "Structural";
    private const string WaterReagent = "Water";

    /// <summary>Лужа меньше этого объёма не горит.</summary>
    private static readonly FixedPoint2 MinPuddleFuel = FixedPoint2.New(1);

    private static readonly AtmosDirection[] Cardinals =
        [AtmosDirection.North, AtmosDirection.South, AtmosDirection.East, AtmosDirection.West];

    private readonly HashSet<EntityUid> _onTile = new();
    private readonly List<(EntityUid Grid, Vector2i Tile)> _toIgnite = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHTileFireComponent, MapInitEvent>(OnFireMapInit);
        SubscribeLocalEvent<PuddleComponent, TileFireEvent>(OnPuddleTileFire);
        SubscribeLocalEvent<PuddleComponent, InteractUsingEvent>(OnPuddleInteractUsing);
    }

    private void OnFireMapInit(Entity<DHTileFireComponent> ent, ref MapInitEvent args)
    {
        var now = _timing.CurTime;
        ent.Comp.SpawnedAt = now;
        ent.Comp.NextTick = now;
        if (ent.Comp.EndTime < now)
            ent.Comp.EndTime = now + TimeSpan.FromSeconds(10);
    }

    private void OnPuddleTileFire(Entity<PuddleComponent> ent, ref TileFireEvent args)
    {
        TryIgnitePuddle(ent);
    }

    private void OnPuddleInteractUsing(Entity<PuddleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var hot = new IsHotEvent();
        RaiseLocalEvent(args.Used, hot);
        if (!hot.IsHot)
            return;

        if (TryIgnitePuddle(ent))
            args.Handled = true;
    }

    #region API

    /// <summary>
    /// Поджигает тайл. Если там уже горит — продлевает огонь и берёт более сильный реагент.
    /// </summary>
    public EntityUid? SpawnFire(EntityUid gridUid, Vector2i tile, DHFlammableReagentPrototype fuel, float durationSeconds)
    {
        if (!TryComp(gridUid, out MapGridComponent? grid) ||
            !_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) ||
            _turf.IsSpace(tileRef))
        {
            return null;
        }

        var end = _timing.CurTime + TimeSpan.FromSeconds(Math.Max(durationSeconds, 0.5f));
        if (TryGetFire(gridUid, grid, tile, out var existing))
        {
            var comp = existing.Value.Comp;
            if (end > comp.EndTime)
                comp.EndTime = end;

            if (fuel.Intensity >= comp.Intensity)
                Apply(existing.Value, fuel);

            return existing.Value;
        }

        var uid = Spawn(DefaultFire, _map.GridTileToLocal(gridUid, grid, tile));
        var fire = EnsureComp<DHTileFireComponent>(uid);
        fire.SpawnedAt = _timing.CurTime;
        fire.NextTick = fire.SpawnedAt;
        fire.EndTime = end;
        Apply((uid, fire), fuel);
        _appearance.SetData(uid, DHTileFireVisuals.Level, 4);
        return uid;
    }

    /// <summary>Гасит огонь на тайле на заданное число секунд. True, если огонь там был.</summary>
    public bool ExtinguishTile(EntityUid gridUid, Vector2i tile, float units)
    {
        if (!TryComp(gridUid, out MapGridComponent? grid) ||
            !TryGetFire(gridUid, grid, tile, out var fire))
        {
            return false;
        }

        fire.Value.Comp.EndTime -= TimeSpan.FromSeconds(units * fire.Value.Comp.ExtinguishPerUnit);
        if (fire.Value.Comp.EndTime <= _timing.CurTime)
            QueueDel(fire.Value);

        return true;
    }

    /// <summary>Поджигает горючие реагенты лужи. Сжигает их и превращает в огонь на тайле.</summary>
    public bool TryIgnitePuddle(Entity<PuddleComponent> puddle)
    {
        if (TerminatingOrDeleted(puddle) ||
            !_solution.TryGetSolution(puddle.Owner, puddle.Comp.SolutionName, out var solEnt, out var solution))
        {
            return false;
        }

        DHFlammableReagentPrototype? fuel = null;
        var best = FixedPoint2.Zero;
        var duration = 0f;
        var burned = new List<(string Reagent, FixedPoint2 Quantity)>();
        foreach (var (reagent, quantity) in solution.Contents)
        {
            if (!_proto.TryIndex<DHFlammableReagentPrototype>(reagent.Prototype, out var proto))
                continue;

            burned.Add((reagent.Prototype, quantity));
            duration += quantity.Float() * proto.PuddleDurationPerUnit;
            if (quantity <= best)
                continue;

            best = quantity;
            fuel = proto;
        }

        if (fuel == null || best < MinPuddleFuel)
            return false;

        var xform = Transform(puddle);
        if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return false;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        foreach (var (reagent, quantity) in burned)
        {
            _solution.RemoveReagent(solEnt.Value, reagent, quantity);
        }

        SpawnFire(gridUid, tile, fuel, Math.Clamp(duration, 3f, 90f));
        return true;
    }

    #endregion

    private void Apply(Entity<DHTileFireComponent> fire, DHFlammableReagentPrototype fuel)
    {
        fire.Comp.Reagent = fuel.ID;
        fire.Comp.Intensity = fuel.Intensity;
        fire.Comp.FireStacks = fuel.FireStacks;
        fire.Comp.StructuralDamage = fuel.StructuralDamage;
        fire.Comp.HotspotTemperature = fuel.HotspotTemperature;
        fire.Comp.Color = fuel.Color;
        Dirty(fire);

        _appearance.SetData(fire, DHTileFireVisuals.Color, fuel.Color);
        _pointLight.SetColor(fire, fuel.Color);
    }

    private bool TryGetFire(EntityUid gridUid, MapGridComponent grid, Vector2i tile,
        out Entity<DHTileFireComponent>? fire)
    {
        fire = null;
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (!TryComp(uid, out DHTileFireComponent? comp) || TerminatingOrDeleted(uid.Value))
                continue;

            fire = (uid.Value, comp);
            return true;
        }

        return false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _toIgnite.Clear();

        var query = EntityQueryEnumerator<DHTileFireComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var fire, out var xform))
        {
            if (now >= fire.EndTime || xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            {
                QueueDel(uid);
                continue;
            }

            UpdateLevel((uid, fire), now);

            if (now < fire.NextTick)
                continue;

            fire.NextTick = now + fire.TickTime;
            var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            BurnTile((uid, fire), gridUid, grid, tile);
        }

        // Поджигаем соседние лужи после обхода, чтобы не плодить огонь внутри перечисления.
        foreach (var (gridUid, tile) in _toIgnite)
        {
            IgnitePuddlesOn(gridUid, tile);
        }
    }

    private void UpdateLevel(Entity<DHTileFireComponent> fire, TimeSpan now)
    {
        var total = fire.Comp.EndTime - fire.Comp.SpawnedAt;
        var left = fire.Comp.EndTime - now;

        int level;
        if (now < fire.Comp.SpawnedAt + fire.Comp.BigFireTime)
            level = 4;
        else if (left < total * 0.33)
            level = 1;
        else if (left < total * 0.66)
            level = 2;
        else
            level = 3;

        _appearance.SetData(fire, DHTileFireVisuals.Level, level);
    }

    private void BurnTile(Entity<DHTileFireComponent> fire, EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var heat = new DamageSpecifier(_proto.Index(HeatType), FixedPoint2.New(fire.Comp.Intensity));
        DamageSpecifier? structural = fire.Comp.StructuralDamage > 0
            ? new DamageSpecifier(_proto.Index(StructuralType), FixedPoint2.New(fire.Comp.StructuralDamage))
            : null;

        _onTile.Clear();
        _lookup.GetLocalEntitiesIntersecting(gridUid, tile, _onTile, -0.1f,
            LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries | LookupFlags.Approximate, grid);

        foreach (var ent in _onTile)
        {
            if (ent == fire.Owner || TerminatingOrDeleted(ent))
                continue;

            // Горючая лужа под огнём — подливает топлива.
            if (TryComp(ent, out PuddleComponent? puddle))
            {
                if (Quench(fire, (ent, puddle)))
                    return;

                TryIgnitePuddle((ent, puddle));
                continue;
            }

            if (TryComp(ent, out FlammableComponent? flammable))
            {
                _flammable.AdjustFireStacks(ent, fire.Comp.FireStacks, flammable);
                _flammable.Ignite(ent, fire.Owner, flammable);
            }

            if (HasComp<MobStateComponent>(ent))
            {
                var protection = new GetFireProtectionEvent();
                RaiseLocalEvent(ent, ref protection);
                if (TryComp(ent, out InventoryComponent? inventory))
                    _inventory.RelayEvent((ent, inventory), ref protection);
                if (protection.Multiplier > 0f)
                    _damageable.TryChangeDamage(ent, heat * protection.Multiplier, interruptsDoAfters: false, origin: fire.Owner);
            }
            else if (structural != null && Transform(ent).Anchored && HasComp<DamageableComponent>(ent))
            {
                _damageable.TryChangeDamage(ent, structural, origin: fire.Owner);
            }
        }

        if (fire.Comp.HotspotTemperature > 0)
            _atmosphere.HotspotExpose(gridUid, tile, fire.Comp.HotspotTemperature, 50f, fire.Owner, true);

        // Огонь бежит по дорожке горючего.
        foreach (var dir in Cardinals)
        {
            _toIgnite.Add((gridUid, tile.Offset(dir)));
        }
    }

    /// <summary>Вода в луже под огнём тушит его и испаряется. True — огонь погас.</summary>
    private bool Quench(Entity<DHTileFireComponent> fire, Entity<PuddleComponent> puddle)
    {
        if (!_solution.TryGetSolution(puddle.Owner, puddle.Comp.SolutionName, out var solEnt, out var solution))
            return false;

        var water = solution.GetTotalPrototypeQuantity(WaterReagent);
        if (water <= FixedPoint2.Zero)
            return false;

        var needed = (fire.Comp.EndTime - _timing.CurTime).TotalSeconds / fire.Comp.ExtinguishPerUnit;
        var used = FixedPoint2.Min(water, FixedPoint2.New(Math.Max(needed, 0.1)));
        _solution.RemoveReagent(solEnt.Value, WaterReagent, used);

        fire.Comp.EndTime -= TimeSpan.FromSeconds(used.Float() * fire.Comp.ExtinguishPerUnit);
        if (fire.Comp.EndTime > _timing.CurTime)
            return false;

        QueueDel(fire);
        return true;
    }

    private void IgnitePuddlesOn(EntityUid gridUid, Vector2i tile)
    {
        if (!TryComp(gridUid, out MapGridComponent? grid))
            return;

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (TryComp(uid, out PuddleComponent? puddle))
                TryIgnitePuddle((uid.Value, puddle));
        }
    }
}
