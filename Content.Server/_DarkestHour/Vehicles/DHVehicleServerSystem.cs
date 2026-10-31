using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.Atmos;
using Content.Shared.Buckle;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Gravity;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Vehicles;

/// <summary>
/// Серверная часть техники: отсек (своя карта с гридом), орудия, вход и выход через люк, наезд, дымовая завеса,
/// уничтожение с выбросом экипажа.
/// </summary>
public sealed partial class DHVehicleServerSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private Robust.Shared.Physics.Systems.SharedPhysicsSystem _physics = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const string WeaponContainerPrefix = "dh_vehicle_weapon_";
    private const string RunoverFixture = "runover";
    private const string SmokeCharge = "DHVehicleSmokeCharge";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Heat = "Heat";

    /// <summary>Кого машина недавно сбила — чтобы не бить каждый тик.</summary>
    private readonly Dictionary<EntityUid, TimeSpan> _runoverCooldown = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHVehicleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DHVehicleComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<DHVehicleComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<DHVehicleComponent, DHVehicleEnterDoAfterEvent>(OnEnterDoAfter);
        SubscribeLocalEvent<DHVehicleComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<DHVehicleComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<DHVehicleComponent, DHVehicleSmokeActionEvent>(OnSmoke);
        SubscribeLocalEvent<DHVehicleHatchComponent, InteractHandEvent>(OnHatchInteract);
    }

    #region Появление: отсек и орудия

    private void OnMapInit(Entity<DHVehicleComponent> ent, ref MapInitEvent args)
    {
        foreach (var (slot, proto) in ent.Comp.Weapons)
        {
            if (!_proto.HasIndex(proto))
            {
                Log.Error($"Vehicle {ToPrettyString(ent)}: unknown weapon prototype {proto}, skipped");
                continue;
            }

            var container = _container.EnsureContainer<ContainerSlot>(ent, WeaponContainerPrefix + slot);
            var weapon = Spawn(proto, Transform(ent).Coordinates);
            var gun = EnsureComp<DHVehicleGunComponent>(weapon);
            gun.Vehicle = ent;
            gun.Slot = slot;
            Dirty(weapon, gun);
            _container.Insert(weapon, container);
            ent.Comp.WeaponEntities[slot] = weapon;
        }

        // Корпус: неподвижное тело-«щит» поверх машины. Люди и машина — оба KinematicController, а такие тела
        // движок между собой не сталкивает; о статичный корпус люди упираются.
        if (ent.Comp.Hull is { } hullProto && _proto.HasIndex(hullProto))
            ent.Comp.HullEntity = Spawn(hullProto, new EntityCoordinates(ent, Vector2.Zero));

        CreateInterior(ent);
    }

    private void CreateInterior(Entity<DHVehicleComponent> ent)
    {
        if (!_proto.TryIndex(ent.Comp.Interior, out var layout))
        {
            Log.Error($"{ToPrettyString(ent)}: no vehicle interior {ent.Comp.Interior}");
            return;
        }

        var mapUid = _map.CreateMap(out var mapId);

        var gravity = EnsureComp<GravityComponent>(mapUid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(mapUid, gravity);

        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        moles[(int) Gas.Oxygen] = Atmospherics.OxygenMolesStandard;
        moles[(int) Gas.Nitrogen] = Atmospherics.NitrogenMolesStandard;
        _atmos.SetMapAtmosphere(mapUid, false, new GasMixture(moles, Atmospherics.T20C));

        var light = EnsureComp<MapLightComponent>(mapUid);
        light.AmbientLightColor = layout.Light;
        Dirty(mapUid, light);

        var grid = _map.CreateGridEntity(mapId);
        var floor = new Tile(((ContentTileDefinition) _tileDef[layout.Floor]).TileId);
        var tiles = new List<(Vector2i, Tile)>();
        for (var x = 0; x < layout.Size.X; x++)
        {
            for (var y = 0; y < layout.Size.Y; y++)
            {
                tiles.Add((new Vector2i(x, y), floor));
            }
        }

        _map.SetTiles(grid.Owner, grid.Comp, tiles);

        // Стены по краю — кроме клеток, где стоит что-то из планировки (люк в стене).
        var occupied = new HashSet<Vector2i>();
        foreach (var entry in layout.Entities)
        {
            if (!entry.OnWall)
                occupied.Add(entry.Pos);
        }

        for (var x = 0; x < layout.Size.X; x++)
        {
            for (var y = 0; y < layout.Size.Y; y++)
            {
                if ((x == 0 || y == 0 || x == layout.Size.X - 1 || y == layout.Size.Y - 1) &&
                    !occupied.Contains(new Vector2i(x, y)))
                {
                    Spawn(layout.Wall, new EntityCoordinates(grid.Owner, x + 0.5f, y + 0.5f));
                }
            }
        }

        foreach (var entry in layout.Entities)
        {
            // Битый прототип не должен ронять запуск раунда — пропускаем с ошибкой в логе.
            if (!_proto.HasIndex(entry.Proto))
            {
                Log.Error($"Vehicle interior {layout.ID}: unknown prototype {entry.Proto}, skipped");
                continue;
            }

            var uid = Spawn(entry.Proto, new EntityCoordinates(grid.Owner, entry.Pos.X + 0.5f, entry.Pos.Y + 0.5f));
            if (entry.Rot != 0f)
                _transform.SetLocalRotation(uid, Angle.FromDegrees(entry.Rot));

            if (TryComp<DHVehicleSeatComponent>(uid, out var seat))
            {
                seat.Vehicle = ent;
                Dirty(uid, seat);
            }

            if (TryComp<DHVehicleHatchComponent>(uid, out var hatch))
            {
                hatch.Vehicle = ent;
                Dirty(uid, hatch);
            }
        }

        var interior = EnsureComp<DHVehicleInteriorComponent>(grid.Owner);
        interior.Vehicle = ent;

        ent.Comp.InteriorMap = mapUid;
        ent.Comp.InteriorGrid = grid.Owner;
        ent.Comp.InteriorEntry = new Vector2(layout.Entry.X + 0.5f, layout.Entry.Y + 0.5f);
    }

    private void OnShutdown(Entity<DHVehicleComponent> ent, ref ComponentShutdown args)
    {
        EjectAll(ent, false);
        if (ent.Comp.InteriorMap is { } map && !TerminatingOrDeleted(map))
            QueueDel(map);

        if (ent.Comp.HullEntity is { } hull && !TerminatingOrDeleted(hull))
            QueueDel(hull);

        ent.Comp.InteriorMap = null;
        ent.Comp.InteriorGrid = null;
        ent.Comp.HullEntity = null;
    }

    #endregion

    #region Вход и выход

    private void OnInteractHand(Entity<DHVehicleComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || ent.Comp.InteriorGrid == null)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("dh-vehicle-entering", ("vehicle", ent.Owner)), ent, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.EnterTime,
            new DHVehicleEnterDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 3f,
        });
    }

    private void OnEnterDoAfter(Entity<DHVehicleComponent> ent, ref DHVehicleEnterDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.InteriorGrid is not { } grid)
            return;

        args.Handled = true;
        MoveWithPulled(args.User, new EntityCoordinates(grid, ent.Comp.InteriorEntry));
    }

    private void OnHatchInteract(Entity<DHVehicleHatchComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || ent.Comp.Vehicle is not { } vehicle || !TryComp<DHVehicleComponent>(vehicle, out var comp))
            return;

        args.Handled = true;
        MoveWithPulled(args.User, ExitCoordinates((vehicle, comp)));
    }

    /// <summary>Снаружи у кормы машины.</summary>
    private EntityCoordinates ExitCoordinates(Entity<DHVehicleComponent> vehicle)
    {
        var xform = Transform(vehicle);
        var rotation = _transform.GetWorldRotation(xform);
        var position = _transform.GetWorldPosition(xform) + rotation.RotateVec(vehicle.Comp.ExitOffset);
        var mapUid = xform.MapUid ?? EntityUid.Invalid;
        return new EntityCoordinates(mapUid, position);
    }

    /// <summary>Переместить вместе с тем, кого тащит (раненого — в машину).</summary>
    private void MoveWithPulled(EntityUid user, EntityCoordinates target)
    {
        EntityUid? pulled = null;
        if (TryComp<PullerComponent>(user, out var puller) && puller.Pulling is { } pulling &&
            TryComp<PullableComponent>(pulling, out var pullable))
        {
            pulled = pulling;
            _pulling.TryStopPull(pulling, pullable, user);
        }

        Teleport(user, target);
        if (pulled is { } other)
            Teleport(other, target);
    }

    private void Teleport(EntityUid uid, EntityCoordinates target)
    {
        _buckle.Unbuckle(uid, null);
        _transform.SetCoordinates(uid, target);
        _transform.AttachToGridOrMap(uid);
        if (TryComp<PhysicsComponent>(uid, out var physics))
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
    }

    #endregion

    #region Бой

    private void OnStartCollide(Entity<DHVehicleComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != RunoverFixture || !HasComp<MobStateComponent>(args.OtherEntity))
            return;

        if (!TryComp<PhysicsComponent>(ent, out var physics) || physics.LinearVelocity.Length() < ent.Comp.RunoverSpeed)
            return;

        var now = _timing.CurTime;
        if (_runoverCooldown.TryGetValue(args.OtherEntity, out var next) && now < next)
            return;

        _runoverCooldown[args.OtherEntity] = now + TimeSpan.FromSeconds(1.5);
        _damageable.TryChangeDamage(args.OtherEntity,
            new DamageSpecifier(_proto.Index(Blunt), ent.Comp.RunoverDamage), origin: ent);
        _stun.TryKnockdown(args.OtherEntity, TimeSpan.FromSeconds(2), true);
        _popup.PopupEntity(Loc.GetString("dh-vehicle-runover", ("vehicle", ent.Owner)), args.OtherEntity,
            args.OtherEntity, PopupType.MediumCaution);
    }

    private void OnSmoke(Entity<DHVehicleComponent> ent, ref DHVehicleSmokeActionEvent args)
    {
        if (args.Handled || !ent.Comp.Smoke)
            return;

        args.Handled = true;
        var center = _transform.GetMapCoordinates(ent);
        for (var i = 0; i < 6; i++)
        {
            var offset = Angle.FromDegrees(60 * i).ToWorldVec() * 3f;
            Spawn(SmokeCharge, new MapCoordinates(center.Position + offset, center.MapId));
        }
    }

    /// <summary>Машина уничтожена: экипаж и десант выбрасывает наружу, на месте — остов.</summary>
    private void OnDestruction(Entity<DHVehicleComponent> ent, ref DestructionEventArgs args)
    {
        EjectAll(ent, true);
        if (ent.Comp.Wreck is { } wreck)
        {
            var xform = Transform(ent);
            var spawned = Spawn(wreck, xform.Coordinates);
            _transform.SetLocalRotation(spawned, xform.LocalRotation);
        }
    }

    private void EjectAll(Entity<DHVehicleComponent> ent, bool hurt)
    {
        if (ent.Comp.InteriorGrid is not { } grid || TerminatingOrDeleted(grid) || TerminatingOrDeleted(ent))
            return;

        var xform = Transform(ent);
        if (xform.MapUid is not { } mapUid)
            return;

        var center = _transform.GetWorldPosition(xform);
        var inside = new List<EntityUid>();
        var children = Transform(grid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (HasComp<MobStateComponent>(child) || HasComp<ItemComponent>(child))
                inside.Add(child);
        }

        foreach (var uid in inside)
        {
            var offset = _random.NextAngle().ToWorldVec() * _random.NextFloat(1.5f, 3f);
            Teleport(uid, new EntityCoordinates(mapUid, center + offset));
            if (!hurt || !HasComp<MobStateComponent>(uid))
                continue;

            var damage = new DamageSpecifier(_proto.Index(Blunt), 30);
            damage.DamageDict[Heat] = 20;
            _damageable.TryChangeDamage(uid, damage, true);
            _stun.TryKnockdown(uid, TimeSpan.FromSeconds(3), true);
        }
    }

    #endregion
}
