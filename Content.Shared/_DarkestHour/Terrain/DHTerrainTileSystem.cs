using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._DarkestHour.Terrain;

/// <summary>
/// Вода и скалы тайлами (решение автора 2026-11-27, большой Терминус 640×768): на огромной карте каждая клетка воды и
/// скалы отдельной сущностью — это сотни тысяч сущностей. Поэтому глубина в середине моря и озера — просто тайл
/// <see cref="DeepWaterTile"/> (к нему не подойти пешком: у берега кольцо обычной глубокой воды-сущностей), а нутро
/// горного массива — тайл <see cref="RockTile"/> под большими блоками-препятствиями (<c>DHRockMass</c>). Системы, которым
/// важна вода или скала под клеткой (лодки, погружение, посадка, огонь, тактическая карта), спрашивают сюда.
/// </summary>
public sealed partial class DHTerrainTileSystem : EntitySystem
{
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private SharedMapSystem _map = default!;

    public const string DeepWaterTile = "DHFloorSeaDeep";
    public const string RockTile = "DHFloorRockMass";

    /// <summary>Глубина погружения на тайловой глубокой воде (как у <c>DHWaterDeep</c>).</summary>
    public const float DeepWaterDepth = 18f;

    private int? _deepWaterId;
    private int? _rockId;

    private int TileId(ref int? cache, string name)
    {
        if (cache == null)
            cache = _tiles.TryGetDefinition(name, out var def) ? def.TileId : -1;

        return cache.Value;
    }

    public bool IsDeepWater(Tile tile)
    {
        return !tile.IsEmpty && tile.TypeId == TileId(ref _deepWaterId, DeepWaterTile);
    }

    public bool IsRock(Tile tile)
    {
        return !tile.IsEmpty && tile.TypeId == TileId(ref _rockId, RockTile);
    }

    public bool IsDeepWater(EntityUid grid, MapGridComponent gridComp, Vector2i indices)
    {
        return _map.TryGetTileRef(grid, gridComp, indices, out var tile) && IsDeepWater(tile.Tile);
    }

    public bool IsRock(EntityUid grid, MapGridComponent gridComp, Vector2i indices)
    {
        return _map.TryGetTileRef(grid, gridComp, indices, out var tile) && IsRock(tile.Tile);
    }

    /// <summary>Тайловая глубина или скала — встать здесь нельзя.</summary>
    public bool IsImpassableTerrain(EntityUid grid, MapGridComponent gridComp, Vector2i indices)
    {
        return _map.TryGetTileRef(grid, gridComp, indices, out var tile) && (IsDeepWater(tile.Tile) || IsRock(tile.Tile));
    }
}
