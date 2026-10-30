using System.Numerics;
using Content.Server._DarkestHour.Assault;
using Content.Server._DarkestHour.Shuttles.Rift;
using Content.Shared._DarkestHour.Assault;
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
    [Dependency] private DHLandingSystem _landing = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>Сколько видит своя РЛС по поверхности.</summary>
    public const float RadarRange = 60f;

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

    /// <summary>Версия местности карты (строит при необходимости).</summary>
    public int TerrainVersion(EntityUid map)
    {
        return GetTerrain(map)?.Version ?? 0;
    }

    public void SendTerrain(EntityUid map, ICommonSession session)
    {
        if (GetTerrain(map) is { } terrain)
            RaiseNetworkEvent(terrain.Event, session);
    }

    private Terrain? GetTerrain(EntityUid map)
    {
        var now = _timing.CurTime;
        if (_terrain.TryGetValue(map, out var cached) && now - cached.Built < TerrainLifetime)
            return cached;

        if (!TryComp<MapGridComponent>(map, out var grid))
            return null;

        var built = Build(map, grid);
        if (built == null)
            return null;

        var terrain = new Terrain { Version = ++_version, Built = now, Event = built };
        built.Version = terrain.Version;
        _terrain[map] = terrain;
        return terrain;
    }

    private DHTacticalMapTerrainEvent? Build(EntityUid map, MapGridComponent grid)
    {
        var bounds = grid.LocalAABB;
        var left = (int) MathF.Floor(bounds.Left);
        var bottom = (int) MathF.Floor(bounds.Bottom);
        var width = (int) MathF.Ceiling(bounds.Right) - left;
        var height = (int) MathF.Ceiling(bounds.Top) - bottom;
        if (width <= 0 || height <= 0 || width > 1024 || height > 1024)
            return null;

        var kinds = new byte[width * height];
        TryComp<DHMappedSurfaceComponent>(map, out var surface);

        foreach (var tile in _map.GetAllTiles(map, grid))
        {
            var x = tile.GridIndices.X - left;
            var y = tile.GridIndices.Y - bottom;
            if (x < 0 || y < 0 || x >= width || y >= height)
                continue;

            var def = (ContentTileDefinition) _tileDef[tile.Tile.TypeId];
            kinds[y * width + x] = (byte) Classify(def, surface);
        }

        // Стены и скалы — непроницаемые закреплённые сущности; вода и окопы — по метке DHTacticalTerrain.
        var walls = EntityQueryEnumerator<AirtightComponent, TransformComponent, MetaDataComponent>();
        while (walls.MoveNext(out _, out _, out var xform, out var meta))
        {
            if (xform.GridUid != map || !xform.Anchored)
                continue;

            var kind = meta.EntityPrototype?.ID.Contains("Rock") == true ? DHTacticalTerrain.Rock : DHTacticalTerrain.Wall;
            Set(kinds, width, height, left, bottom, xform, map, grid, kind);
        }

        var water = EntityQueryEnumerator<DHTacticalTerrainComponent, TransformComponent>();
        while (water.MoveNext(out _, out var terrain, out var xform))
        {
            if (xform.GridUid == map)
                Set(kinds, width, height, left, bottom, xform, map, grid, terrain.Kind);
        }

        return new DHTacticalMapTerrainEvent
        {
            Map = GetNetEntity(map),
            Left = left,
            Bottom = bottom,
            Width = width,
            Height = height,
            Data = Encode(kinds),
        };
    }

    private void Set(byte[] kinds, int width, int height, int left, int bottom, TransformComponent xform,
        EntityUid map, MapGridComponent grid, DHTacticalTerrain kind)
    {
        var tile = _map.TileIndicesFor(map, grid, xform.Coordinates);
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

    /// <summary>Отметки стороны на карте поверхности.</summary>
    public List<DHTacticalMarker> Markers(DHAssaultFaction faction, EntityUid map)
    {
        var markers = new List<DHTacticalMarker>();

        var points = EntityQueryEnumerator<DHControlPointComponent, TransformComponent>();
        while (points.MoveNext(out _, out var point, out var xform))
        {
            if (xform.MapUid != map)
                continue;

            markers.Add(Marker(DHTacticalMarkerKind.ControlPoint, xform, Loc.GetString(point.PointName), point.Holder,
                point.Radius));
        }

        var zones = EntityQueryEnumerator<Content.Shared._DarkestHour.Assault.DHLandingZoneComponent, TransformComponent>();
        while (zones.MoveNext(out _, out var zone, out var xform))
        {
            if (xform.MapUid == map)
                markers.Add(Marker(DHTacticalMarkerKind.LandingZone, xform, zone.ZoneName, null, 0f));
        }

        // Свои глаза: РЛС (радиус RadarRange) и включённые детекторы движения у своих бойцов.
        var eyes = new List<(Vector2 Position, float Range)>();
        var radars = EntityQueryEnumerator<DHRadarComponent, TransformComponent>();
        while (radars.MoveNext(out var uid, out var radar, out var xform))
        {
            if (xform.MapUid != map || radar.Faction != faction)
                continue;

            markers.Add(Marker(DHTacticalMarkerKind.Radar, xform, Name(uid), faction, radar.Active ? RadarRange : 0f));
            if (radar.Active)
                eyes.Add((_transform.GetWorldPosition(xform), RadarRange));
        }

        var detectors = EntityQueryEnumerator<DHMotionDetectorComponent, TransformComponent>();
        while (detectors.MoveNext(out _, out var detector, out var xform))
        {
            if (!detector.Active || xform.MapUid != map ||
                !TryComp<DHAssaultFighterComponent>(xform.ParentUid, out var holder) || holder.Faction != faction)
            {
                continue;
            }

            eyes.Add((_transform.GetWorldPosition(xform), detector.Range));
        }

        var fighters = EntityQueryEnumerator<DHAssaultFighterComponent, TransformComponent>();
        while (fighters.MoveNext(out var uid, out var fighter, out var xform))
        {
            if (fighter.Faction == faction || xform.MapUid != map || !_mobState.IsAlive(uid))
                continue;

            var position = _transform.GetWorldPosition(xform);
            foreach (var (eye, range) in eyes)
            {
                if (Vector2.Distance(eye, position) > range)
                    continue;

                markers.Add(new DHTacticalMarker
                {
                    Kind = DHTacticalMarkerKind.Enemy,
                    X = position.X,
                    Y = position.Y,
                    Owner = fighter.Faction,
                });
                break;
            }
        }

        var marks = EntityQueryEnumerator<DHLaserMarkComponent, TransformComponent>();
        while (marks.MoveNext(out _, out var mark, out var xform))
        {
            if (xform.MapUid != map || mark.Faction is { } markFaction && markFaction != faction)
                continue;

            markers.Add(Marker(DHTacticalMarkerKind.LaserMark, xform,
                Loc.GetString("dh-tacmap-laser-mark", ("code", mark.Code)), faction, 0f));
        }

        return markers;
    }

    private DHTacticalMarker Marker(DHTacticalMarkerKind kind, TransformComponent xform, string name,
        DHAssaultFaction? owner, float radius)
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
        };
    }
}
