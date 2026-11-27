using System.Numerics;
using Content.Server._DarkestHour.Shuttles.Rift;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Levels;
using Content.Shared.Audio;
using Content.Shared.Movement.Systems;
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
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._DarkestHour.Assault;

/// <summary>Суборбитальный челнок десанта.</summary>
[RegisterComponent]
public sealed partial class DHDropshipComponent : Component
{
    [ViewVariables]
    public EntityUid? Carrier;

    /// <summary>Место стоянки в ангаре авианосца (<see cref="DHDropshipHangarComponent"/>), если челнок стоит в ангаре, а не у шлюза.</summary>
    [ViewVariables]
    public EntityUid? Hangar;

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
    public TimeSpan NextShake;

    [DataField]
    public TimeSpan LaunchDelay = TimeSpan.FromSeconds(10);

    /// <summary>Переход с орбиты в атмосферу над полем боя (и обратно).</summary>
    [DataField]
    public TimeSpan TransitTime = TimeSpan.FromSeconds(14);

    /// <summary>Скорость полёта над полем боя, тайлов в секунду.</summary>
    [DataField]
    public float CruiseSpeed = 8f;

    /// <summary>Скорость в слое атмосферы под управлением пилота (заход на выброску), тайлов в секунду.</summary>
    [DataField]
    public float AtmosphereSpeed = 3f;

    /// <summary>Снижение (и подъём) на один уровень.</summary>
    [DataField]
    public TimeSpan LevelStep = TimeSpan.FromSeconds(3);

    /// <summary>Высота полёта: на столько уровней выше верхнего этажа (на «Терминусе» — уровень 4).</summary>
    [DataField]
    public int CruiseHeight = 3;

    /// <summary>Примерное расстояние точки входа от цели.</summary>
    public const float EntryDistance = 200f;

    /// <summary>Фаза полёта (при <see cref="DHDropshipState.InFlight"/>).</summary>
    [ViewVariables]
    public DHDropshipFlight Flight;

    /// <summary>Переход: на орбиту (к авианосцу или другой земле), а не с неё.</summary>
    [ViewVariables]
    public bool TransitOut;

    /// <summary>Конец перехода или следующий шаг по уровням.</summary>
    [ViewVariables]
    public TimeSpan NextStep;

    /// <summary>Земля (уровень 0 стопки), над которой летим.</summary>
    [ViewVariables]
    public EntityUid? Surface;

    /// <summary>Уровень в стопке поверхности; −1 — не над полем боя (в переходе, в ангаре).</summary>
    [ViewVariables]
    public int Level = -1;

    [ViewVariables]
    public int CruiseLevel;

    /// <summary>Точка курса на уровне воздуха (центр челнока).</summary>
    [ViewVariables]
    public Vector2? Waypoint;

    /// <summary>Точка входа, выбранная при взлёте с орбиты.</summary>
    [ViewVariables]
    public Vector2? Entry;

    /// <summary>Ручное управление (автопилот выключен).</summary>
    [ViewVariables]
    public bool Manual;

    /// <summary>Кто сейчас ведёт челнок с пульта (его кнопки движения переданы пульту).</summary>
    [ViewVariables]
    public EntityUid? Pilot;

    [ViewVariables]
    public EntityUid? PilotConsole;

    [ViewVariables]
    public int HoverLevel = -1;

    [ViewVariables]
    public bool HoverLift;

    [ViewVariables]
    public TimeSpan NextHoverRefresh;

    /// <summary>
    /// ЛТЦ (ложные тепловые цели, решение автора 2026-10-30): залп в полёте сбивает наведение ПВО (шахты, автопушки,
    /// зенитки) до конца захода. Запас — <see cref="MaxFlares"/>, пополняется пристыкованным к авианосцу.
    /// </summary>
    [DataField]
    public int MaxFlares = 2;

    [DataField]
    public int Flares = 2;

    /// <summary>Пристыкованный челнок получает один залп ЛТЦ за это время.</summary>
    [DataField]
    public TimeSpan FlareRefill = TimeSpan.FromMinutes(4);

    /// <summary>Сколько ЛТЦ прикрывают, если челнок просто висит в воздухе (без курса).</summary>
    [DataField]
    public TimeSpan FlareHover = TimeSpan.FromSeconds(20);

    /// <summary>ПВО не наводится на челнок до этого времени.</summary>
    [ViewVariables]
    public TimeSpan FlaresUntil;

    [ViewVariables]
    public TimeSpan NextFlareRefill;

