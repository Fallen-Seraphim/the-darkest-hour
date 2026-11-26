using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Levels;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Explosion.EntitySystems;

/// <summary>
/// DarkestHour: на планете взрывы не ломают пол. Раньше от обстрела земля разбивалась до настила и решётки, под
/// которой виден космос (решение автора 2026-11-26). Ломается только пол, под которым на уровне ниже есть пол:
/// этажи и крыши над зданиями, земля над бункером — пролом открывает нижний уровень. Корабли и станции — как в ванили.
/// </summary>
public sealed partial class ExplosionSystem
{
    /// <summary>Карта → карта уровня ниже (null — ниже ничего), или «не Z-уровень» (Protected — защищена ли целиком).</summary>
    private readonly Dictionary<EntityUid, (bool ZLevel, MapId? Below, bool Protected)> _dhMaps = new();
    private GameTick _dhMapsTick;

    private bool DHIsPlanetSurface(EntityUid grid)
    {
        if (HasComp<DHMappedSurfaceComponent>(grid))
            return true;

        return Transform(grid).MapUid is { } map && HasComp<DHPlanetSurfaceMapComponent>(map);
    }

    /// <summary>
    /// Пол этой клетки взрыв не ломает: на Z-уровне — если на уровне ниже под ней нет пола (нечего открывать);
    /// вне Z-стопки — на поверхности планеты.
    /// </summary>
    private bool DHTileProtected(TileRef tileRef)
    {
        if (_timing.CurTick != _dhMapsTick)
        {
            _dhMaps.Clear();
            _dhMapsTick = _timing.CurTick;
        }

        var grid = tileRef.GridUid;
        var map = Transform(grid).MapUid ?? grid;
        if (!_dhMaps.TryGetValue(map, out var info))
        {
            if (TryComp<DHZLevelComponent>(map, out var level))
                info = (true, DHLevelBelow(level), false);
            else
                info = (false, null, DHIsPlanetSurface(grid));
            _dhMaps[map] = info;
        }

        if (!info.ZLevel)
            return info.Protected;

        if (info.Below is not { } below || !TryComp<MapGridComponent>(grid, out var gridComp))
            return true;

        var center = _map.GridTileToWorldPos(grid, gridComp, tileRef.GridIndices);
        var point = new MapCoordinates(center, below);
        if (!_map.TryFindGridAt(point, out var lowerGrid, out var lowerComp))
            return true;

        return !_map.TryGetTileRef(lowerGrid, lowerComp, center, out var lowerTile) || lowerTile.Tile.IsEmpty;
    }

    private MapId? DHLevelBelow(DHZLevelComponent level)
    {
        var query = EntityQueryEnumerator<DHZLevelComponent, TransformComponent>();
        while (query.MoveNext(out var other, out var xform))
        {
            if (other.Stack == level.Stack && other.Level == level.Level - 1)
                return xform.MapID;
        }

        return null;
    }
}
