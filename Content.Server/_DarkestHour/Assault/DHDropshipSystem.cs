using System.Numerics;
using Content.Server._DarkestHour.Shuttles.Rift;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Atmos.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._DarkestHour.Assault;

/// <summary>Суборбитальный челнок десанта.</summary>
[RegisterComponent]
public sealed partial class DHDropshipComponent : Component
{
    [ViewVariables]
    public EntityUid? Carrier;

    [ViewVariables]
    public DHDropshipState State = DHDropshipState.Docked;

    /// <summary>Куда летим: ЗП или ангар (null при <see cref="ToHangar"/>).</summary>
    [ViewVariables]
    public EntityUid? Destination;

    [ViewVariables]
    public bool ToHangar;

    /// <summary>На какой ЗП стоим.</summary>
    [ViewVariables]
    public EntityUid? Zone;

    [ViewVariables]
    public TimeSpan DepartAt;

    [ViewVariables]
    public TimeSpan ArriveAt;

    [ViewVariables]
    public TimeSpan NextShake;

    [DataField]
    public TimeSpan LaunchDelay = TimeSpan.FromSeconds(10);

    /// <summary>Перелёт, когда авианосец на орбите.</summary>
    [DataField]
    public TimeSpan OrbitFlightTime = TimeSpan.FromSeconds(40);

    /// <summary>Перелёт, когда авианосец висит в атмосфере.</summary>
    [DataField]
    public TimeSpan AtmosphereFlightTime = TimeSpan.FromSeconds(20);
}

