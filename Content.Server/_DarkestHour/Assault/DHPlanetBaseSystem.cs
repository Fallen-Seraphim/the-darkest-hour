using Content.Server._DarkestHour.Shuttles.Rift;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Parallax;
using Content.Server.Station.Events;
using Content.Server.Station.Systems;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Atmos;
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

        // Под базой биом не спавнит деревья и камни.
        if (TryComp<MapGridComponent>(grid, out var gridComp))
        {
            var bounds = gridComp.LocalAABB.Translated(_transform.GetWorldPosition(grid)).Enlarged(ent.Comp.ClearMargin);
            _biome.ReserveTiles(map, bounds, new List<(Vector2i Index, Tile Tile)>());
        }
    }
}