    // --- Палубы, рампа и ручной полёт (решения автора 2026-11-27) ---

    /// <summary>Нижний ярус (рампа, стойки, брюхо) — на уровне ниже кабины, только когда челнок стоит на земле.</summary>
    [ViewVariables]
    public EntityUid? Underside;

    /// <summary>Ходовая крыша — на уровне выше кабины, пока челнок над полем боя.</summary>
    [ViewVariables]
    public EntityUid? Roof;

    [ViewVariables]
    public bool RampLowered;

    /// <summary>Временная ЗП, созданная посадкой вне площадок: при взлёте убирается.</summary>
    [ViewVariables]
    public EntityUid? FieldZone;

    /// <summary>Взлёт с неподготовленной площадки: прогрев и подъём дольше.</summary>
    [ViewVariables]
    public bool FieldTakeoff;

    /// <summary>Прогрев двигателей и шаг подъёма вне ЗП во столько раз дольше.</summary>
    [DataField]
    public float FieldTakeoffFactor = 4f;

    /// <summary>Поворот корпуса в ручном полёте, градусов в секунду.</summary>
    [DataField]
    public float TurnRate = 40f;

    /// <summary>Ручная смена уровня — не чаще.</summary>
    [DataField]
    public TimeSpan ManualLevelCooldown = TimeSpan.FromSeconds(1.5);

    [ViewVariables]
    public TimeSpan NextManualLevel;
}

/// <summary>
/// Место стоянки челнока в ангаре авианосца (решение автора 2026-11-11, «Зона вылета 1»): маркер на гриде авианосца,
/// у которого в начале раунда загружается свой челнок (карта <see cref="Map"/>) и куда он возвращается из полёта.
/// Челнок ставится так, что точка <see cref="Anchor"/> его грида совпадает с маркером, а поворот — с поворотом маркера.
/// Под челнок в полу авианосца оставлен вырез по его клеткам: люки выходят прямо на палубу ангара.
/// Если маркеров на авианосце нет — по-старому: челноки пристыковываются к шлюзам.
/// </summary>
[RegisterComponent]
public sealed partial class DHDropshipHangarComponent : Component
{
    [DataField]
    public ResPath Map = new("/Maps/_DarkestHour/Shuttles/midway.yml");

    /// <summary>Точка грида челнока (в его координатах), которая встаёт на маркер.</summary>
    [DataField]
    public Vector2 Anchor = new(0.5f, 0.5f);

    [ViewVariables]
    public EntityUid? Ship;
}

/// <summary>
/// Челноки «Штурма»: стоят пристыкованными к авианосцу, по команде с консоли пилота летят на автопилоте на ЗП
/// (готовые площадки у базы или переносные маяки) и обратно в ангар. В полёте челнок висит в атмосфере над целью —
/// по нему бьют ПКО и ПВО, из него можно прыгнуть с парашютом. Авианосец сам на поверхность не садится.
/// </summary>
public sealed partial class DHDropshipSystem : EntitySystem
{
    [Dependency] private Content.Server._DarkestHour.Aviation.DHAircraftSystem _aircraft = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private Content.Server._DarkestHour.Carrier.DHCarrierDamageSystem _carrierDamage = default!;
    [Dependency] private DHFlareEffectsServerSystem _flareEffects = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DHCelestialSystem _celestial = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private DockingSystem _docking = default!;
    [Dependency] private Content.Server._DarkestHour.HeavyGuns.DHHeavyGunSystem _guns = default!;
    [Dependency] private DHLandingSystem _landing = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DHRiftSystem _rift = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private ServerStationSystem _station = default!;
    [Dependency] private ITileDefinitionManager _tileDefs = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private DHZLevelSystem _zLevels = default!;
    [Dependency] private DHDropshipInputSystem _dropshipInput = default!;
    [Dependency] private Content.Server._DarkestHour.Carrier.DHCarrierDecksSystem _carrierDecks = default!;

    private static readonly SoundSpecifier LaunchSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_begin.ogg");
    private static readonly SoundSpecifier LandSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_end.ogg");
    private static readonly SoundSpecifier AlarmSound = new SoundPathSpecifier("/Audio/Machines/alarm.ogg",
        AudioParams.Default.WithVolume(-6f));

