using System.Numerics;
using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Fluids;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map.Components;

namespace Content.Server._DarkestHour.Shuttles.Rift;

/// <summary>
/// Высадка за краем нарисованной карты (решение автора 2026-11-01): если авианосец (челнок) висит в атмосфере за
/// пределами района операции, прыгнувшие не падают в пустоту — приземляются у ближайшей границы карты, внутри
/// играбельной зоны, в случайной точке вдоль ближайшего участка границы (разброс, чтобы не было одного «угла» для
/// абуза), на свободной суше — не в стене, не в скале и не в воде.
/// </summary>
public sealed partial class DHLandingSystem
{
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    /// <summary>Сколько клеток от края карты (скалы края) держаться.</summary>
    private const float EdgeMargin = 5f;

    /// <summary>Разброс вдоль границы, клеток в каждую сторону.</summary>
    private const float EdgeSpread = 20f;

    /// <summary>Радиус поиска свободной клетки вокруг выбранной точки.</summary>
    private const int FreeTileSearch = 12;

    private readonly Dictionary<EntityUid, Box2> _surfaceBounds = new();

    /// <summary>Точка приземления внутри района операции (для нарисованных вручную карт).</summary>
    private Vector2 KeepInsideMappedSurface(EntityUid surface, Vector2 target)
    {
        if (!HasComp<DHMappedSurfaceComponent>(surface) || !TryComp<MapGridComponent>(surface, out var grid))
            return target;

        if (!_surfaceBounds.TryGetValue(surface, out var bounds))
        {
            bounds = SurfaceTileBounds(surface, grid);
            _surfaceBounds[surface] = bounds;
        }

        var inner = new Box2(bounds.Left + EdgeMargin, bounds.Bottom + EdgeMargin,
            bounds.Right - EdgeMargin, bounds.Top - EdgeMargin);
        if (inner.Width <= 0f || inner.Height <= 0f)
            return bounds.Center;

        var outsideX = target.X < inner.Left || target.X > inner.Right;
        var outsideY = target.Y < inner.Bottom || target.Y > inner.Top;
        var point = target;

        if (outsideX || outsideY)
        {
            point = Clamp(target, inner);

            // Вдоль той границы, к которой вышли (в углу — в обе стороны внутрь).
            if (outsideX && !outsideY)
                point.Y += _random.NextFloat(-EdgeSpread, EdgeSpread);
            else if (outsideY && !outsideX)
                point.X += _random.NextFloat(-EdgeSpread, EdgeSpread);
            else
                point += new Vector2(
                    target.X < inner.Left ? _random.NextFloat(0f, EdgeSpread) : -_random.NextFloat(0f, EdgeSpread),
                    target.Y < inner.Bottom ? _random.NextFloat(0f, EdgeSpread) : -_random.NextFloat(0f, EdgeSpread));

            point = Clamp(point, inner);
        }
        else if (IsFreeLanding(surface, grid, point))
        {
            return target;
        }

        return FindFreeLanding(surface, grid, point, inner) ?? point;
    }

    private static Vector2 Clamp(Vector2 point, Box2 box)
    {
        return new Vector2(Math.Clamp(point.X, box.Left, box.Right), Math.Clamp(point.Y, box.Bottom, box.Top));
    }

    private Box2 SurfaceTileBounds(EntityUid gridUid, MapGridComponent grid)
    {
        var any = false;
        var box = new Box2();
        foreach (var tile in _map.GetAllTiles(gridUid, grid))
        {
            var tileBox = Box2.FromDimensions(tile.GridIndices.X, tile.GridIndices.Y, 1f, 1f);
            box = any ? box.Union(tileBox) : tileBox;
            any = true;
        }

        return any ? box : grid.LocalAABB;
    }

    /// <summary>Ближайшая к точке свободная клетка (по расширяющимся кольцам) внутри района.</summary>
    private Vector2? FindFreeLanding(EntityUid surface, MapGridComponent grid, Vector2 point, Box2 inner)
    {
        var center = new Vector2i((int) MathF.Floor(point.X), (int) MathF.Floor(point.Y));
        for (var radius = 0; radius <= FreeTileSearch; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                        continue;

                    var candidate = new Vector2(center.X + dx + 0.5f, center.Y + dy + 0.5f);
                    if (inner.Contains(candidate) && IsFreeLanding(surface, grid, candidate))
                        return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>Есть грунт, клетка не занята стеной/скалой и не вода.</summary>
    private bool IsFreeLanding(EntityUid surface, MapGridComponent grid, Vector2 position)
    {
        var indices = _map.WorldToTile(surface, grid, position);
        if (!_map.TryGetTileRef(surface, grid, indices, out var tile) || tile.Tile.IsEmpty)
            return false;

        if (_turf.IsTileBlocked(surface, indices, CollisionGroup.Impassable, grid))
            return false;

        foreach (var uid in _lookup.GetLocalEntitiesIntersecting(surface, indices, 0f, LookupFlags.Static | LookupFlags.Sundries, grid))
        {
            if (HasComp<DHWaterSourceComponent>(uid))
                return false;
        }

        return true;
    }
}
