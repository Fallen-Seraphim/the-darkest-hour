using System.Linq;
using System.Numerics;
using Content.Server._DarkestHour.Assault;
using Content.Server._DarkestHour.Atmos.Fire;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Atmos.Fire;
using Content.Shared._DarkestHour.Carrier;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Carrier;

/// <summary>
/// Живучесть авианосца (план осады п. 19, решения автора 2026-11-19):
/// <list type="bullet">
/// <item>попадание ПКО (и ПВО) по кораблю — пробоина в отсеке, куда попали, и часто пожар (зажигательные — всегда);
/// по кораблю — тревога с названием отсека;</item>
/// <item>пробоина заваривается сваркой (~10 с) с листом стали в руках; из неё уходит воздух;</item>
/// <item>пока в отсеке есть пробоина, его машины и консоли обесточены; ангар — челноки не вылетают, штурмовик не
/// взлетает, лифт стоит; энергетика — по кораблю гаснет свет (аварийный остаётся), утечки радиации;</item>
/// <item>каждая незалатанная пробоина съедает 0,5% прочности в минуту, каждый непотушенный пожар — 0,2%.</item>
/// </list>
/// </summary>
public sealed partial class DHCarrierDamageSystem : EntitySystem
{
    [Dependency] private DHAssaultRuleSystem _assault = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DHFireSystem _fire = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const string BreachProto = "DHCarrierBreach";
    private const string ReactorBreachProto = "DHCarrierBreachReactor";
    private static readonly ProtoId<ToolQualityPrototype> WeldingQuality = "Welding";
    private static readonly ProtoId<StackPrototype> SteelStack = "Steel";
    private static readonly ProtoId<DHFlammableReagentPrototype> ShipFireFuel = "WeldingFuel";
    private static readonly SoundSpecifier AlarmSound = new SoundPathSpecifier("/Audio/Machines/Nuke/angry_beep.ogg");

    /// <summary>Пробоина отнимает столько прочности в минуту, пожар (клетка огня) — столько.</summary>
    private const float BreachDrainPerMinute = 0.5f;
    private const float FireDrainPerMinute = 0.2f;

    /// <summary>Шанс пожара от обычного попадания (зажигательное — всегда) и утечки радиации в энергетике.</summary>
    private const float FireChance = 0.5f;
    private const float ReactorLeakChance = 0.35f;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextUpdate;

