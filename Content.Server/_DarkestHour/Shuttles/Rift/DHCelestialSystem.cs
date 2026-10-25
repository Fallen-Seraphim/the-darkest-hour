using System.Numerics;
using Content.Server.Radiation.Systems;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Popups;
using Content.Server.Radiation.Components;
using Content.Server.Shuttles.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Shuttles.Rift;

/// <summary>
/// Станция при создании привязывает свою карту к домашней звёздной системе.
/// </summary>
[RegisterComponent]
public sealed partial class DHHomeStarSystemComponent : Component
{
    [DataField(required: true)]
    public ProtoId<DHStarSystemPrototype> System;
}

/// <summary>
/// Небесные тела на картах звёздных систем: привязка домашней системы к карте станции, радиация звезды и газовых
/// гигантов, выталкивание кораблей из недр тел (садиться — только через посадочную консоль).
/// </summary>
public sealed partial class DHCelestialSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private RadiationSystem _radiation = default!;
    [Dependency] private ServerStationSystem _station = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    /// <summary>Скорость, с которой тело выталкивает корабль из своих недр, м/с.</summary>
    private const float RepelSpeed = 12f;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHHomeStarSystemComponent, StationPostInitEvent>(OnStationPostInit);
    }

    private void OnStationPostInit(Entity<DHHomeStarSystemComponent> ent, ref StationPostInitEvent args)
    {
        if (BindHomeMap(ent) == null)
            Log.Warning($"{ToPrettyString(ent)}: station has no grid yet, home star system map is bound later on demand.");
    }

    /// <summary>Привязывает карту станции к её домашней системе; возвращает карту.</summary>
    private EntityUid? BindHomeMap(Entity<DHHomeStarSystemComponent> ent)
    {
        var grid = _station.GetLargestGrid(ent.Owner);
        if (grid == null)
            return null;

        var mapUid = Transform(grid.Value).MapUid;
        if (mapUid == null)
            return null;

        var comp = EnsureComp<DHStarSystemMapComponent>(mapUid.Value);
        if (comp.System != ent.Comp.System)
        {
            comp.System = ent.Comp.System;
            comp.Origin = _transform.GetWorldPosition(grid.Value);
            Dirty(mapUid.Value, comp);
        }

        return mapUid;
    }

    /// <summary>
    /// Карта звёздной системы. Домашняя — карта станции; если она ещё не привязана (станция создана без грида,
    /// карта пересоздана и т. п.), привязывается сейчас.
    /// </summary>
    public EntityUid? GetSystemMap(string system)
    {
        var query = EntityQueryEnumerator<DHStarSystemMapComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.System?.Id == system)
                return uid;
        }

        var homes = EntityQueryEnumerator<DHHomeStarSystemComponent>();
        while (homes.MoveNext(out var station, out var home))
        {
            if (home.System.Id == system && BindHomeMap((station, home)) is { } map)
                return map;
        }

        return null;
    }

    /// <summary>Тела системы карты и начало отсчёта.</summary>
    public bool TryGetSystem(EntityUid? map, out DHStarSystemPrototype system, out Vector2 origin)
    {
        system = default!;
        origin = Vector2.Zero;

        if (map == null ||
            !TryComp<DHStarSystemMapComponent>(map, out var comp) ||
            comp.System == null ||
            !_proto.TryIndex(comp.System.Value, out var proto))
        {
            return false;
        }

        system = proto;
        origin = comp.Origin;
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;
        var seconds = (float) UpdateInterval.TotalSeconds;

        // Радиация: всем приёмникам на картах с телами.
        var receivers = EntityQueryEnumerator<RadiationReceiverComponent, TransformComponent>();
        while (receivers.MoveNext(out var uid, out _, out var xform))
        {
            if (!TryGetSystem(xform.MapUid, out var system, out var origin))
                continue;

            var pos = _transform.GetWorldPosition(xform) - origin;
            var rads = 0f;
            foreach (var body in system.Bodies)
            {
                rads += DHCelestial.RadiationAt(body, Vector2.Distance(pos, body.Position));
            }

            // Корпус корабля экранирует тех, кто на борту.
            rads *= HullFactor(xform);
            if (rads > 0.01f)
                _radiation.IrradiateEntity(uid, rads, seconds);
        }

        UpdateGasLayers();

        // Корабли не проходят сквозь тела: ниже допустимой глубины их выталкивает наружу.
        var grids = EntityQueryEnumerator<MapGridComponent, PhysicsComponent, TransformComponent>();
        while (grids.MoveNext(out var uid, out _, out var physics, out var xform))
        {
            if (physics.BodyType != BodyType.Dynamic ||
                !TryGetSystem(xform.MapUid, out var system, out var origin))
            {
                continue;
            }

            var center = _transform.GetWorldPosition(xform) + _transform.GetWorldRotation(xform).RotateVec(physics.LocalCenter);
            var pos = center - origin;

            foreach (var body in system.Bodies)
            {
                var offset = pos - body.Position;
                var distance = offset.Length();
                if (distance >= DHCelestial.SolidRadius(body))
                    continue;

                var normal = distance > 0.01f ? offset / distance : Vector2.UnitY;
                var velocity = physics.LinearVelocity;
                var outward = Vector2.Dot(velocity, normal);
                if (outward < RepelSpeed)
                    _physics.SetLinearVelocity(uid, velocity + normal * (RepelSpeed - outward), body: physics);

                WarnPilots(uid, body);
                break;
            }
        }
    }

    private void WarnPilots(EntityUid grid, DHCelestialBody body)
    {
        var consoles = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (consoles.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            _popup.PopupEntity(Loc.GetString("dh-celestial-repelled", ("body", Loc.GetString(body.Name))), uid, PopupType.LargeCaution);
        }
    }
}
