using System.Numerics;
using Content.Server._DarkestHour.Assault;
using Content.Server._DarkestHour.Shuttles.Rift;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Aviation;
using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.Examine;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared._DarkestHour.City;
using Content.Shared._DarkestHour.ControlPoints;
using Content.Shared._DarkestHour.HeavyGuns;
using Content.Shared._DarkestHour.Overwatch;
using Content.Shared._DarkestHour.Recon;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Server.Atmos.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.ControlPoints;

/// <summary>
/// Тактическая карта (консоль и планшет наблюдения): местность поверхности планеты базы (строится раз в несколько
/// минут, клиент кэширует по версии) и отметки стороны — зоны захвата, ЗП, РЛС, метки ЛЦУ и противник, которого
/// видят свои РЛС и включённые детекторы движения.
/// </summary>
public sealed partial class DHTacticalMapSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private DHLandingSystem _landing = default!;
    [Dependency] private Content.Server._DarkestHour.HeavyGuns.DHHeavyGunSystem _guns = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private Content.Shared._DarkestHour.Levels.DHZLevelSystem _zLevels = default!;
    [Dependency] private Content.Server._DarkestHour.Recon.DHConcealmentSystem _concealment = default!;

    /// <summary>Сколько видит своя РЛС по поверхности.</summary>
    public const float RadarRange = 60f;

    /// <summary>Сколько видит свой боец (в прямой видимости, сквозь стены — нет).</summary>
    public const float SightRange = 9f;

    /// <summary>Сколько видит экипаж своей техники (в прямой видимости).</summary>
    public const float VehicleSightRange = 13f;

    /// <summary>Замаскированного (видимость ниже этой) глаза бойцов не замечают.</summary>
    private const float SightStealthThreshold = 0.35f;

    private static readonly TimeSpan TerrainLifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Туман войны (решение автора 2026-11-19): пропавший из виду противник остаётся бледной отметкой «последний раз
    /// видели здесь» на минуту, а подсвеченный ЛЦУ — на 3 минуты после того, как лазер погас.
    /// </summary>
    private static readonly TimeSpan SightingMemory = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan LasedMemory = TimeSpan.FromMinutes(3);

    /// <summary>Противник считается подсвеченным, если стоит не дальше этого от своей метки ЛЦУ.</summary>
    private const float LaserLitRadius = 1.5f;

    private readonly Dictionary<(DHAssaultFaction Side, EntityUid Target), Sighting> _sightings = new();

    private sealed class Sighting
    {
        public EntityUid? Map;
        public Vector2 Position;
        public TimeSpan Seen;
        public TimeSpan? Lit;
        public DHTacticalMarkerKind Kind;
        public string Name = string.Empty;
        public DHAssaultFaction Owner;

        public TimeSpan Expires => Lit is { } lit && lit + LasedMemory > Seen + SightingMemory
            ? lit + LasedMemory
            : Seen + SightingMemory;
    }

    private readonly Dictionary<EntityUid, Terrain> _terrain = new();
    private int _version;

    private sealed class Terrain
    {
        public int Version;
        public TimeSpan Built;
        public DHTacticalMapTerrainEvent Event = default!;
    }

    /// <summary>Поверхность планеты базы «Штурма» (первая найденная).</summary>
    public EntityUid? GetSurfaceMap()
    {
        var bases = EntityQueryEnumerator<DHPlanetBaseComponent>();
        while (bases.MoveNext(out var planetBase))
        {
            if (_landing.GetSurface(planetBase.System, planetBase.Body) is { } surface)
                return surface;
        }

        return null;
    }

    /// <summary>Уровни тактической карты поверхности: все Z-уровни её стопки, а без стопки — только она.</summary>
    public List<(EntityUid Map, int Level)> Levels(EntityUid surface)
    {
        if (!_zLevels.TryGetLevel(surface, out var level))
            return new List<(EntityUid, int)> { (surface, 0) };

        return _zLevels.Levels(level.Stack);
    }

    /// <summary>Карта входит в стопку поверхности: её уровень.</summary>
    private static bool LevelOf(List<(EntityUid Map, int Level)> levels, EntityUid? map, out int level)
    {
        level = 0;
        foreach (var (levelMap, number) in levels)
        {
            if (levelMap != map)
                continue;

            level = number;
            return true;
        }

        return false;
    }

    /// <summary>Версия местности карты (строит при необходимости).</summary>
    public int TerrainVersion(EntityUid map)
    {
        return GetTerrain(map)?.Version ?? 0;
    }

    public void SendTerrain(EntityUid map, ICommonSession session)
    {
        if (GetTerrain(map) is not { } terrain)
        {
            Log.Warning($"Tactical map: no terrain for {ToPrettyString(map)} (requested by {session})");
            return;
        }

        var ev = terrain.Event;
        Log.Info($"Tactical map: sending {ev.Width}x{ev.Height} terrain v{ev.Version} ({ev.Data.Length} B) to {session}");
        RaiseNetworkEvent(ev, session);
    }

    private Terrain? GetTerrain(EntityUid map)
    {
        var now = _timing.CurTime;
        if (_terrain.TryGetValue(map, out var cached) && now - cached.Built < TerrainLifetime)
            return cached;

        // Этаж или подвал — в границах местности земли, чтобы уровни ложились друг на друга.
        DHTacticalMapTerrainEvent? built;
        if (_zLevels.TryGetLevel(map, out var zLevel) && zLevel.Level != 0)
        {
            if (_zLevels.LevelMap(zLevel.Stack, 0) is not { } ground || GetTerrain(ground) is not { } groundTerrain)
                return null;

            var g = groundTerrain.Event;
            built = Build(map, (g.Left, g.Bottom, g.Width, g.Height), zLevel.Level);
        }
        else
        {
            built = Build(map, null, 0);
        }

        if (built == null)
        {
            return null;
        }

        var terrain = new Terrain { Version = ++_version, Built = now, Event = built };
        built.Version = terrain.Version;
        _terrain[map] = terrain;
        return terrain;
    }

    /// <summary>Гриды карты: сама карта (поверхность-грид) и гриды на ней, со смещением в целых тайлах.</summary>
    private List<(EntityUid Uid, MapGridComponent Grid, Vector2i Offset)> MapGrids(EntityUid map)
    {
        var result = new List<(EntityUid, MapGridComponent, Vector2i)>();
        if (TryComp<MapGridComponent>(map, out var mapGrid))
            result.Add((map, mapGrid, Vector2i.Zero));

        var grids = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (grids.MoveNext(out var uid, out var grid, out var xform))
        {
            if (uid == map || xform.MapUid != map)
                continue;

            var pos = _transform.GetWorldPosition(xform);
            result.Add((uid, grid, new Vector2i((int) MathF.Round(pos.X), (int) MathF.Round(pos.Y))));
        }

        return result;
    }

    private DHTacticalMapTerrainEvent? Build(EntityUid map, (int Left, int Bottom, int Width, int Height)? bounds,
        int level)
    {
        var grids = MapGrids(map);
        int left, bottom, width, height;
        if (bounds is { } b)
        {
            (left, bottom, width, height) = b;
        }
        else
        {
            // Границы считаем по самим тайлам: у карты-грида LocalAABB может остаться нулевым (не пересчитывается).
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var (uid, grid, offset) in grids)
            {
                foreach (var tile in _map.GetAllTiles(uid, grid))
                {
                    var pos = tile.GridIndices + offset;
                    minX = Math.Min(minX, pos.X);
                    minY = Math.Min(minY, pos.Y);
                    maxX = Math.Max(maxX, pos.X);
                    maxY = Math.Max(maxY, pos.Y);
                }
            }

            left = minX;
            bottom = minY;
            width = maxX - minX + 1;
            height = maxY - minY + 1;
        }

        if (left == int.MaxValue || width <= 0 || height <= 0 || width > 1024 || height > 1024)
        {
            Log.Warning($"Tactical map: {ToPrettyString(map)} has bad tile bounds ({left}, {bottom}) {width}x{height}");
            return null;
        }

        var kinds = new byte[width * height];
        TryComp<DHMappedSurfaceComponent>(map, out var surface);

        var gridOffsets = new Dictionary<EntityUid, (MapGridComponent Grid, Vector2i Offset)>();
        foreach (var (uid, grid, offset) in grids)
        {
            gridOffsets[uid] = (grid, offset);
            foreach (var tile in _map.GetAllTiles(uid, grid))
            {
                var def = (ContentTileDefinition) _tileDef[tile.Tile.TypeId];
                Set(kinds, width, height, left, bottom, tile.GridIndices + offset, Classify(def, surface));
            }
        }

        // Стены и скалы — непроницаемые закреплённые сущности; вода и окопы — по метке DHTacticalTerrain.
        var walls = EntityQueryEnumerator<AirtightComponent, TransformComponent, MetaDataComponent>();
        while (walls.MoveNext(out _, out _, out var xform, out var meta))
        {
            if (xform.GridUid is not { } gridUid || !xform.Anchored || !gridOffsets.TryGetValue(gridUid, out var g))
                continue;

            var kind = meta.EntityPrototype?.ID.Contains("Rock") == true ? DHTacticalTerrain.Rock : DHTacticalTerrain.Wall;
            Set(kinds, width, height, left, bottom, _map.TileIndicesFor(gridUid, g.Grid, xform.Coordinates) + g.Offset, kind);
        }

        var water = EntityQueryEnumerator<DHTacticalTerrainComponent, TransformComponent>();
        while (water.MoveNext(out _, out var terrain, out var xform))
        {
            if (xform.GridUid is { } gridUid && gridOffsets.TryGetValue(gridUid, out var g))
                Set(kinds, width, height, left, bottom, _map.TileIndicesFor(gridUid, g.Grid, xform.Coordinates) + g.Offset, terrain.Kind);
        }

        return new DHTacticalMapTerrainEvent
        {
            Map = GetNetEntity(map),
            Level = level,
            Left = left,
            Bottom = bottom,
            Width = width,
            Height = height,
            Data = Encode(kinds),
        };
    }

    private static void Set(byte[] kinds, int width, int height, int left, int bottom, Vector2i tile,
        DHTacticalTerrain kind)
    {
        var x = tile.X - left;
        var y = tile.Y - bottom;
        if (x >= 0 && y >= 0 && x < width && y < height)
            kinds[y * width + x] = (byte) kind;
    }

    private static DHTacticalTerrain Classify(ContentTileDefinition def, DHMappedSurfaceComponent? surface)
    {
        var id = def.ID;
        if (id == "Space")
            return DHTacticalTerrain.Empty;

        if (id.Contains("Asphalt") || id.Contains("Road"))
            return DHTacticalTerrain.Road;

        if (id.Contains("Grass"))
            return DHTacticalTerrain.Grass;

        if (surface != null && (id == surface.GroundTile.Id || surface.GroundTiles.Contains(id)) ||
            id.Contains("Sand") || id.Contains("Desert") || id.Contains("Dirt") || id.Contains("Grass"))
        {
            return DHTacticalTerrain.Ground;
        }

        return DHTacticalTerrain.Floor;
    }

    /// <summary>RLE: пары «сколько (1–255), что».</summary>
    private static byte[] Encode(byte[] kinds)
    {
        var result = new List<byte>(kinds.Length / 8);
        var i = 0;
        while (i < kinds.Length)
        {
            var value = kinds[i];
            var run = 1;
            while (i + run < kinds.Length && kinds[i + run] == value && run < 255)
            {
                run++;
            }

            result.Add((byte) run);
            result.Add(value);
            i += run;
        }

        return result.ToArray();
    }

    /// <summary>Отметки стороны на карте поверхности и её Z-уровнях (у каждой — свой уровень).</summary>
    public List<DHTacticalMarker> Markers(DHAssaultFaction faction, EntityUid map)
    {
        var markers = new List<DHTacticalMarker>();
        var levels = Levels(map);

        var points = EntityQueryEnumerator<DHControlPointComponent, TransformComponent>();
        while (points.MoveNext(out _, out var point, out var xform))
        {
            if (!LevelOf(levels, xform.MapUid, out var level))
                continue;

            markers.Add(Marker(DHTacticalMarkerKind.ControlPoint, xform, Loc.GetString(point.PointName), point.Holder,
                point.Radius, level));
        }

        var zones = EntityQueryEnumerator<Content.Shared._DarkestHour.Assault.DHLandingZoneComponent, TransformComponent>();
        while (zones.MoveNext(out _, out var zone, out var xform))
        {
            if (LevelOf(levels, xform.MapUid, out var level))
                markers.Add(Marker(DHTacticalMarkerKind.LandingZone, xform, zone.ZoneName, null, 0f, level));
        }

        // Свои глаза: РЛС (радиус RadarRange) и включённые детекторы движения у своих бойцов.
        var eyes = new List<(Vector2 Position, float Range)>();
        // Глаза видят сквозь уровни (РЛС — всю стопку): по XY.
        var radars = EntityQueryEnumerator<DHRadarComponent, TransformComponent>();
        while (radars.MoveNext(out var uid, out var radar, out var xform))
        {
            if (!LevelOf(levels, xform.MapUid, out var level) || radar.Faction != faction)
                continue;

            var working = _guns.RadarWorking(uid, radar);
            markers.Add(Marker(DHTacticalMarkerKind.Radar, xform, Name(uid), faction, working ? RadarRange : 0f,
                level));
            if (working)
                eyes.Add((_transform.GetWorldPosition(xform), RadarRange));
        }

        // Детектор движения видит только движущихся (как и на экране у владельца), а не всех в радиусе.
        var motionEyes = new List<(Vector2 Position, float Range, float MinSpeed)>();
        var detectors = EntityQueryEnumerator<DHMotionDetectorComponent, TransformComponent>();
        while (detectors.MoveNext(out _, out var detector, out var xform))
        {
            if (!detector.Active || !LevelOf(levels, xform.MapUid, out _) ||
                !TryComp<DHAssaultFighterComponent>(xform.ParentUid, out var holder) || holder.Faction != faction)
            {
                continue;
            }

            motionEyes.Add((_transform.GetWorldPosition(xform), detector.Range, detector.MinSpeed));
        }

        // Глаза бойцов и экипажей: противник в прямой видимости своих (на том же уровне).
        var sight = new List<(EntityUid Eye, EntityUid? Map, Vector2 Position, float Range)>();
        var fighters = EntityQueryEnumerator<DHAssaultFighterComponent, TransformComponent>();
        while (fighters.MoveNext(out var uid, out var fighter, out var xform))
        {
            if (fighter.Faction == faction && LevelOf(levels, xform.MapUid, out _) && _mobState.IsAlive(uid))
                sight.Add((uid, xform.MapUid, _transform.GetWorldPosition(xform), SightRange));
        }

        var ownVehicles = EntityQueryEnumerator<DHVehicleComponent, TransformComponent>();
        while (ownVehicles.MoveNext(out var uid, out var vehicle, out var xform))
        {
            if (vehicle.Faction != faction || !LevelOf(levels, xform.MapUid, out var ownLevel))
                continue;

            var ownPosition = _transform.GetWorldPosition(xform);
            sight.Add((uid, xform.MapUid, ownPosition, VehicleSightRange));

            // Своя техника — значком по типу (план осады п. 9).
            markers.Add(new DHTacticalMarker
            {
                Kind = DHTacticalMarkerKind.OwnVehicle,
                X = ownPosition.X,
                Y = ownPosition.Y,
                Name = Name(uid),
                Owner = faction,
                Level = ownLevel,
                Icon = (byte) vehicle.MapIcon,
            });
        }

        // Свои метки ЛЦУ: противник рядом с ними подсвечен (виден и запоминается надолго).
        var lasers = new List<(EntityUid? Map, Vector2 Position)>();
        var lasing = EntityQueryEnumerator<DHLaserMarkComponent, TransformComponent>();
        while (lasing.MoveNext(out _, out var laser, out var xform))
        {
            if (laser.Faction == faction)
                lasers.Add((xform.MapUid, _transform.GetWorldPosition(xform)));
        }

        var now = _timing.CurTime;
        var seenNow = new HashSet<EntityUid>();

        var enemies = EntityQueryEnumerator<DHAssaultFighterComponent, TransformComponent>();
        while (enemies.MoveNext(out var uid, out var fighter, out var xform))
        {
            if (fighter.Faction == faction || !LevelOf(levels, xform.MapUid, out var level) || !_mobState.IsAlive(uid))
                continue;

            var position = _transform.GetWorldPosition(xform);
            var hidden = HasComp<StealthComponent>(uid) && _stealth.GetVisibility(uid) < SightStealthThreshold;
            var lit = Lit(lasers, xform.MapUid, position);
            if (!lit && !SeenByEyes(eyes, position) && !SeenByMotion(motionEyes, uid, position) &&
                (hidden || !SeenBySight(sight, uid, xform.MapUid, position)))
            {
                continue;
            }

            seenNow.Add(uid);
            Remember(faction, uid, xform.MapUid, position, now, lit, DHTacticalMarkerKind.Enemy, string.Empty,
                fighter.Faction);
            markers.Add(new DHTacticalMarker
            {
                Kind = DHTacticalMarkerKind.Enemy,
                X = position.X,
                Y = position.Y,
                Owner = fighter.Faction,
                Level = level,
                Lased = lit,
            });
        }

        var enemyVehicles = EntityQueryEnumerator<DHVehicleComponent, TransformComponent>();
        while (enemyVehicles.MoveNext(out var uid, out var vehicle, out var xform))
        {
            if (vehicle.Faction == faction || !LevelOf(levels, xform.MapUid, out var level))
                continue;

            var position = _transform.GetWorldPosition(xform);
            var moving = TryComp<DHGridDriveComponent>(uid, out var drive) && drive.IsMoving;
            // Под маскировочной сетью стоящую технику РЛС не берёт (решение автора 2026-11-19).
            var camo = HasComp<DHCamoHiddenComponent>(uid);
            var lit = Lit(lasers, xform.MapUid, position);
            if (!lit && !(!camo && SeenByEyes(eyes, position)) && !(moving && SeenByMotionAt(motionEyes, position)) &&
                !SeenBySight(sight, uid, xform.MapUid, position, VehicleSightRange - SightRange))
            {
                continue;
            }

            seenNow.Add(uid);
            Remember(faction, uid, xform.MapUid, position, now, lit, DHTacticalMarkerKind.EnemyVehicle, Name(uid),
                vehicle.Faction);
            markers.Add(new DHTacticalMarker
            {
                Kind = DHTacticalMarkerKind.EnemyVehicle,
                X = position.X,
                Y = position.Y,
                Name = Name(uid),
                Owner = vehicle.Faction,
                Level = level,
                Lased = lit,
            });
        }

        AddSightings(faction, levels, seenNow, now, markers);

        // Челноки десанта над полем боя (решение автора 2026-11-19): Империи — свои всегда, Республике — если их видят
        // РЛС или детекторы. Рисуются на всех уровнях.
        var dropships = EntityQueryEnumerator<Content.Server._DarkestHour.Assault.DHDropshipComponent, TransformComponent>();
        while (dropships.MoveNext(out var uid, out var ship, out var xform))
        {
            // В слое атмосферы (заход на выброску) — над той землёй, над которой идёт.
            var over = ship.Flight == DHDropshipFlight.Atmosphere ? ship.Surface : xform.MapUid;
            if (ship.State != DHDropshipState.InFlight || !LevelOf(levels, over, out _))
                continue;

            var position = _transform.GetWorldPosition(xform);
            if (TryComp<PhysicsComponent>(uid, out var shipPhysics))
                position += _transform.GetWorldRotation(xform).RotateVec(shipPhysics.LocalCenter);

            if (faction != DHAssaultFaction.Empire && !SeenByEyes(eyes, position))
                continue;

            markers.Add(new DHTacticalMarker
            {
                Kind = DHTacticalMarkerKind.Dropship,
                X = position.X,
                Y = position.Y,
                Name = Name(uid),
                Owner = DHAssaultFaction.Empire,
            });
        }

        // Свой штурмовик в воздухе — с курсом (только у Империи).
        if (faction == DHAssaultFaction.Empire)
            AddAircraft(map, levels, markers);

        // Контрбатарейные засечки стороны.
        foreach (var battery in _concealment.Marks)
        {
            if (battery.Detector != faction || !LevelOf(levels, battery.Map, out var batteryLevel))
                continue;

            markers.Add(new DHTacticalMarker
            {
                Kind = DHTacticalMarkerKind.CounterBattery,
                X = battery.Center.X,
                Y = battery.Center.Y,
                Name = Loc.GetString("dh-tacmap-counter-battery"),
                Owner = faction == DHAssaultFaction.Empire ? DHAssaultFaction.Republic : DHAssaultFaction.Empire,
                Radius = Content.Server._DarkestHour.Recon.DHConcealmentSystem.CounterBatteryRadius,
                Level = batteryLevel,
            });
        }

        var marks = EntityQueryEnumerator<DHLaserMarkComponent, TransformComponent>();
        while (marks.MoveNext(out _, out var mark, out var xform))
        {
            if (!LevelOf(levels, xform.MapUid, out var level) || mark.Faction is { } markFaction && markFaction != faction)
                continue;

            markers.Add(Marker(DHTacticalMarkerKind.LaserMark, xform,
                Loc.GetString("dh-tacmap-laser-mark", ("code", mark.Code)), faction, 0f, level));
        }

        markers.AddRange(Beacons(map));

        var contacts = EntityQueryEnumerator<DHScanContactComponent, TransformComponent>();
        while (contacts.MoveNext(out _, out var contact, out var xform))
        {
            if (!LevelOf(levels, xform.MapUid, out var level) || contact.Faction != faction)
                continue;

            markers.Add(Marker(contact.Blip ? DHTacticalMarkerKind.ScanBlip : DHTacticalMarkerKind.ScanContact, xform,
                contact.Label, null, 0f, level));
        }

        // РЛС штурмовика в воздухе: сигнатуры техники и пехоты (только у его стороны).
        if (faction == DHAssaultFaction.Empire)
        {
            var aircraft = EntityQueryEnumerator<DHAircraftComponent>();
            while (aircraft.MoveNext(out var plane))
            {
                if (plane.State is not (DHAircraftState.Airborne or DHAircraftState.Returning or DHAircraftState.Pass) ||
                    !plane.Modules.Contains(DHAircraftModule.Radar))
                {
                    continue;
                }

                foreach (var signature in plane.Signatures)
                {
                    markers.Add(new DHTacticalMarker
                    {
                        Kind = DHTacticalMarkerKind.AirSignature,
                        X = signature.Position.X,
                        Y = signature.Position.Y,
                        Name = Loc.GetString("dh-aircraft-signature-" + signature.Kind.ToString().ToLowerInvariant()),
                        Owner = DHAssaultFaction.Republic,
                    });
                }
            }
        }

        return markers;
    }

    private static bool Lit(List<(EntityUid? Map, Vector2 Position)> lasers, EntityUid? map, Vector2 position)
    {
        foreach (var (laserMap, laser) in lasers)
        {
            if (laserMap == map && Vector2.Distance(laser, position) <= LaserLitRadius)
                return true;
        }

        return false;
    }

    private void Remember(DHAssaultFaction side, EntityUid target, EntityUid? map, Vector2 position, TimeSpan now,
        bool lit, DHTacticalMarkerKind kind, string name, DHAssaultFaction owner)
    {
        if (!_sightings.TryGetValue((side, target), out var sighting))
        {
            sighting = new Sighting();
            _sightings[(side, target)] = sighting;
        }

        sighting.Map = map;
        sighting.Position = position;
        sighting.Seen = now;
        sighting.Kind = kind;
        sighting.Name = name;
        sighting.Owner = owner;
        if (lit)
            sighting.Lit = now;
    }

    /// <summary>Бледные отметки противника, которого сейчас не видно, но видели недавно.</summary>
    private void AddSightings(DHAssaultFaction faction, List<(EntityUid Map, int Level)> levels,
        HashSet<EntityUid> seenNow, TimeSpan now, List<DHTacticalMarker> markers)
    {
        var forget = new List<(DHAssaultFaction, EntityUid)>();
        foreach (var (key, sighting) in _sightings)
        {
            var (side, target) = key;
            if (sighting.Expires <= now || TerminatingOrDeleted(target) ||
                HasComp<Content.Shared.Mobs.Components.MobStateComponent>(target) && !_mobState.IsAlive(target))
            {
                forget.Add(key);
                continue;
            }

            if (side != faction || seenNow.Contains(target) || !LevelOf(levels, sighting.Map, out var level))
                continue;

            markers.Add(new DHTacticalMarker
            {
                Kind = sighting.Kind,
                X = sighting.Position.X,
                Y = sighting.Position.Y,
                Name = sighting.Name,
                Owner = sighting.Owner,
                Level = level,
                Stale = true,
                Lased = sighting.Lit is { } lit && lit + LasedMemory > now,
            });
        }

        foreach (var key in forget)
        {
            _sightings.Remove(key);
        }
    }

    /// <summary>Свой штурмовик в небе над этой поверхностью или на бреющем над её уровнями.</summary>
    private void AddAircraft(EntityUid map, List<(EntityUid Map, int Level)> levels, List<DHTacticalMarker> markers)
    {
        EntityUid? sky = null;
        if (TryComp<DHPlanetSurfaceMapComponent>(map, out var surface))
            sky = _landing.GetAtmosphere(surface.System, surface.Body);

        var aircraft = EntityQueryEnumerator<DHAircraftComponent, TransformComponent>();
        while (aircraft.MoveNext(out var uid, out var plane, out var xform))
        {
            if (plane.State is not (DHAircraftState.Airborne or DHAircraftState.Returning or DHAircraftState.Pass) ||
                xform.MapUid != sky && !LevelOf(levels, xform.MapUid, out _))
            {
                continue;
            }

            var position = _transform.GetWorldPosition(xform);
            var heading = TryComp<PhysicsComponent>(uid, out var physics) && physics.LinearVelocity.LengthSquared() > 0.01f
                ? Vector2.Normalize(physics.LinearVelocity)
                : _transform.GetWorldRotation(xform).ToWorldVec();

            markers.Add(new DHTacticalMarker
            {
                Kind = DHTacticalMarkerKind.Aircraft,
                X = position.X,
                Y = position.Y,
                Name = Name(uid),
                Owner = DHAssaultFaction.Empire,
                Rotation = MathF.Atan2(heading.Y, heading.X),
            });
        }
    }

    /// <summary>Точку видят РЛС или включённые детекторы движения (сквозь стены и уровни).</summary>
    private static bool SeenByEyes(List<(Vector2 Position, float Range)> eyes, Vector2 position)
    {
        foreach (var (eye, range) in eyes)
        {
            if (Vector2.Distance(eye, position) <= range)
                return true;
        }

        return false;
    }

    /// <summary>Детектор движения: в его радиусе и движется.</summary>
    private bool SeenByMotion(List<(Vector2 Position, float Range, float MinSpeed)> detectors, EntityUid target,
        Vector2 position)
    {
        if (detectors.Count == 0 || !TryComp<PhysicsComponent>(target, out var physics))
            return false;

        var speed = physics.LinearVelocity.Length();
        foreach (var (eye, range, minSpeed) in detectors)
        {
            if (speed >= minSpeed && Vector2.Distance(eye, position) <= range)
                return true;
        }

        return false;
    }

    private static bool SeenByMotionAt(List<(Vector2 Position, float Range, float MinSpeed)> detectors, Vector2 position)
    {
        foreach (var (eye, range, _) in detectors)
        {
            if (Vector2.Distance(eye, position) <= range)
                return true;
        }

        return false;
    }

    /// <summary>Кого-то видит свой боец или экипаж: тот же уровень, в дальности и в прямой видимости.</summary>
    private bool SeenBySight(List<(EntityUid Eye, EntityUid? Map, Vector2 Position, float Range)> sight, EntityUid target,
        EntityUid? map, Vector2 position, float bonus = 0f)
    {
        foreach (var (eye, eyeMap, eyePosition, range) in sight)
        {
            var reach = range + bonus;
            if (eyeMap != map || Vector2.Distance(eyePosition, position) > reach)
                continue;

            if (_examine.InRangeUnOccluded(eye, target, reach, e => e == eye || e == target))
                return true;
        }

        return false;
    }

    /// <summary>Городские ориентиры (DHMapBeacon) на карте поверхности и её уровнях — видны всем.</summary>
    public List<DHTacticalMarker> Beacons(EntityUid map)
    {
        var markers = new List<DHTacticalMarker>();
        var levels = Levels(map);
        var beacons = EntityQueryEnumerator<DHMapBeaconComponent, TransformComponent>();
        while (beacons.MoveNext(out _, out var beacon, out var xform))
        {
            if (LevelOf(levels, xform.MapUid, out var level))
                markers.Add(Marker(DHTacticalMarkerKind.Beacon, xform, Loc.GetString(beacon.BeaconName), null, 0f, level));
        }

        return markers;
    }

    private DHTacticalMarker Marker(DHTacticalMarkerKind kind, TransformComponent xform, string name,
        DHAssaultFaction? owner, float radius, int level)
    {
        var position = _transform.GetWorldPosition(xform);
        return new DHTacticalMarker
        {
            Kind = kind,
            X = position.X,
            Y = position.Y,
            Name = name,
            Owner = owner,
            Radius = radius,
            Level = level,
        };
    }
}
