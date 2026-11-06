using Content.Server._DarkestHour.Shuttles.Rift;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Parallax;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using System.Numerics;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.HeavyGuns;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Gravity;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

using Content.Shared._DarkestHour.Environment;

namespace Content.Server._DarkestHour.Assault;

/// <summary>
/// Станция на поверхности планеты (военная база Республики): карта станции становится поверхностью тела — биом,
/// атмосфера и свет тела, посадочные консоли садятся на неё. Под базой и вокруг неё биом не ставит деревья и камни.
/// </summary>
public sealed partial class DHPlanetBaseSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private Content.Server._DarkestHour.Environment.DHDayNightSystem _dayNight = default!;
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private DHLandingSystem _landing = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ServerStationSystem _station = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private Content.Shared._DarkestHour.Levels.DHZLevelSystem _zLevels = default!;
    [Dependency] private Content.Server._DarkestHour.Levels.DHZTransitSystem _zTransit = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHPlanetBaseComponent, StationPostInitEvent>(OnStationPostInit);
    }

    private void OnStationPostInit(Entity<DHPlanetBaseComponent> ent, ref StationPostInitEvent args)
    {
        if (_station.GetLargestGrid(ent.Owner) is not { } grid ||
            Transform(grid).MapUid is not { } map ||
            !_proto.TryIndex(ent.Comp.System, out var system))
        {
            Log.Error($"{ToPrettyString(ent)}: planetary base has no grid or star system {ent.Comp.System}");
            return;
        }

        var bodyId = ent.Comp.Body;
        var body = system.Bodies.Find(b => b.Id == bodyId);
        if (body == null)
        {
            Log.Error($"{ToPrettyString(ent)}: no body {bodyId} in {system.ID}");
            return;
        }

        if (ent.Comp.Mapped)
        {
            SetupMappedSurface(ent, map, body);
        }
        else if (body.Biome != null && _proto.TryIndex(body.Biome.Value, out var biome))
        {
            _biome.EnsurePlanet(map, biome, mapLight: body.SurfaceLight);
        }
        else
        {
            Log.Error($"{ToPrettyString(ent)}: body {bodyId} of {system.ID} has no biome");
            return;
        }

        // Своя атмосфера тела вместо земного воздуха EnsurePlanet.
        if (body.SurfaceGases.Count > 0)
        {
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            foreach (var (gas, amount) in body.SurfaceGases)
            {
                moles[(int) gas] = amount;
            }

            _atmos.SetMapAtmosphere(map, false, new GasMixture(moles, body.SurfaceTemperature));
        }

        _landing.RegisterSurface(system.ID, body.Id, map);

        // Готовые зоны посадки челноков: маяки на поверхности, вокруг них биом не ставит камни.
        // На нарисованной карте маяки стоят на самой карте, а записи здесь (если есть) просто ставят ещё маяки.
        // Сначала грунт (биом ещё не подгружен — без тайла маяк не прикрепится), потом маяк, прикреплённый к грунту
        // планеты: иначе он оказывался не прикреплён и севший сверху челнок уносил его с собой.
        foreach (var (zoneName, position) in ent.Comp.LandingZones)
        {
            _biome.ReserveTiles(map, Box2.CenteredAround(position, new Vector2(ent.Comp.LandingZoneClear * 2)),
                new List<(Vector2i Index, Tile Tile)>());
            var zone = _landing.SpawnOnGround("DHLandingZonePad", map, position);
            var zoneComp = EnsureComp<DHLandingZoneComponent>(zone);
            zoneComp.ZoneName = zoneName;
            _meta.SetEntityName(zone, zoneName);
        }

        // В постройках базы — обычный воздух с самого начала (иначе грид получает пустую атмосферу).
        // Нарисованная поверхность — грид самой карты: весь воздух берётся из атмосферы карты (тысячи тайлов
        // открытой местности не симулируются).
        if (grid != map)
            FillAir(grid);

        LoadUnderground(ent, grid, body);
        LoadLevels(ent, map, grid, body);

        if (ent.Comp.Mapped)
            return;

        // Под базой биом не спавнит деревья и камни.
        if (TryComp<MapGridComponent>(grid, out var gridComp))
        {
            var bounds = gridComp.LocalAABB.Translated(_transform.GetWorldPosition(grid)).Enlarged(ent.Comp.ClearMargin);
            _biome.ReserveTiles(map, bounds, new List<(Vector2i Index, Tile Tile)>());
        }
    }

    /// <summary>
    /// Нарисованная вручную поверхность: без биома (сохранённый при маппинге — снимается), но с тяжестью, светом,
    /// сменой дня и ночи и воздухом тела.
    /// </summary>
    private void SetupMappedSurface(Entity<DHPlanetBaseComponent> ent, EntityUid map, DHCelestialBody body)
    {
        RemComp<BiomeComponent>(map);
        EnsureComp<MapGridComponent>(map);

        var gravity = EnsureComp<GravityComponent>(map);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(map, gravity);

        var light = EnsureComp<MapLightComponent>(map);
        light.AmbientLightColor = body.SurfaceLight;
        Dirty(map, light);

        EnsureComp<RoofComponent>(map);
        // Смена дня и ночи — своя (DHDayNightCycle, сутки 45 мин, начинаем с утра), вместо ванильного LightCycle;
        // тени от солнца ходят по тем же суткам. Погода — DHWeatherSchedule (песчаные бури).
        RemComp<LightCycleComponent>(map);
        _dayNight.Enable(map, body.SurfaceLight);
        EnsureComp<SunShadowComponent>(map);
        var shadows = EnsureComp<SunShadowCycleComponent>(map);
        shadows.Duration = TimeSpan.FromMinutes(45);
        Dirty(map, shadows);
        var weather = EnsureComp<DHWeatherScheduleComponent>(map);
        if (ent.Comp.Weather is { Count: > 0 } options)
            weather.Options = new List<DHWeatherOption>(options);
        if (ent.Comp.WeatherMinClear is { } minClear)
            weather.MinClear = minClear;
        if (ent.Comp.WeatherMaxClear is { } maxClear)
            weather.MaxClear = maxClear;
        if (ent.Comp.WeatherFirstDelay is { } firstDelay)
            weather.FirstDelay = firstDelay;

        // Воздух тела ставится общим кодом ниже (как и у процедурной планеты).
        var surface = EnsureComp<DHMappedSurfaceComponent>(map);
        surface.GroundTile = ent.Comp.GroundTile;
        surface.GroundTiles = new HashSet<ProtoId<ContentTileDefinition>>(ent.Comp.GroundTiles);
    }

    private void FillAir(EntityUid grid)
    {
        if (!TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var atmos = EnsureComp<GridAtmosphereComponent>(grid);
        EnsureComp<GasTileOverlayComponent>(grid);
        _atmos.RebuildGridAtmosphere((grid, atmos, gridComp));
    }

    /// <summary>
    /// Z-уровни базы: поверхность — уровень 0 новой стопки, бункер — −1, остальные уровни из <c>Levels</c> — свои
    /// карты с тем же смещением (этажи — с воздухом, светом и сменой суток поверхности; подземные — тёмные).
    /// </summary>
    private void LoadLevels(Entity<DHPlanetBaseComponent> ent, EntityUid surfaceMap, EntityUid surfaceGrid,
        DHCelestialBody body)
    {
        if (ent.Comp.Levels.Count == 0 && ent.Comp.UndergroundGrid == null)
            return;

        var stack = _zLevels.NewStack();
        SetLevel(surfaceMap, stack, 0);
        if (ent.Comp.UndergroundGrid is { } bunker && Transform(bunker).MapUid is { } bunkerMap)
            SetLevel(bunkerMap, stack, -1);

        foreach (var (level, path) in ent.Comp.Levels)
        {
            if (level == 0 || level < Content.Shared._DarkestHour.Levels.DHZLevelComponent.MinLevel ||
                level > Content.Shared._DarkestHour.Levels.DHZLevelComponent.MaxLevel ||
                level == -1 && ent.Comp.UndergroundGrid != null)
            {
                Log.Error($"{ToPrettyString(ent)}: bad or duplicate Z-level {level}");
                continue;
            }

            var name = level > 0
                ? Loc.GetString("dh-zlevel-map-up", ("level", level))
                : Loc.GetString("dh-zlevel-map-down", ("level", -level));
            if (CreateLevelMap(ent, surfaceGrid, body, path, name, level < 0, out var map, out var grid))
            {
                SetLevel(map, stack, level);
                ent.Comp.LevelGrids[level] = grid;
            }
        }

        // Под этажами — крыша: там нет солнца и погоды.
        _zTransit.SyncRoofs(stack);
    }

    private void SetLevel(EntityUid map, int stack, int level)
    {
        var comp = EnsureComp<Content.Shared._DarkestHour.Levels.DHZLevelComponent>(map);
        comp.Stack = stack;
        comp.Level = level;
        Dirty(map, comp);
    }

    /// <summary>Карта уровня: тяжесть и воздух тела; под землёй темно, наверху — свет и сутки поверхности.</summary>
    private bool CreateLevelMap(Entity<DHPlanetBaseComponent> ent, EntityUid surfaceGrid, DHCelestialBody body,
        ResPath path, string name, bool underground, out EntityUid mapUid, out EntityUid gridUid)
    {
        gridUid = default;
        mapUid = _map.CreateMap(out var mapId);
        _meta.SetEntityName(mapUid, name);
        if (underground)
            EnsureComp<DHUndergroundMapComponent>(mapUid);

        var gravity = EnsureComp<GravityComponent>(mapUid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(mapUid, gravity);

        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        foreach (var (gas, amount) in body.SurfaceGases)
        {
            moles[(int) gas] = amount;
        }

        _atmos.SetMapAtmosphere(mapUid, body.SurfaceGases.Count == 0, new GasMixture(moles, body.SurfaceTemperature));

        var light = EnsureComp<MapLightComponent>(mapUid);
        light.AmbientLightColor = underground ? ent.Comp.UndergroundLight : body.SurfaceLight;
        Dirty(mapUid, light);
        if (!underground)
            _dayNight.Enable(mapUid, body.SurfaceLight);

        if (!_loader.TryLoadGrid(mapId, path, out var grid, offset: _transform.GetWorldPosition(surfaceGrid)))
        {
            Log.Error($"{ToPrettyString(ent)}: failed to load Z-level {path}");
            return false;
        }

        gridUid = grid.Value;
        _meta.SetEntityName(grid.Value, name);
        _station.AddGridToStation(ent.Owner, grid.Value);
        if (ent.Comp.LevelAtmosphere)
            FillAir(grid.Value);

        return true;
    }

    /// <summary>
    /// Бункер под базой: своя карта с воздухом тела и почти без света, грид бункера с тем же смещением, что и база,
    /// входит в станцию (записи, объявления, наблюдение).
    /// </summary>
    private void LoadUnderground(Entity<DHPlanetBaseComponent> ent, EntityUid surfaceGrid, DHCelestialBody body)
    {
        if (ent.Comp.Underground is not { } path)
            return;

        var mapUid = _map.CreateMap(out var mapId);
        _meta.SetEntityName(mapUid, Loc.GetString(ent.Comp.UndergroundName));
        EnsureComp<DHUndergroundMapComponent>(mapUid);

        // Бункер — под поверхностью планеты: тяжесть та же, что наверху (без генератора и питания).
        var gravity = EnsureComp<GravityComponent>(mapUid);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(mapUid, gravity);

        var moles = new float[Atmospherics.AdjustedNumberOfGases];
        foreach (var (gas, amount) in body.SurfaceGases)
        {
            moles[(int) gas] = amount;
        }

        _atmos.SetMapAtmosphere(mapUid, body.SurfaceGases.Count == 0, new GasMixture(moles, body.SurfaceTemperature));

        var light = EnsureComp<MapLightComponent>(mapUid);
        light.AmbientLightColor = ent.Comp.UndergroundLight;
        Dirty(mapUid, light);

        if (!_loader.TryLoadGrid(mapId, path, out var grid, offset: _transform.GetWorldPosition(surfaceGrid)))
        {
            Log.Error($"{ToPrettyString(ent)}: failed to load the underground level {path}");
            return;
        }

        _meta.SetEntityName(grid.Value, Loc.GetString(ent.Comp.UndergroundName));
        ent.Comp.UndergroundGrid = grid.Value;
        _station.AddGridToStation(ent.Owner, grid.Value);
        FillAir(grid.Value);
    }
}