/// <summary>
/// Челноки «Штурма»: стоят пристыкованными к авианосцу, по команде с консоли пилота летят на автопилоте на ЗП
/// (готовые площадки у базы или переносные маяки) и обратно в ангар. В полёте челнок висит в атмосфере над целью —
/// по нему бьют ПКО и ПВО, из него можно прыгнуть с парашютом. Авианосец сам на поверхность не садится.
/// </summary>
public sealed partial class DHDropshipSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DHCelestialSystem _celestial = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private DockingSystem _docking = default!;
    [Dependency] private Content.Server._DarkestHour.HeavyGuns.DHHeavyGunSystem _guns = default!;
    [Dependency] private DHLandingSystem _landing = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DHRiftSystem _rift = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private ServerStationSystem _station = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly SoundSpecifier LaunchSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_begin.ogg");
    private static readonly SoundSpecifier LandSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_end.ogg");
    private static readonly SoundSpecifier AlarmSound = new SoundPathSpecifier("/Audio/Machines/alarm.ogg",
        AudioParams.Default.WithVolume(-6f));

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextUpdate;
    private int _temporaryZones;
    private int _zones;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHLandingZoneComponent, MapInitEvent>(OnZoneMapInit);
        SubscribeLocalEvent<DHLandingZoneBeaconComponent, UseInHandEvent>(OnBeaconUse);
        SubscribeLocalEvent<DHLandingZoneBeaconComponent, DHLandingZoneDeployDoAfterEvent>(OnBeaconDeployed);

        Subs.BuiEvents<DHDropshipConsoleComponent>(DHDropshipUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnConsoleOpened);
            subs.Event<DHDropshipFlyMessage>(OnFly);
            subs.Event<DHDropshipAbortMessage>(OnAbort);
        });
    }

    #region Появление челноков

    /// <summary>Загрузить челноки рядом с авианосцем и пристыковать их; они входят в станцию авианосца.</summary>
    public void SpawnDropships(EntityUid carrier, EntityUid? station, ResPath path, IReadOnlyList<string> names)
    {
        var xform = Transform(carrier);
        var origin = _transform.GetWorldPosition(xform);
        for (var i = 0; i < names.Count; i++)
        {
            if (!_loader.TryLoadGrid(xform.MapID, path, out var grid, offset: origin + new Vector2(-60f - i * 25f, 0f)))
            {
                Log.Error($"Failed to load dropship {path}");
                continue;
            }

            _meta.SetEntityName(grid.Value, names[i]);
            var atmos = EnsureComp<GridAtmosphereComponent>(grid.Value);
            EnsureComp<GasTileOverlayComponent>(grid.Value);
            _atmos.RebuildGridAtmosphere((grid.Value, atmos, grid.Value.Comp));

            var dropship = EnsureComp<DHDropshipComponent>(grid.Value);
            dropship.Carrier = carrier;
            if (station != null)
                _station.AddGridToStation(station.Value, grid.Value);

            Dock((grid.Value, dropship));
        }
    }

    private void Dock(Entity<DHDropshipComponent> ship)
    {
        if (ship.Comp.Carrier is not { } carrier || !Exists(carrier) || !TryComp<ShuttleComponent>(ship, out var shuttle))
            return;

        _shuttle.TryFTLDock(ship, shuttle, carrier);
        ship.Comp.State = DHDropshipState.Docked;
        ship.Comp.Zone = null;
    }

    #endregion

    #region Зоны посадки

    private void OnZoneMapInit(Entity<DHLandingZoneComponent> ent, ref MapInitEvent args)
    {
        if (!string.IsNullOrEmpty(ent.Comp.ZoneName))
            return;

        // Маяк, расставленный на карте и переименованный мапером: его имя и есть имя зоны.
        var meta = MetaData(ent);
        if (!ent.Comp.Temporary && meta.EntityPrototype is { } proto && meta.EntityName != proto.Name)
        {
            ent.Comp.ZoneName = meta.EntityName;
            return;
        }

        ent.Comp.ZoneName = ent.Comp.Temporary
            ? Loc.GetString("dh-lz-temporary-name", ("number", ++_temporaryZones))
            : Loc.GetString("dh-lz-name", ("number", ++_zones));
        _meta.SetEntityName(ent, ent.Comp.ZoneName);
    }

    private void OnBeaconUse(Entity<DHLandingZoneBeaconComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!HasComp<DHPlanetSurfaceMapComponent>(Transform(args.User).MapUid))
        {
            _popup.PopupEntity(Loc.GetString("dh-lz-beacon-not-surface"), args.User, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.DeployTime,
            new DHLandingZoneDeployDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnBeaconDeployed(Entity<DHLandingZoneBeaconComponent> ent, ref DHLandingZoneDeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        // Маяк — на грунт планеты, а не на грид под ногами (челнок, мешки): иначе челнок увозит его с собой.
        var userXform = Transform(args.User);
        if (userXform.MapUid is not { } map)
            return;

        var zone = _landing.SpawnOnGround(ent.Comp.Zone, map, _transform.GetWorldPosition(userXform));
        _popup.PopupEntity(Loc.GetString("dh-lz-beacon-deployed", ("zone", Name(zone))), args.User, args.User,
            PopupType.Medium);
        QueueDel(ent);
    }

    /// <summary>Id звёздной системы карты: орбита, слой атмосферы или поверхность.</summary>
    private string? SystemOfMap(EntityUid? map)
    {
        if (map == null)
            return null;

        if (TryComp<DHPlanetSurfaceMapComponent>(map, out var surface))
            return surface.System;

        if (TryComp<DHPlanetAtmosphereMapComponent>(map, out var atmosphere))
            return atmosphere.System;

        return _celestial.TryGetSystem(map, out var system, out _) ? system.ID : null;
    }

    #endregion

    #region Полёт

    private Entity<DHDropshipComponent>? ShipOf(EntityUid console)
    {
        return Transform(console).GridUid is { } grid && TryComp<DHDropshipComponent>(grid, out var comp)
            ? (grid, comp)
            : null;
    }

    private void OnFly(Entity<DHDropshipConsoleComponent> ent, ref DHDropshipFlyMessage args)
    {
        if (ShipOf(ent) is not { } ship)
            return;

        var comp = ship.Comp;
        var hovering = comp.State == DHDropshipState.InFlight && comp.Destination == null && !comp.ToHangar;
        if (comp.State != DHDropshipState.Docked && comp.State != DHDropshipState.Landed && !hovering)
        {
            _popup.PopupEntity(Loc.GetString("dh-dropship-busy"), ent, args.Actor);
            return;
        }

        if (args.Zone is { } netZone)
        {
            if (!TryGetEntity(netZone, out var zone) || !ZoneAvailable(ship, zone.Value, out var reason))
            {
                _popup.PopupEntity(Loc.GetString("dh-dropship-zone-unavailable"), ent, args.Actor);
                return;
            }

            comp.Destination = zone;
            comp.ToHangar = false;
        }
        else
        {
            if (comp.State == DHDropshipState.Docked || comp.Carrier is not { } carrier || !Exists(carrier) ||
                TryComp<DHAssaultCarrierComponent>(carrier, out var carrierComp) && carrierComp.Downed)
            {
                _popup.PopupEntity(Loc.GetString("dh-dropship-no-hangar"), ent, args.Actor);
                return;
            }

            comp.Destination = null;
            comp.ToHangar = true;
        }

        if (hovering)
        {
            // Уже в воздухе: сразу курс на новую цель, перелёт короче.
            comp.ArriveAt = _timing.CurTime + comp.AtmosphereFlightTime / 2;
            if (comp.Destination is { } newTarget)
                _guns.WarnIncomingDropship(newTarget, DHAssaultFaction.Empire, Name(ship));
            Announce(ship, Loc.GetString("dh-dropship-course", ("destination", DestinationName(comp))));
        }
        else
        {
            comp.State = DHDropshipState.Departing;
            comp.DepartAt = _timing.CurTime + comp.LaunchDelay;
            _audio.PlayGlobal(AlarmSound, Filter.Empty().AddInGrid(ship), true);
            Announce(ship, Loc.GetString("dh-dropship-departing", ("destination", DestinationName(comp)),
                ("seconds", (int) comp.LaunchDelay.TotalSeconds)));
        }

        UpdateUi(ent, ship);
    }

    private void OnAbort(Entity<DHDropshipConsoleComponent> ent, ref DHDropshipAbortMessage args)
    {
        if (ShipOf(ent) is not { } ship || ship.Comp.State != DHDropshipState.Departing)
            return;

        // Отмена взлёта: остаёмся там, где стояли.
        ship.Comp.State = ship.Comp.Zone != null ? DHDropshipState.Landed : DHDropshipState.Docked;
        ship.Comp.Destination = null;
        ship.Comp.ToHangar = false;
        Announce(ship, Loc.GetString("dh-dropship-aborted"));
        UpdateUi(ent, ship);
    }

    private string DestinationName(DHDropshipComponent comp)
    {
        if (comp.ToHangar)
            return comp.Carrier is { } carrier && Exists(carrier)
                ? Loc.GetString("dh-dropship-hangar", ("carrier", Name(carrier)))
                : Loc.GetString("dh-dropship-hangar-lost");

        return comp.Destination is { } zone && Exists(zone) ? Name(zone) : "—";
    }

    /// <summary>ЗП в той же звёздной системе, что авианосец (или челнок), не та, где стоим, и свободна.</summary>
    private bool ZoneAvailable(Entity<DHDropshipComponent> ship, EntityUid zone, out string reason)
    {
        reason = string.Empty;
        var zoneXform = Transform(zone);
        if (!HasComp<DHLandingZoneComponent>(zone) || zoneXform.MapUid is not { } zoneMap)
        {
            reason = Loc.GetString("dh-dropship-note-lost");
            return false;
        }

        var reference = ship.Comp.Carrier is { } carrier && Exists(carrier) ? carrier : ship.Owner;
        if (SystemOfMap(Transform(reference).MapUid) is not { } system || SystemOfMap(zoneMap) != system)
        {
            reason = Loc.GetString("dh-dropship-note-other-system");
            return false;
        }

        if (ship.Comp.Zone == zone && ship.Comp.State == DHDropshipState.Landed)
        {
            reason = Loc.GetString("dh-dropship-note-here");
            return false;
        }

        if (!_rift.ExitFree(ship, zoneXform.Coordinates))
        {
            reason = Loc.GetString("dh-dropship-note-blocked");
            return false;
        }

        return true;
    }

    private void Announce(EntityUid ship, string text)
    {
        var consoles = EntityQueryEnumerator<DHDropshipConsoleComponent, TransformComponent>();
        while (consoles.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == ship)
                _popup.PopupEntity(text, uid, PopupType.LargeCaution);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var ships = EntityQueryEnumerator<DHDropshipComponent>();
        var due = new List<Entity<DHDropshipComponent>>();
        while (ships.MoveNext(out var uid, out var comp))
        {
            if (comp.State == DHDropshipState.Departing && now >= comp.DepartAt ||
                comp.State == DHDropshipState.InFlight && (comp.Destination != null || comp.ToHangar) &&
                now >= comp.ArriveAt)
            {
                due.Add((uid, comp));
            }

            if (comp.State == DHDropshipState.InFlight && now >= comp.NextShake)
            {
                comp.NextShake = now + TimeSpan.FromSeconds(2);
                _landing.Shake(uid, 0.3f);
            }
        }

        foreach (var ship in due)
        {
            if (ship.Comp.State == DHDropshipState.Departing)
                Launch(ship);
            else
                Arrive(ship);
        }

        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;
        FollowCarriers();

        var consoles = EntityQueryEnumerator<DHDropshipConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            if (_ui.IsUiOpen(uid, DHDropshipUiKey.Key) && ShipOf(uid) is { } ship)
                UpdateUi((uid, console), ship);
        }
    }

    /// <summary>Взлёт: расстыковка и переход в атмосферу над целью (над ЗП или над местом взлёта — к ангару).</summary>
    private void Launch(Entity<DHDropshipComponent> ship)
    {
        var comp = ship.Comp;
        EntityUid? surfaceMap = null;
        Vector2 above;
        if (!comp.ToHangar && comp.Destination is { } zone && Exists(zone))
        {
            surfaceMap = Transform(zone).MapUid;
            above = _transform.GetWorldPosition(zone);
        }
        else
        {
            surfaceMap = comp.Zone is { } current && Exists(current) ? Transform(current).MapUid : null;
            above = _transform.GetWorldPosition(ship);
        }

        // К ангару из ангара не летаем; к ЗП из ангара — через атмосферу тела этой ЗП.
        if (surfaceMap == null || !TryComp<DHPlanetSurfaceMapComponent>(surfaceMap, out var surface) ||
            _landing.EnsureAtmosphere(surface.System, surface.Body) is not { } sky)
        {
            comp.State = comp.Zone != null ? DHDropshipState.Landed : DHDropshipState.Docked;
            Announce(ship, Loc.GetString("dh-dropship-zone-unavailable"));
            return;
        }

        var fromOrbit = comp.Carrier is { } carrier && Exists(carrier) &&
                        !HasComp<DHPlanetAtmosphereMapComponent>(Transform(carrier).MapUid);

        _docking.UndockDocks(ship);
        _landing.PlaceShuttle(ship, new EntityCoordinates(sky, above));
        _audio.PlayGlobal(LaunchSound, Filter.Empty().AddInGrid(ship), true);

        comp.State = DHDropshipState.InFlight;
        comp.Zone = null;
        comp.ArriveAt = _timing.CurTime + (fromOrbit ? comp.OrbitFlightTime : comp.AtmosphereFlightTime);
        comp.NextShake = _timing.CurTime;
        // РЛС противника на карте ЗП замечает челнок на подлёте.
        if (!comp.ToHangar && comp.Destination is { } target)
            _guns.WarnIncomingDropship(target, DHAssaultFaction.Empire, Name(ship));

        Announce(ship, Loc.GetString("dh-dropship-launched", ("destination", DestinationName(comp)),
            ("seconds", (int) (comp.ArriveAt - _timing.CurTime).TotalSeconds)));
    }

    private void Arrive(Entity<DHDropshipComponent> ship)
    {
        var comp = ship.Comp;
        if (comp.ToHangar)
        {
            comp.ToHangar = false;
            if (comp.Carrier is { } carrier && Exists(carrier))
            {
                Dock(ship);
                _audio.PlayGlobal(LandSound, Filter.Empty().AddInGrid(ship), true);
                Announce(ship, Loc.GetString("dh-dropship-docked", ("carrier", Name(carrier))));
            }
            else
            {
                Announce(ship, Loc.GetString("dh-dropship-hangar-lost"));
            }

            return;
        }

        if (comp.Destination is not { } zone || !ZoneAvailable(ship, zone, out var reason))
        {
            // ЗП пропала или занята — зависаем в воздухе, пилот выбирает другую.
            comp.Destination = null;
            Announce(ship, Loc.GetString("dh-dropship-hovering"));
            return;
        }

        _landing.PlaceShuttle(ship, Transform(zone).Coordinates);
        comp.State = DHDropshipState.Landed;
        comp.Zone = zone;
        comp.Destination = null;
        _landing.Shake(ship, 1f);
        _audio.PlayGlobal(LandSound, Filter.Empty().AddInGrid(ship), true);
        Announce(ship, Loc.GetString("dh-dropship-landed", ("zone", Name(zone))));
    }

    /// <summary>
    /// Пристыкованные челноки следуют за авианосцем: прыжок и спуск в атмосферу переносят только сам авианосец
    /// (стыковка рвётся) — челноки стыкуются заново, как только он на месте.
    /// </summary>
    private void FollowCarriers()
    {
        var ships = EntityQueryEnumerator<DHDropshipComponent, TransformComponent>();
        var lost = new List<Entity<DHDropshipComponent>>();
        while (ships.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.State != DHDropshipState.Docked || comp.Carrier is not { } carrier || !Exists(carrier))
                continue;

            if (TryComp<FTLComponent>(carrier, out var ftl) &&
                ftl.State is FTLState.Starting or FTLState.Travelling or FTLState.Arriving)
            {
                continue;
            }

            if (xform.MapUid != Transform(carrier).MapUid)
                lost.Add((uid, comp));
        }

        foreach (var ship in lost)
        {
            Dock(ship);
        }
    }

    #endregion

    #region Консоль

    private void OnConsoleOpened(Entity<DHDropshipConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (ShipOf(ent) is { } ship)
            UpdateUi(ent, ship);
    }

    private void UpdateUi(Entity<DHDropshipConsoleComponent> console, Entity<DHDropshipComponent> ship)
    {
        var comp = ship.Comp;
        var destinations = new List<DHDropshipDestination>();

        if (comp.State != DHDropshipState.Docked && comp.Carrier is { } carrier && Exists(carrier))
            destinations.Add(new DHDropshipDestination(null, Loc.GetString("dh-dropship-hangar", ("carrier", Name(carrier))),
                true, string.Empty));

        var zones = EntityQueryEnumerator<DHLandingZoneComponent>();
        while (zones.MoveNext(out var uid, out var zone))
        {
            var available = ZoneAvailable(ship, uid, out var reason);
            if (!available && reason == Loc.GetString("dh-dropship-note-other-system"))
                continue;

            destinations.Add(new DHDropshipDestination(GetNetEntity(uid), Name(uid), available, reason));
        }

        string location = comp.State switch
        {
            DHDropshipState.Docked => comp.Carrier is { } c && Exists(c)
                ? Loc.GetString("dh-dropship-hangar", ("carrier", Name(c)))
                : Loc.GetString("dh-dropship-hangar-lost"),
            DHDropshipState.Landed => comp.Zone is { } z && Exists(z) ? Name(z) : "—",
            _ => Loc.GetString("dh-dropship-in-air"),
        };

        var now = _timing.CurTime;
        var seconds = comp.State switch
        {
            DHDropshipState.Departing => (int) Math.Ceiling((comp.DepartAt - now).TotalSeconds),
            DHDropshipState.InFlight when comp.Destination != null || comp.ToHangar =>
                (int) Math.Ceiling((comp.ArriveAt - now).TotalSeconds),
            _ => 0,
        };

        string? destination = comp.State is DHDropshipState.Departing or DHDropshipState.InFlight &&
                              (comp.Destination != null || comp.ToHangar)
            ? DestinationName(comp)
            : null;

        _ui.SetUiState(console.Owner, DHDropshipUiKey.Key, new DHDropshipConsoleState(Name(ship), comp.State, location,
            destination, Math.Max(0, seconds), destinations));
    }

    #endregion
}