    private static readonly SoundSpecifier FlareSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/flaregun.ogg");


    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextUpdate;
    private int _temporaryZones;
    private int _zones;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHDropshipComponent, ComponentShutdown>(OnShipShutdown);
        SubscribeLocalEvent<DHLandingZoneComponent, MapInitEvent>(OnZoneMapInit);
        SubscribeLocalEvent<DHLandingZoneBeaconComponent, UseInHandEvent>(OnBeaconUse);
        SubscribeLocalEvent<DHLandingZoneBeaconComponent, DHLandingZoneDeployDoAfterEvent>(OnBeaconDeployed);
        SubscribeLocalEvent<DHDropshipRampControlComponent, Content.Shared.Interaction.InteractHandEvent>(OnRampControl);
        _dropshipInput.KeyPressed += OnPilotKey;

        Subs.BuiEvents<DHDropshipConsoleComponent>(DHDropshipUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnConsoleOpened);
            subs.Event<DHDropshipFlyMessage>(OnFly);
            subs.Event<DHDropshipAbortMessage>(OnAbort);
            subs.Event<DHDropshipFlaresMessage>(OnFlares);
            subs.Event<DHDropshipManualMessage>(OnManual);
            subs.Event<DHDropshipAtmosphereMessage>(OnAtmosphere);
            subs.Event<DHDropshipRampMessage>(OnRampMessage);
            subs.Event<DHDropshipAltitudeMessage>(OnAltitudeMessage);
            subs.Event<BoundUIClosedEvent>(OnConsoleClosed);
        });
    }

    #region Появление челноков

    /// <summary>
    /// Загрузить челноки авианосца: в ангар на маркеры <see cref="DHDropshipHangarComponent"/>, а если их нет —
    /// рядом с авианосцем с пристыковкой к шлюзам. Челноки входят в станцию авианосца.
    /// </summary>
    public void SpawnDropships(EntityUid carrier, EntityUid? station, ResPath path, IReadOnlyList<string> names)
    {
        var xform = Transform(carrier);

        var hangars = new List<Entity<DHDropshipHangarComponent>>();
        var query = EntityQueryEnumerator<DHDropshipHangarComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var hangar, out var hangarXform))
        {
            // Маркер стоянки — на любом уровне авианосца (с 2026-11-27 — на полётной палубе).
            if (_carrierDecks.OnCarrier(hangarXform.GridUid, carrier))
                hangars.Add((uid, hangar));
        }

        if (hangars.Count > 0)
        {
            hangars.Sort((a, b) => a.Owner.Id.CompareTo(b.Owner.Id));
            for (var i = 0; i < hangars.Count; i++)
            {
                var hangar = hangars[i];
                var (origin, rotation) = HangarPlacement(hangar);
                if (!_loader.TryLoadGrid(Transform(hangar).MapID, hangar.Comp.Map, out var grid, offset: origin, rot: rotation))
                {
                    Log.Error($"Failed to load dropship {hangar.Comp.Map}");
                    continue;
                }

                hangar.Comp.Ship = grid.Value.Owner;
                var name = i < names.Count ? names[i] : Loc.GetString("dh-dropship-name-extra", ("number", i + 1));
                var setup = SetupDropship(grid.Value, carrier, station, name);
                setup.Hangar = hangar.Owner;
                LoadDecks((grid.Value.Owner, setup), hangar.Comp.Map);
                SyncDecks((grid.Value.Owner, setup));
            }

            return;
        }

        var worldOrigin = _transform.GetWorldPosition(xform);
        for (var i = 0; i < names.Count; i++)
        {
            if (!_loader.TryLoadGrid(xform.MapID, path, out var grid, offset: worldOrigin + new Vector2(-60f - i * 25f, 0f)))
            {
                Log.Error($"Failed to load dropship {path}");
                continue;
            }

            var dropship = SetupDropship(grid.Value, carrier, station, names[i]);
            LoadDecks((grid.Value.Owner, dropship), path);
            Dock((grid.Value, dropship));
        }
    }

    private DHDropshipComponent SetupDropship(Entity<MapGridComponent> grid, EntityUid carrier, EntityUid? station, string name)
    {
        _meta.SetEntityName(grid, name);
        var atmos = EnsureComp<GridAtmosphereComponent>(grid);
        EnsureComp<GasTileOverlayComponent>(grid);
        _atmos.RebuildGridAtmosphere((grid, atmos, grid.Comp));

        var dropship = EnsureComp<DHDropshipComponent>(grid);
        dropship.Carrier = carrier;
        if (station != null)
            _station.AddGridToStation(station.Value, grid);

        return dropship;
    }

    /// <summary>Мировые положение начала грида челнока и его поворот для стоянки на маркере ангара.</summary>
    private (Vector2 Origin, Angle Rotation) HangarPlacement(Entity<DHDropshipHangarComponent> hangar)
    {
        var (position, rotation) = _transform.GetWorldPositionRotation(hangar);
        return (position - rotation.RotateVec(hangar.Comp.Anchor), rotation);
    }

    private void Dock(Entity<DHDropshipComponent> ship)
    {
        if (ship.Comp.Carrier is not { } carrier || !Exists(carrier))
            return;

        if (ship.Comp.Hangar is { } hangarUid && TryComp<DHDropshipHangarComponent>(hangarUid, out var hangar))
        {
            // В ангар челнок ставится точно на своё место, без стыковки (на карте уровня, где маркер).
            if (Transform(hangarUid).MapUid is not { } map)
                return;

            var (origin, rotation) = HangarPlacement((hangarUid, hangar));
            _transform.SetCoordinates(ship, Transform(ship), new EntityCoordinates(map, origin), rotation);
            if (TryComp<PhysicsComponent>(ship, out var physics))
            {
                _physics.SetLinearVelocity(ship, Vector2.Zero, body: physics);
                _physics.SetAngularVelocity(ship, 0f, body: physics);
            }
        }
        else
        {
            if (!TryComp<ShuttleComponent>(ship, out var shuttle))
                return;

            _shuttle.TryFTLDock(ship, shuttle, carrier);
        }

        if (ship.Comp.RampLowered)
            SetRamp(ship, false, null);

        ship.Comp.State = DHDropshipState.Docked;
        ship.Comp.Zone = null;
        ClearFlight(ship.Comp);
        ClearHover(ship);
        SyncDecks(ship);
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
        // В полёте цель можно сменить в любой момент, кроме перехода орбита — атмосфера.
        var airborne = comp.State == DHDropshipState.InFlight && comp.Flight != DHDropshipFlight.Transit;
        if (comp.State != DHDropshipState.Docked && comp.State != DHDropshipState.Landed && !airborne)
        {
            _popup.PopupEntity(Loc.GetString("dh-dropship-busy"), ent, args.Actor);
            return;
        }

        // Ангар авианосца повреждён: пока пробоину не заварят, из ангара не вылететь (живучесть, п. 19).
        if (comp.State == DHDropshipState.Docked &&
            _carrierDamage.Damaged(comp.Carrier, Content.Shared._DarkestHour.Carrier.DHCarrierCompartmentKind.Hangar))
        {
            _popup.PopupEntity(Loc.GetString("dh-dropship-hangar-damaged"), ent, args.Actor);
            return;
        }

        if (args.Zone is { } netZone)
        {
            if (!TryGetEntity(netZone, out var zone) || !ZoneAvailable(ship, zone.Value, out _))
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

        if (airborne)
        {
            // Уже в воздухе: сразу новый курс (если ведёт пилот — автопилот подхватит, когда его включат).
            Retarget(ship);
            WarnTarget(ship);
            Announce(ship, Loc.GetString("dh-dropship-course", ("destination", DestinationName(comp))));
        }
        else
        {
            // Рампа поднимается сама; с неподготовленной площадки прогрев двигателей дольше.
            if (comp.RampLowered)
                SetRamp(ship, false, null);

            var delay = comp.State == DHDropshipState.Landed && comp.FieldZone != null && comp.Zone == comp.FieldZone
                ? comp.LaunchDelay * comp.FieldTakeoffFactor
                : comp.LaunchDelay;
            comp.State = DHDropshipState.Departing;
            comp.DepartAt = _timing.CurTime + delay;
            _audio.PlayGlobal(AlarmSound, Filter.Empty().AddInGrid(ship), true);
            Announce(ship, Loc.GetString(delay > comp.LaunchDelay ? "dh-dropship-departing-field" : "dh-dropship-departing",
                ("destination", DestinationName(comp)), ("seconds", (int) delay.TotalSeconds)));
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

    private void OnFlares(Entity<DHDropshipConsoleComponent> ent, ref DHDropshipFlaresMessage args)
    {
        if (ShipOf(ent) is not { } ship)
            return;

        var comp = ship.Comp;
        var now = _timing.CurTime;
        if (comp.State != DHDropshipState.InFlight || comp.Flares <= 0 || now < comp.FlaresUntil)
        {
            UpdateUi(ent, ship);
            return;
        }

        comp.Flares--;

        // До посадки (или, если висим без курса, на время), с небольшим запасом.
        var until = comp.Destination != null || comp.ToHangar
            ? now + TimeSpan.FromSeconds(EstimateSeconds(ship))
            : now + comp.FlareHover;
        comp.FlaresUntil = (until > now + comp.FlareHover ? until : now + comp.FlareHover) + TimeSpan.FromSeconds(3);

        _audio.PlayGlobal(FlareSound, Filter.Empty().AddInGrid(ship), true);
        Announce(ship, Loc.GetString("dh-dropship-flares-fired", ("left", comp.Flares)));
        SpawnDecoysBelow(ship);
        UpdateUi(ent, ship);
    }

    /// <summary>
    /// ЛТЦ видно (план осады п. 8): в небе у челнока ловушки разлетаются со шлейфом, на поверхности под ним — падают
    /// и догорают на земле.
    /// </summary>
    private void SpawnDecoysBelow(Entity<DHDropshipComponent> ship)
    {
        var xform = Transform(ship);
        _flareEffects.AirBurst(_transform.GetMapCoordinates(ship.Owner, xform));
        EntityUid surface;
        if (ship.Comp.Level > 0 && ship.Comp.Surface is { } below && Exists(below))
        {
            surface = below;
        }
        else if (TryComp<DHPlanetAtmosphereMapComponent>(xform.MapUid, out var atmosphere) &&
                 _landing.GetSurface(atmosphere.System, atmosphere.Body) is { } fromSky)
        {
            surface = fromSky;
        }
        else
        {
            return;
        }

        var center = _transform.GetWorldPosition(xform);
        _flareEffects.FallBelow(new MapCoordinates(center, Transform(surface).MapID));
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
        var departing = new List<Entity<DHDropshipComponent>>();
        var flying = new List<Entity<DHDropshipComponent>>();
        while (ships.MoveNext(out var uid, out var comp))
        {
            if (comp.State == DHDropshipState.Departing && now >= comp.DepartAt)
                departing.Add((uid, comp));
            else if (comp.State == DHDropshipState.InFlight)
                flying.Add((uid, comp));
            else if (_hover.ContainsKey(uid))
                ClearHover(uid);

            if (comp.State == DHDropshipState.InFlight && now >= comp.NextShake)
            {
                comp.NextShake = now + TimeSpan.FromSeconds(2);
                _landing.Shake(uid, comp.Flight is DHDropshipFlight.Cruise or DHDropshipFlight.Atmosphere ? 0.15f : 0.3f);
            }
        }

        FollowHangars();

        foreach (var ship in departing)
        {
            Launch(ship);
        }

        foreach (var ship in flying)
        {
            if (TerminatingOrDeleted(ship))
                continue;

            UpdateFlight(ship, now, frameTime);
            UpdateHover(ship, now);
        }

        UpdateRamps(now);

        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;
        FollowCarriers();
        RefillFlares(now);

        var consoles = EntityQueryEnumerator<DHDropshipConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            if (_ui.IsUiOpen(uid, DHDropshipUiKey.Key) && ShipOf(uid) is { } ship)
                UpdateUi((uid, console), ship);
        }
    }

    /// <summary>Пристыкованный к авианосцу челнок понемногу пополняет ЛТЦ.</summary>
    private void RefillFlares(TimeSpan now)
    {
        var ships = EntityQueryEnumerator<DHDropshipComponent>();
        while (ships.MoveNext(out _, out var comp))
        {
            if (comp.Flares >= comp.MaxFlares || comp.State != DHDropshipState.Docked)
                continue;

            if (comp.NextFlareRefill <= TimeSpan.Zero)
                comp.NextFlareRefill = now + comp.FlareRefill;

            if (now < comp.NextFlareRefill)
                continue;

            comp.Flares++;
            comp.NextFlareRefill = comp.Flares < comp.MaxFlares ? now + comp.FlareRefill : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Челнок в ангаре — как ещё один уровень авианосца: каждый тик стоит точно на маркере стоянки, куда бы
    /// авианосец ни унесли прыжок, разрыв или спуск (и вместе с ним проходит через разрыв, а не догоняет потом).
    /// </summary>
    private void FollowHangars()
    {
        var moved = new List<(Entity<DHDropshipComponent> Ship, EntityUid Map, Vector2 Origin, Angle Rotation)>();
        var query = EntityQueryEnumerator<DHDropshipComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.State is not (DHDropshipState.Docked or DHDropshipState.Departing) ||
                comp.Hangar is not { } hangarUid || !TryComp<DHDropshipHangarComponent>(hangarUid, out var hangar) ||
                Transform(hangarUid).MapUid is not { } map)
            {
                continue;
            }

            var (origin, rotation) = HangarPlacement((hangarUid, hangar));
            if (xform.MapUid == map)
            {
                var (position, current) = _transform.GetWorldPositionRotation(xform);
                if ((position - origin).LengthSquared() <= 0.0001f && current.EqualsApprox(rotation, 0.0001))
                    continue;
            }

            moved.Add(((uid, comp), map, origin, rotation));
        }

        foreach (var (ship, map, origin, rotation) in moved)
        {
            var xform = Transform(ship);
            if (xform.MapUid == map)
                _transform.SetWorldPositionRotation(ship, origin, rotation, xform);
            else
                _transform.SetCoordinates(ship, xform, new EntityCoordinates(map, origin), rotation);

            if (TryComp<PhysicsComponent>(ship, out var physics))
            {
                _physics.SetLinearVelocity(ship, Vector2.Zero, body: physics);
                _physics.SetAngularVelocity(ship, 0f, body: physics);
            }

            SyncDecks(ship);
        }
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
            // В ангаре челнок каждый тик стоит на своём маркере (FollowHangars).
            if (comp.State != DHDropshipState.Docked || comp.Carrier is not { } carrier || !Exists(carrier) ||
                comp.Hangar != null && Exists(comp.Hangar))
            {
                continue;
            }

            if (TryComp<FTLComponent>(carrier, out var ftl) &&
                ftl.State is FTLState.Starting or FTLState.Travelling or FTLState.Arriving)
            {
                continue;
            }

            var home = comp.Hangar is { } hangar && Exists(hangar) ? Transform(hangar).MapUid : Transform(carrier).MapUid;
            if (xform.MapUid != home)
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
            DHDropshipState.InFlight when comp.Destination != null || comp.ToHangar => EstimateSeconds(ship),
            _ => 0,
        };

        string? destination = comp.State is DHDropshipState.Departing or DHDropshipState.InFlight &&
                              (comp.Destination != null || comp.ToHangar)
            ? DestinationName(comp)
            : null;

        // Фаза полёта: без причин зависания (решение автора: пилоту не сообщается, почему не садимся).
        string? phase = comp.State == DHDropshipState.InFlight
            ? Loc.GetString(comp.Flight switch
            {
                DHDropshipFlight.Transit => "dh-dropship-ui-phase-transit",
                DHDropshipFlight.Descend => "dh-dropship-ui-phase-descend",
                DHDropshipFlight.Climb => "dh-dropship-ui-phase-climb",
                _ when comp.Manual => "dh-dropship-ui-phase-manual",
                _ when comp.Waypoint == null => "dh-dropship-ui-phase-hover",
                _ => "dh-dropship-ui-phase-cruise",
            })
            : null;

        // В слое атмосферы земли не видно: пилоту — координаты точки под челноком (сверять с тактической картой).
        if (comp.State == DHDropshipState.InFlight && comp.Flight == DHDropshipFlight.Atmosphere)
        {
            var over = ShipCenter(ship);
            phase = Loc.GetString("dh-dropship-ui-phase-atmosphere", ("x", (int) MathF.Round(over.X)),
                ("y", (int) MathF.Round(over.Y)));
        }

        _ui.SetUiState(console.Owner, DHDropshipUiKey.Key, new DHDropshipConsoleState(Name(ship), comp.State, location,
            destination, Math.Max(0, seconds), destinations, comp.Flares, comp.MaxFlares, now < comp.FlaresUntil)
        {
            Phase = phase,
            Manual = comp.Manual,
            ManualAvailable = comp.State == DHDropshipState.InFlight &&
                              comp.Flight is DHDropshipFlight.Cruise or DHDropshipFlight.Atmosphere,
            InAtmosphere = comp.State == DHDropshipState.InFlight && comp.Flight == DHDropshipFlight.Atmosphere,
            AtmosphereAvailable = comp.State == DHDropshipState.InFlight && comp.Flight == DHDropshipFlight.Cruise,
            Retargetable = comp.State == DHDropshipState.InFlight && comp.Flight != DHDropshipFlight.Transit,
            RampAvailable = comp.State is DHDropshipState.Landed or DHDropshipState.Docked && comp.Underside != null &&
                            UndersideDeployed(comp),
            RampLowered = comp.RampLowered,
            ManualLevels = comp.Manual && comp.State == DHDropshipState.InFlight && comp.Flight == DHDropshipFlight.Cruise,
            Level = comp.Level,
        });
    }

    #endregion
}
