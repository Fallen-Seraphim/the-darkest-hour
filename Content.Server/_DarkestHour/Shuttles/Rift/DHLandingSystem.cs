using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Parallax;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Shuttles.Rift;

/// <summary>Корабль сел на тело: куда вернуть его при взлёте.</summary>
[RegisterComponent]
public sealed partial class DHLandedComponent : Component
{
    [DataField]
    public string System = string.Empty;

    [DataField]
    public string Body = string.Empty;

    /// <summary>Точка орбиты в координатах системы, откуда корабль начал посадку.</summary>
    [DataField]
    public Vector2 OrbitPosition;
}

/// <summary>
/// Консоль посадки (спуск на поверхность тела и взлёт на орбиту) и газозаборник (набор газа в верхних слоях
/// газовых гигантов).
/// </summary>
public sealed partial class DHLandingSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private DHCelestialSystem _celestial = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private NodeContainerSystem _nodes = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private DHRiftSystem _rift = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(2);

    /// <summary>Высота над поверхностью, на которую корабль выходит при взлёте без сохранённой орбиты, м.</summary>
    private const float AscentAltitude = 1500f;

    private const int MaxGrids = 32;

    private readonly Dictionary<(string System, string Body), EntityUid> _surfaces = new();
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<DHGasScoopComponent, AtmosDeviceUpdateEvent>(OnScoopUpdate);

        Subs.BuiEvents<DHLandingConsoleComponent>(DHLandingUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<DHLandingSelectBodyMessage>(OnSelectBody);
            subs.Event<DHLandingDescendMessage>(OnDescend);
            subs.Event<DHLandingAscendMessage>(OnAscend);
        });
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _surfaces.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;

        var query = EntityQueryEnumerator<DHLandingConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_ui.IsUiOpen(uid, DHLandingUiKey.Key))
                UpdateUi((uid, comp));
        }
    }

    #region Карты

    private EntityUid? SystemMap(string system)
    {
        return _celestial.GetSystemMap(system);
    }

    /// <summary>Карта поверхности тела; создаётся при первой посадке.</summary>
    private EntityUid? GetOrCreateSurface(DHStarSystemPrototype system, DHCelestialBody body)
    {
        if (_surfaces.TryGetValue((system.ID, body.Id), out var existing) && !TerminatingOrDeleted(existing))
            return existing;

        if (body.Biome == null || !_proto.TryIndex(body.Biome.Value, out var biome))
            return null;

        var mapUid = _map.CreateMap(out _);
        _meta.SetEntityName(mapUid, Loc.GetString(body.Name));
        _biome.EnsurePlanet(mapUid, biome, mapLight: body.SurfaceLight);

        if (body.SurfaceGases.Count > 0)
        {
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            foreach (var (gas, amount) in body.SurfaceGases)
            {
                moles[(int) gas] = amount;
            }

            _atmos.SetMapAtmosphere(mapUid, false, new GasMixture(moles, body.SurfaceTemperature));
        }
        else
        {
            _atmos.SetMapAtmosphere(mapUid, true, GasMixture.SpaceGas);
        }

        var surface = EnsureComp<DHPlanetSurfaceMapComponent>(mapUid);
        surface.System = system.ID;
        surface.Body = body.Id;
        Dirty(mapUid, surface);

        _surfaces[(system.ID, body.Id)] = mapUid;
        return mapUid;
    }

    private bool TryGetBody(string system, string body, out DHStarSystemPrototype proto, out DHCelestialBody found)
    {
        found = default!;
        proto = default!;
        if (!_proto.TryIndex<DHStarSystemPrototype>(system, out var indexed))
            return false;

        proto = indexed;

        var match = indexed.Bodies.Find(b => b.Id == body);
        if (match == null)
            return false;

        found = match;
        return true;
    }

    #endregion

    #region Интерфейс

    private void OnUiOpened(Entity<DHLandingConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnSelectBody(Entity<DHLandingConsoleComponent> ent, ref DHLandingSelectBodyMessage args)
    {
        ent.Comp.SelectedBody = args.Body;
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<DHLandingConsoleComponent> ent)
    {
        var shuttle = Transform(ent).GridUid;
        var map = shuttle != null ? Transform(shuttle.Value).MapUid : null;
        var bodies = new List<DHLandingBodyEntry>();
        var grids = new List<DHRiftNavGrid>();
        string? surfaceName = null;
        string status;
        var canAct = false;

        if (map != null && TryComp<DHPlanetSurfaceMapComponent>(map, out var surface))
        {
            surfaceName = TryGetBody(surface.System, surface.Body, out _, out var body)
                ? Loc.GetString(body.Name)
                : MetaData(map.Value).EntityName;

            CollectGrids(map.Value, shuttle, grids);
            canAct = CanFly(shuttle, out status);
            if (canAct)
                status = Loc.GetString("dh-landing-ready-ascend");
        }
        else if (_celestial.TryGetSystem(map, out var system, out var origin) && shuttle != null)
        {
            var position = _transform.GetWorldPosition(shuttle.Value) - origin;
            foreach (var body in system.Bodies)
            {
                if (!body.Landable || body.Biome == null)
                    continue;

                var altitude = Vector2.Distance(position, body.Position) - body.Radius;
                bodies.Add(new DHLandingBodyEntry(body.Id, Loc.GetString(body.Name), altitude,
                    altitude <= ent.Comp.LandingRange));
            }

            bodies.Sort((a, b) => a.Altitude.CompareTo(b.Altitude));

            if (ent.Comp.SelectedBody == null || bodies.Find(b => b.Id == ent.Comp.SelectedBody) == null)
                ent.Comp.SelectedBody = bodies.Count > 0 ? bodies[0].Id : null;

            var selected = bodies.Find(b => b.Id == ent.Comp.SelectedBody);
            if (selected != null &&
                _surfaces.TryGetValue((system.ID, selected.Id), out var surfaceMap) &&
                !TerminatingOrDeleted(surfaceMap))
            {
                CollectGrids(surfaceMap, shuttle, grids);
            }

            if (selected == null)
            {
                status = Loc.GetString("dh-landing-no-bodies");
            }
            else if (!selected.InRange)
            {
                status = Loc.GetString("dh-landing-out-of-range",
                    ("body", selected.Name), ("range", DHCelestial.FormatDistance(ent.Comp.LandingRange)));
            }
            else
            {
                canAct = CanFly(shuttle, out status);
                if (canAct)
                    status = Loc.GetString("dh-landing-ready-descend", ("body", selected.Name));
            }
        }
        else
        {
            status = shuttle == null ? Loc.GetString("dh-rift-nav-no-shuttle") : Loc.GetString("dh-landing-no-bodies");
        }

        _ui.SetUiState(ent.Owner, DHLandingUiKey.Key, new DHLandingState(surfaceName, bodies, ent.Comp.SelectedBody,
            grids, ent.Comp.ZoneSize, ent.Comp.LandingRange, status, canAct));
    }

    private void CollectGrids(EntityUid map, EntityUid? self, List<DHRiftNavGrid> grids)
    {
        var query = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            // Сама карта поверхности — тоже грид.
            if (uid == map || xform.MapUid != map || grids.Count >= MaxGrids)
                continue;

            var isSelf = uid == self;
            TryComp<IFFComponent>(uid, out var iff);
            var label = _shuttle.GetIFFLabel(uid, isSelf, iff);
            if (label == null && !isSelf)
                continue;

            grids.Add(new DHRiftNavGrid(label ?? string.Empty, _transform.GetWorldPosition(xform),
                _shuttle.GetIFFColor(uid, isSelf, iff), isSelf));
        }
    }

    private bool CanFly(EntityUid? shuttle, out string status)
    {
        if (shuttle == null || !TryComp<ShuttleComponent>(shuttle, out var comp) || !comp.Enabled)
        {
            status = Loc.GetString("dh-rift-nav-no-shuttle");
            return false;
        }

        if (!_shuttle.CanFTL(shuttle.Value, out var reason))
        {
            status = reason;
            return false;
        }

        status = string.Empty;
        return true;
    }

    #endregion

    #region Посадка и взлёт

    private void OnDescend(Entity<DHLandingConsoleComponent> ent, ref DHLandingDescendMessage args)
    {
        var shuttle = Transform(ent).GridUid;
        if (!CanFly(shuttle, out var status))
        {
            _popup.PopupEntity(status, ent, PopupType.MediumCaution);
            return;
        }

        var map = Transform(shuttle!.Value).MapUid;
        if (!_celestial.TryGetSystem(map, out var system, out var origin))
            return;

        var bodyId = args.Body; // ref-параметр нельзя захватывать в лямбде
        var body = system.Bodies.Find(b => b.Id == bodyId);
        if (body == null || !body.Landable ||
            !float.IsFinite(args.Position.X) || !float.IsFinite(args.Position.Y) ||
            MathF.Abs(args.Position.X) > ent.Comp.ZoneSize || MathF.Abs(args.Position.Y) > ent.Comp.ZoneSize)
        {
            return;
        }

        var position = _transform.GetWorldPosition(shuttle.Value) - origin;
        if (Vector2.Distance(position, body.Position) - body.Radius > ent.Comp.LandingRange)
        {
            _popup.PopupEntity(Loc.GetString("dh-landing-out-of-range",
                ("body", Loc.GetString(body.Name)), ("range", DHCelestial.FormatDistance(ent.Comp.LandingRange))),
                ent, PopupType.MediumCaution);
            return;
        }

        if (GetOrCreateSurface(system, body) is not { } surfaceMap)
            return;

        var coordinates = new EntityCoordinates(surfaceMap, args.Position);
        if (!_rift.ExitFree(shuttle.Value, coordinates))
        {
            _popup.PopupEntity(Loc.GetString("dh-rift-nav-exit-blocked"), ent, PopupType.MediumCaution);
            return;
        }

        if (TryComp<PhysicsComponent>(shuttle, out var physics))
            coordinates = coordinates.Offset(-physics.LocalCenter);

        var landed = EnsureComp<DHLandedComponent>(shuttle.Value);
        landed.System = system.ID;
        landed.Body = body.Id;
        landed.OrbitPosition = position;

        _shuttle.FTLToCoordinates(shuttle.Value, Comp<ShuttleComponent>(shuttle.Value), coordinates, Angle.Zero);
        _popup.PopupEntity(Loc.GetString("dh-landing-descending", ("body", Loc.GetString(body.Name))), ent, PopupType.Medium);
        UpdateUi(ent);
    }

    private void OnAscend(Entity<DHLandingConsoleComponent> ent, ref DHLandingAscendMessage args)
    {
        var shuttle = Transform(ent).GridUid;
        if (!CanFly(shuttle, out var status))
        {
            _popup.PopupEntity(status, ent, PopupType.MediumCaution);
            return;
        }

        var map = Transform(shuttle!.Value).MapUid;
        if (!TryComp<DHPlanetSurfaceMapComponent>(map, out var surface) ||
            !TryGetBody(surface.System, surface.Body, out var system, out var body) ||
            SystemMap(system.ID) is not { } systemMap ||
            !_celestial.TryGetSystem(systemMap, out _, out var origin))
        {
            return;
        }

        // Назад на ту же орбиту; без неё — над телом со стороны начала отсчёта системы.
        Vector2 orbit;
        if (TryComp<DHLandedComponent>(shuttle, out var landed) && landed.System == system.ID && landed.Body == body.Id)
        {
            orbit = landed.OrbitPosition;
        }
        else
        {
            var dir = body.Position.LengthSquared() > 1f ? -Vector2.Normalize(body.Position) : Vector2.UnitY;
            orbit = body.Position + dir * (body.Radius + AscentAltitude);
        }

        var coordinates = new EntityCoordinates(systemMap, origin + orbit);
        if (!_rift.ExitFree(shuttle.Value, coordinates))
        {
            _popup.PopupEntity(Loc.GetString("dh-rift-nav-exit-blocked"), ent, PopupType.MediumCaution);
            return;
        }

        if (TryComp<PhysicsComponent>(shuttle, out var physics))
            coordinates = coordinates.Offset(-physics.LocalCenter);

        RemComp<DHLandedComponent>(shuttle.Value);
        _shuttle.FTLToCoordinates(shuttle.Value, Comp<ShuttleComponent>(shuttle.Value), coordinates, Angle.Zero);
        _popup.PopupEntity(Loc.GetString("dh-landing-ascending"), ent, PopupType.Medium);
        UpdateUi(ent);
    }

    #endregion

    #region Газозаборник

    private void OnScoopUpdate(Entity<DHGasScoopComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        var working = false;
        ent.Comp.LastMolesPerSecond = 0f;

        var xform = Transform(ent);
        if (xform.Anchored &&
            xform.GridUid is { } grid &&
            (!TryComp<ApcPowerReceiverComponent>(ent, out var power) || power.Powered) &&
            _celestial.TryGetSystem(xform.MapUid, out var system, out var origin) &&
            _nodes.TryGetNode(ent.Owner, ent.Comp.OutletNode, out PipeNode? outlet) &&
            outlet.Air.Pressure < ent.Comp.MaxOutputPressure)
        {
            var position = _transform.GetWorldPosition(grid) - origin;
            foreach (var body in system.Bodies)
            {
                if (body.LayerGases.Count == 0)
                    continue;

                var distance = Vector2.Distance(position, body.Position);
                if (!DHCelestial.InGasLayer(body, distance))
                    continue;

                var depth = Math.Clamp((body.Radius - distance) / body.EnterableDepth, 0f, 1f);
                var amount = ent.Comp.MolesPerSecond * (0.25f + 0.75f * depth) * args.dt;

                var total = 0f;
                foreach (var fraction in body.LayerGases.Values)
                {
                    total += fraction;
                }

                if (total <= 0f)
                    break;

                var mix = new GasMixture(outlet.Air.Volume) { Temperature = body.LayerTemperature };
                foreach (var (gas, fraction) in body.LayerGases)
                {
                    mix.AdjustMoles(gas, amount * fraction / total);
                }

                _atmos.Merge(outlet.Air, mix);
                ent.Comp.LastMolesPerSecond = amount / MathF.Max(args.dt, 0.001f);
                working = true;
                break;
            }
        }

        _appearance.SetData(ent, DHGasScoopVisuals.Working, working);
    }

    #endregion
}
