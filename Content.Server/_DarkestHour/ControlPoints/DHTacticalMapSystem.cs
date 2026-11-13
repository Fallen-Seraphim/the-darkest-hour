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
using Content.Server.Atmos.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
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
    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private Content.Shared._DarkestHour.Levels.DHZLevelSystem _zLevels = default!;

    /// <summary>Сколько видит своя РЛС по поверхности.</summary>
    public const float RadarRange = 60f;

    /// <summary>Сколько видит свой боец (в прямой видимости, сквозь стены — нет).</summary>
    public const float SightRange = 9f;

    /// <summary>Сколько видит экипаж своей техники (в прямой видимости).</summary>
    public const float VehicleSightRange = 13f;

    /// <summary>Замаскированного (видимость ниже этой) глаза бойцов не замечают.</summary>
    private const float SightStealthThreshold = 0.35f;

    private static readonly TimeSpan TerrainLifetime = TimeSpan.FromMinutes(5);

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

            markers.Add(Marker(DHTacticalMarkerKind.Radar, xform, Name(uid), faction, radar.Active ? RadarRange : 0f,
                level));
            if (radar.Active)
                eyes.Add((_transform.GetWorldPosition(xform), RadarRange));
        }

        var detectors = EntityQueryEnumerator<DHMotionDetectorComponent, TransformComponent>();
        while (detectors.MoveNext(out _, out var detector, out var xform))
        {
            if (!detector.Active || !LevelOf(levels, xform.MapUid, out _) ||
                !TryComp<DHAssaultFighterComponent>(xform.ParentUid, out var holder) || holder.Faction != faction)
            {
                continue;
            }

            eyes.Add((_transform.GetWorldPosition(xform), detector.Range));
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
            if (vehicle.Faction == faction && LevelOf(levels, xform.MapUid, out _))
                sight.Add((uid, xform.MapUid, _transform.GetWorldPosition(xform), VehicleSightRange));
        }

        var enemies = EntityQueryEnumerator<DHAssaultFighterComponent, TransformComponent>();
        while (enemies.MoveNext(out var uid, out var fighter, out var xform))
        {
            if (fighter.Faction == faction || !LevelOf(levels, xform.MapUid, out var level) || !_mobState.IsAlive(uid))
                continue;

            var position = _transform.GetWorldPosition(xform);
            var hidden = HasComp<StealthComponent>(uid) && _stealth.GetVisibility(uid) < SightStealthThreshold;
            if (!SeenByEyes(eyes, position) && (hidden || !SeenBySight(sight, uid, xform.MapUid, position)))
                continue;

            markers.Add(new DHTacticalMarker
            {
                Kind = DHTacticalMarkerKind.Enemy,
                X = position.X,
                Y = position.Y,
                Owner = fighter.Faction,
                Level = level,
            });
        }

        var enemyVehicles = EntityQueryEnumerator<DHVehicleComponent, TransformComponent>();
        while (enemyVehicles.MoveNext(out var uid, out var vehicle, out var xform))
        {
            if (vehicle.Faction == faction || !LevelOf(levels, xform.MapUid, out var level))
                continue;

            var position = _transform.GetWorldPosition(xform);
            if (!SeenByEyes(eyes, position) && !SeenBySight(sight, uid, xform.MapUid, position, VehicleSightRange - SightRange))
                continue;

            markers.Add(new DHTacticalMarker
            {
                Kind = DHTacticalMarkerKind.EnemyVehicle,
                X = position.X,
                Y = position.Y,
                Name = Name(uid),
                Owner = vehicle.Faction,
                Level = level,
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
