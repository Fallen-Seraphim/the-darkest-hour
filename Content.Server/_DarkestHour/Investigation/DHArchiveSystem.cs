using Content.Server.GameTicking;
using Content.Server.Power.Components;
using Content.Shared._DarkestHour.Investigation;
using Content.Shared.Access.Systems;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Mobs;
using Content.Shared.Station.Systems;
using Content.Shared.SurveillanceCamera.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Investigation;

/// <summary>
/// Память камер и история мониторинга: камеры станции запоминают, кого видели (по видимой личности — маска и
/// противогаз скрывают лицо) и кто упал или погиб у них на виду; датчики костюмов пишут крит и смерть (с
/// координатами в режиме координат). Журнал станции смотрят на компьютере архива наблюдения.
/// </summary>
public sealed partial class DHArchiveSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private ServerGameTicker _gameTicker = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const float CameraRange = 6f;
    private const int MaxEntries = 400;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SightingCooldown = TimeSpan.FromMinutes(2);

    /// <summary>Журнал каждой станции (ключ — станция; null — вне станций).</summary>
    private readonly Dictionary<EntityUid, List<DHArchiveEntry>> _logs = new();

    /// <summary>Когда камера последний раз записала этого человека.</summary>
    private readonly Dictionary<(EntityUid Camera, EntityUid Mob), TimeSpan> _lastSeen = new();

    private readonly HashSet<Entity<HumanoidProfileComponent>> _nearby = new();
    private TimeSpan _nextScan;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);

        Subs.BuiEvents<DHArchiveConsoleComponent>(DHArchiveUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<DHArchiveRefreshMessage>(OnRefresh);
        });
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _logs.Clear();
        _lastSeen.Clear();
        _nextScan = TimeSpan.Zero;
    }

    #region Запись

    private void Add(EntityUid source, DHArchiveKind kind, string sourceName, string text)
    {
        var station = _station.GetOwningStation(source);
        if (station == null)
            return;

        if (!_logs.TryGetValue(station.Value, out var log))
            _logs[station.Value] = log = new List<DHArchiveEntry>();

        log.Add(new DHArchiveEntry(_gameTicker.RoundDuration(), kind, sourceName, text));
        if (log.Count > MaxEntries)
            log.RemoveRange(0, log.Count - MaxEntries);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextScan)
            return;
        _nextScan = now + ScanInterval;

        var query = EntityQueryEnumerator<SurveillanceCameraComponent, TransformComponent>();
        while (query.MoveNext(out var camera, out var cam, out var xform))
        {
            if (!CameraWorks(camera, cam))
                continue;

            _nearby.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, CameraRange, _nearby);
            foreach (var mob in _nearby)
            {
                if (!CameraSees(camera, xform, mob))
                    continue;

                var key = (camera, mob.Owner);
                if (_lastSeen.TryGetValue(key, out var last) && now - last < SightingCooldown)
                    continue;

                _lastSeen[key] = now;
                Add(camera, DHArchiveKind.Camera, cam.CameraId,
                    Loc.GetString("dh-archive-seen", ("name", Identity.Name(mob, EntityManager))));
            }
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState is not (MobState.Critical or MobState.Dead) || !HasComp<HumanoidProfileComponent>(args.Target))
            return;

        var dead = args.NewMobState == MobState.Dead;

        // камеры, которые это видели
        var query = EntityQueryEnumerator<SurveillanceCameraComponent, TransformComponent>();
        var targetCoords = _transform.GetMapCoordinates(args.Target);
        while (query.MoveNext(out var camera, out var cam, out var xform))
        {
            if (!CameraWorks(camera, cam) || _transform.GetMapCoordinates(camera, xform).MapId != targetCoords.MapId ||
                !CameraSees(camera, xform, args.Target))
            {
                continue;
            }

            Add(camera, DHArchiveKind.Camera, cam.CameraId, Loc.GetString(dead ? "dh-archive-seen-dead" : "dh-archive-seen-crit",
                ("name", Identity.Name(args.Target, EntityManager))));
        }

        // датчики костюма
        if (!_inventory.TryGetSlotEntity(args.Target, "jumpsuit", out var suit) ||
            !TryComp<SuitSensorComponent>(suit, out var sensor) || sensor.Mode == SuitSensorMode.SensorOff)
        {
            return;
        }

        var name = _idCard.TryFindIdCard(args.Target, out var id) && id.Comp.FullName is { } full
            ? full
            : Loc.GetString("dh-archive-unknown");

        var text = Loc.GetString(dead ? "dh-archive-sensor-dead" : "dh-archive-sensor-crit", ("name", name));
        if (sensor.Mode == SuitSensorMode.SensorCords)
        {
            var pos = _transform.GetWorldPosition(args.Target);
            text += " " + Loc.GetString("dh-archive-sensor-coords", ("x", (int) pos.X), ("y", (int) pos.Y));
        }

        Add(args.Target, DHArchiveKind.Sensors, Loc.GetString("dh-archive-source-sensors"), text);
    }

    private bool CameraWorks(EntityUid camera, SurveillanceCameraComponent cam)
    {
        if (!cam.Active)
            return false;

        return !TryComp<ApcPowerReceiverComponent>(camera, out var power) || power.Powered;
    }

    /// <summary>Видит ли камера цель: в радиусе и без преград (стену, на которой висит камера, не считаем).</summary>
    private bool CameraSees(EntityUid camera, TransformComponent xform, EntityUid target)
    {
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return _examine.InRangeUnOccluded(camera, target, CameraRange);

        var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        return _examine.InRangeUnOccluded(camera, target, CameraRange, e =>
        {
            var ex = Transform(e);
            return ex.Anchored && ex.GridUid == grid && _map.TileIndicesFor(grid, gridComp, ex.Coordinates) == tile;
        });
    }

    #endregion

    #region Интерфейс

    private void OnUiOpened(Entity<DHArchiveConsoleComponent> ent, ref BoundUIOpenedEvent args) => UpdateUi(ent);

    private void OnRefresh(Entity<DHArchiveConsoleComponent> ent, ref DHArchiveRefreshMessage args) => UpdateUi(ent);

    private void UpdateUi(Entity<DHArchiveConsoleComponent> ent)
    {
        var entries = new List<DHArchiveEntry>();
        if (_station.GetOwningStation(ent) is { } station && _logs.TryGetValue(station, out var log))
            entries.AddRange(log);

        _ui.SetUiState(ent.Owner, DHArchiveUiKey.Key, new DHArchiveBoundUserInterfaceState(entries));
    }

    #endregion
}