    /// <summary>Что обесточено из-за повреждённого отсека: (грид, отсек) → сущности, которым надо вернуть питание.</summary>
    private readonly Dictionary<(EntityUid Grid, string Compartment), List<EntityUid>> _disabled = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHCarrierBreachComponent, InteractUsingEvent>(OnBreachInteract);
        SubscribeLocalEvent<DHCarrierBreachComponent, DHCarrierBreachWeldDoAfterEvent>(OnBreachWelded);
        SubscribeLocalEvent<DHCarrierDamageConsoleComponent, BoundUIOpenedEvent>(OnConsoleOpened);
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ => _disabled.Clear());
    }

    #region Отсеки

    private DHCarrierLayoutPrototype? Layout(EntityUid grid)
    {
        return TryComp<DHCarrierCompartmentsComponent>(grid, out var comp) && _proto.TryIndex(comp.Layout, out var layout)
            ? layout
            : null;
    }

    /// <summary>Отсек, в котором точка грида (локальные координаты).</summary>
    private static DHCarrierCompartment? CompartmentAt(DHCarrierLayoutPrototype layout, Vector2 local)
    {
        foreach (var compartment in layout.Compartments)
        {
            foreach (var box in compartment.Boxes)
            {
                if (box.Contains(local))
                    return compartment;
            }
        }

        return null;
    }

    private static bool Inside(DHCarrierCompartment compartment, Vector2 local)
    {
        return compartment.Boxes.Any(b => b.Contains(local));
    }

    /// <summary>Отсек такого вида на корабле сейчас повреждён (есть незалатанная пробоина).</summary>
    public bool Damaged(EntityUid? grid, DHCarrierCompartmentKind kind)
    {
        if (grid == null || Layout(grid.Value) is not { } layout)
            return false;

        foreach (var compartment in layout.Compartments)
        {
            if (compartment.Kind == kind && _disabled.ContainsKey((grid.Value, compartment.Id)))
                return true;
        }

        return false;
    }

    #endregion

    #region Попадание

    /// <summary>Попадание по кораблю в мировой точке: пробоина в отсеке, пожар, тревога.</summary>
    public void Hit(EntityUid grid, MapCoordinates point, bool incendiary)
    {
        if (Layout(grid) is not { } layout || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var local = Vector2.Transform(point.Position, _transform.GetInvWorldMatrix(grid));
        var compartment = CompartmentAt(layout, local);
        if (compartment == null)
            return;

        var tile = _map.TileIndicesFor(grid, gridComp, point);
        var spot = FreeTile(grid, gridComp, tile) ?? tile;
        var proto = compartment.Kind == DHCarrierCompartmentKind.Engineering && _random.Prob(ReactorLeakChance)
            ? ReactorBreachProto
            : BreachProto;
        var breach = Spawn(proto, _map.GridTileToLocal(grid, gridComp, spot));
        var breachComp = EnsureComp<DHCarrierBreachComponent>(breach);
        breachComp.Grid = grid;
        breachComp.Compartment = compartment.Id;
        _audio.PlayPvs(AlarmSound, breach, AudioParams.Default.WithMaxDistance(20f));

        var fire = incendiary || _random.Prob(FireChance);
        if (fire && _proto.TryIndex(ShipFireFuel, out var fuel))
        {
            foreach (var offset in new[] { Vector2i.Zero, Vector2i.Up, Vector2i.Down, Vector2i.Left, Vector2i.Right })
            {
                if (offset == Vector2i.Zero || _random.Prob(0.5f))
                    _fire.SpawnFire(grid, spot + offset, fuel, _random.NextFloat(60f, 120f));
            }
        }

        var name = Loc.GetString(compartment.Name);
        _assault.AnnounceToFaction(DHAssaultFaction.Empire, Loc.GetString("dh-carrier-damage-sender"),
            Loc.GetString(fire ? "dh-carrier-damage-hit-fire" : "dh-carrier-damage-hit", ("compartment", name)),
            AlarmSound);
        Refresh(grid, layout);
    }

    /// <summary>Клетка без стены рядом с местом попадания (пробоина — на полу).</summary>
    private Vector2i? FreeTile(EntityUid grid, MapGridComponent gridComp, Vector2i around)
    {
        for (var radius = 0; radius <= 2; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                        continue;

                    var tile = around + new Vector2i(dx, dy);
                    if (!_map.TryGetTileRef(grid, gridComp, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                        continue;

                    var blocked = false;
                    var anchored = _map.GetAnchoredEntitiesEnumerator(grid, gridComp, tile);
                    while (anchored.MoveNext(out var uid))
                    {
                        if (HasComp<Content.Server.Atmos.Components.AirtightComponent>(uid))
                        {
                            blocked = true;
                            break;
                        }
                    }

                    if (!blocked)
                        return tile;
                }
            }
        }

        return null;
    }

    #endregion

    #region Ремонт

    private void OnBreachInteract(Entity<DHCarrierBreachComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, WeldingQuality))
            return;

        args.Handled = true;
        if (SteelInHands(args.User) == null)
        {
            _popup.PopupEntity(Loc.GetString("dh-carrier-breach-need-steel"), ent, args.User);
            return;
        }

        _tool.UseTool(args.Used, args.User, ent, ent.Comp.WeldTime, new[] { WeldingQuality },
            new DHCarrierBreachWeldDoAfterEvent(), ent.Comp.WeldFuel);
    }

    private void OnBreachWelded(Entity<DHCarrierBreachComponent> ent, ref DHCarrierBreachWeldDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (SteelInHands(args.User) is not { } steel || !_stack.TryUse(new Entity<StackComponent?>(steel.Owner, steel.Comp), 1))
        {
            _popup.PopupEntity(Loc.GetString("dh-carrier-breach-need-steel"), ent, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("dh-carrier-breach-sealed"), ent, args.User);
        var grid = ent.Comp.Grid;
        QueueDel(ent);
        if (Layout(grid) is { } layout)
            Refresh(grid, layout, ent.Owner);
    }

    private Entity<StackComponent>? SteelInHands(EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (TryComp<StackComponent>(held, out var stack) && stack.StackTypeId == SteelStack && stack.Count > 0)
                return (held, stack);
        }

        return null;
    }

    #endregion

    #region Последствия

    /// <summary>Пересчитать повреждённые отсеки корабля и их последствия. <paramref name="removed"/> — уже удаляемая пробоина.</summary>
    private void Refresh(EntityUid grid, DHCarrierLayoutPrototype layout, EntityUid? removed = null)
    {
        var breached = new HashSet<string>();
        var query = EntityQueryEnumerator<DHCarrierBreachComponent>();
        while (query.MoveNext(out var uid, out var breach))
        {
            if (breach.Grid == grid && uid != removed && !TerminatingOrDeleted(uid))
                breached.Add(breach.Compartment);
        }

        foreach (var compartment in layout.Compartments)
        {
            var key = (grid, compartment.Id);
            var damaged = breached.Contains(compartment.Id);
            if (damaged == _disabled.ContainsKey(key))
                continue;

            if (damaged)
            {
                _disabled[key] = Disable(grid, compartment);
                continue;
            }

            if (_disabled.Remove(key, out var list))
            {
                foreach (var uid in list)
                {
                    if (!TerminatingOrDeleted(uid))
                        _power.SetPowerDisabled(uid, false);
                }

                _assault.AnnounceToFaction(DHAssaultFaction.Empire, Loc.GetString("dh-carrier-damage-sender"),
                    Loc.GetString("dh-carrier-damage-repaired", ("compartment", Loc.GetString(compartment.Name))));
            }
        }
    }

    /// <summary>
    /// Обесточить последствия отсека: машины и консоли в нём (кроме света); у энергетики — свет по всему кораблю
    /// (аварийные лампы со своими батареями горят).
    /// </summary>
    private List<EntityUid> Disable(EntityUid grid, DHCarrierCompartment compartment)
    {
        var result = new List<EntityUid>();
        if (compartment.Kind == DHCarrierCompartmentKind.None)
            return result;

        var query = EntityQueryEnumerator<Content.Server.Power.Components.ApcPowerReceiverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var receiver, out var xform))
        {
            if (xform.GridUid != grid || receiver.PowerDisabled)
                continue;

            var light = HasComp<PoweredLightComponent>(uid);
            bool affected;
            if (compartment.Kind == DHCarrierCompartmentKind.Engineering)
                affected = light;
            else
                affected = !light && xform.Anchored && Inside(compartment, xform.LocalPosition);

            if (!affected)
                continue;

            _power.SetPowerDisabled(uid, true, receiver);
            result.Add(uid);
        }

        return result;
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + Interval;
        var minutes = (float) Interval.TotalMinutes;

        // Пробоины: уходит воздух, капает урон.
        var drain = new Dictionary<EntityUid, float>();
        var breaches = EntityQueryEnumerator<DHCarrierBreachComponent>();
        while (breaches.MoveNext(out var uid, out var breach))
        {
            if (!Exists(breach.Grid))
                continue;

            drain[breach.Grid] = drain.GetValueOrDefault(breach.Grid) + BreachDrainPerMinute * minutes;
            if (breach.VentFraction > 0f && _atmos.GetTileMixture(uid, excite: true) is { } air)
                air.Multiply(1f - breach.VentFraction);
        }

        // Пожары на кораблях с отсеками.
        var fires = EntityQueryEnumerator<DHTileFireComponent, TransformComponent>();
        while (fires.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid is { } grid && HasComp<DHCarrierCompartmentsComponent>(grid))
                drain[grid] = drain.GetValueOrDefault(grid) + FireDrainPerMinute * minutes;
        }

        foreach (var (grid, amount) in drain)
        {
            _assault.DamageCarrier(grid, amount, announce: false);
        }

        UpdateConsoles();
    }

    #region Схема повреждений

    private void OnConsoleOpened(Entity<DHCarrierDamageConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateConsole(ent);
    }

    private void UpdateConsoles()
    {
        var query = EntityQueryEnumerator<DHCarrierDamageConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            UpdateConsole((uid, console));
        }
    }

    private void UpdateConsole(Entity<DHCarrierDamageConsoleComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, DHCarrierDamageUiKey.Key) || Transform(ent).GridUid is not { } grid ||
            Layout(grid) is not { } layout)
        {
            return;
        }

        var breaches = new Dictionary<string, int>();
        var query = EntityQueryEnumerator<DHCarrierBreachComponent>();
        while (query.MoveNext(out _, out var breach))
        {
            if (breach.Grid == grid)
                breaches[breach.Compartment] = breaches.GetValueOrDefault(breach.Compartment) + 1;
        }

        var fires = new Dictionary<string, int>();
        var fireQuery = EntityQueryEnumerator<DHTileFireComponent, TransformComponent>();
        while (fireQuery.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == grid && CompartmentAt(layout, xform.LocalPosition) is { } compartment)
                fires[compartment.Id] = fires.GetValueOrDefault(compartment.Id) + 1;
        }

        var rows = new List<DHCarrierCompartmentRow>();
        foreach (var compartment in layout.Compartments)
        {
            rows.Add(new DHCarrierCompartmentRow(compartment.Name, compartment.Kind,
                breaches.GetValueOrDefault(compartment.Id), fires.GetValueOrDefault(compartment.Id)));
        }

        var hull = 0f;
        var maxHull = 100f;
        if (TryComp<DHAssaultCarrierComponent>(grid, out var carrier))
        {
            hull = carrier.Hull;
            maxHull = carrier.MaxHull;
        }

        _ui.SetUiState(ent.Owner, DHCarrierDamageUiKey.Key, new DHCarrierDamageState(hull, maxHull, rows));
    }

    #endregion
}
