using Content.Server.Parallax;
using Content.Shared.Maps;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Assault;

/// <summary>
/// Поверхность планеты, нарисованная вручную (без биома): карта целиком сохранена мапером. «Голая земля» здесь —
/// тайлы из списка, пустые тайлы застилаются <see cref="GroundTile"/>.
/// </summary>
[RegisterComponent]
public sealed partial class DHMappedSurfaceComponent : Component
{
    [DataField]
    public ProtoId<ContentTileDefinition> GroundTile = "FloorAsteroidSand";

    [DataField]
    public HashSet<ProtoId<ContentTileDefinition>> GroundTiles = new();
}

/// <summary>
/// Грунт поверхности: что считается голой землёй (мешки с песком) и как поставить грунт там, где тайла нет —
/// и на процедурных планетах (биом), и на нарисованных вручную.
/// </summary>
public sealed partial class DHGroundSystem : EntitySystem
{
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>Стоит на голой земле планеты, а не на полу постройки или палубе.</summary>
    public bool OnGround(EntityUid uid)
    {
        return IsGroundAt(Transform(uid).Coordinates);
    }

    /// <summary>Голая земля планеты в точке: грунт биома или тайл грунта нарисованной поверхности.</summary>
    public bool IsGroundAt(EntityCoordinates coordinates)
    {
        if (_transform.GetGrid(coordinates) is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var tile = _map.GetTileRef(grid, gridComp, coordinates);
        if (tile.Tile.IsEmpty)
            return false;

        // Процедурная планета: вся сетка карты — грунт биома.
        if (HasComp<BiomeComponent>(grid))
            return true;

        if (!TryComp<DHMappedSurfaceComponent>(grid, out var surface))
            return false;

        var def = (ContentTileDefinition) _tileDef[tile.Tile.TypeId];
        return def.ID == surface.GroundTile.Id || surface.GroundTiles.Contains(def.ID);
    }

    /// <summary>
    /// Нарисованная поверхность конечна: есть ли в точке грунт. Для процедурных планет (и не поверхностей) — всегда да.
    /// </summary>
    public bool HasSurfaceAt(EntityUid map, System.Numerics.Vector2 position)
    {
        if (!HasComp<DHMappedSurfaceComponent>(map) || !TryComp<MapGridComponent>(map, out var grid))
            return true;

        var tile = _map.WorldToTile(map, grid, position);
        return _map.TryGetTileRef(map, grid, tile, out var tileRef) && !tileRef.Tile.IsEmpty;
    }

    /// <summary>Есть ли под точкой тайл; если нет — ставит грунт (биома или тайл нарисованной поверхности).</summary>
    public void EnsureGround(EntityUid map, MapGridComponent grid, Vector2i tile)
    {
        if (_map.TryGetTileRef(map, grid, tile, out var tileRef) && !tileRef.Tile.IsEmpty)
            return;

        if (HasComp<BiomeComponent>(map))
        {
            _biome.ReserveTiles(map, Box2.CenteredAround(_map.GridTileToWorldPos(map, grid, tile), new System.Numerics.Vector2(0.5f)),
                new List<(Vector2i Index, Tile Tile)>());
            return;
        }

        if (!TryComp<DHMappedSurfaceComponent>(map, out var surface))
            return;

        var def = (ContentTileDefinition) _tileDef[surface.GroundTile.Id];
        _map.SetTile(map, grid, tile, new Tile(def.TileId, variant: _random.NextByte(def.Variants)));
    }
}
