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
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Assault;

/// <summary>
/// Станция на поверхности планеты (военная база Республики): карта станции становится поверхностью тела — биом,
/// атмосфера и свет тела, посадочные консоли садятся на неё. Под базой и вокруг неё биом не ставит деревья и камни.
/// </summary>
public sealed partial class DHPlanetBaseSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private DHLandingSystem _landing = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ServerStationSystem _station = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

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
        if (body?.Biome == null || !_proto.TryIndex(body.Biome.Value, out var biome))
        {
            Log.Error($"{ToPrettyString(ent)}: body {bodyId} of {system.ID} has no biome");
            return;
        }

        _biome.EnsurePlanet(map, biome, mapLight: body.SurfaceLight);

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
        FillAir(grid);
        LoadUnderground(ent, grid, body);

        // Под базой биом не спавнит деревья и камни.
        if (TryComp<MapGridComponent>(grid, out var gridComp))
        {
            var bounds = gridComp.LocalAABB.Translated(_transform.GetWorldPosition(grid)).Enlarged(ent.Comp.ClearMargin);
            _biome.ReserveTiles(map, bounds, new List<(Vector2i Index, Tile Tile)>());
        }
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
