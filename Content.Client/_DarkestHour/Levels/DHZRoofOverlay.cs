using System.Numerics;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._DarkestHour.Levels;

/// <summary>
/// Крыши для пилота (решение автора 2026-11-13): когда смотрят вниз с уровня воздуха, клетки под крышей (флаг крыши
/// грида) закрываются плитой — металлом, шифером или бетоном по полу под ней. Людей, мебель и технику внутри зданий
/// сверху не видно; на открытом месте видно всё. Рисуется поверх сущностей, но под светом карты.
/// </summary>
public sealed class DHZRoofOverlay : Overlay
{
    private static readonly ResPath Rsi = new("_DarkestHour/Effects/roofs.rsi");

    private readonly IEntityManager _entities;
    private readonly ITileDefinitionManager _tiles;
    private readonly SharedMapSystem _map;
    private readonly SharedRoofSystem _roof;
    private readonly SharedTransformSystem _transform;
    private readonly Texture _metal;
    private readonly Texture _slate;
    private readonly Texture _concrete;
    private readonly Dictionary<int, Texture> _byTile = new();
    private List<Entity<MapGridComponent>> _grids = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public DHZRoofOverlay(IEntityManager entities)
    {
        _entities = entities;
        _tiles = IoCManager.Resolve<ITileDefinitionManager>();
        _map = entities.System<SharedMapSystem>();
        _roof = entities.System<SharedRoofSystem>();
        _transform = entities.System<SharedTransformSystem>();

        var sprite = entities.System<SpriteSystem>();
        _metal = sprite.Frame0(new SpriteSpecifier.Rsi(Rsi, "metal"));
        _slate = sprite.Frame0(new SpriteSpecifier.Rsi(Rsi, "slate"));
        _concrete = sprite.Frame0(new SpriteSpecifier.Rsi(Rsi, "concrete"));

        // Над дверями и техникой, под газами и эффектами (дым и взрывы видно и сверху).
        ZIndex = (int) DrawDepth.Overdoors + 1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.Viewport.Eye is DHZPassEye { Depth: > 0, Roofs: true };
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        _grids.Clear();
        _map.FindGridsIntersecting(args.MapId, args.WorldAABB, ref _grids, approx: true, includeMap: true);

        foreach (var grid in _grids)
        {
            if (!_entities.TryGetComponent(grid.Owner, out RoofComponent? roof))
                continue;

            handle.SetTransform(_transform.GetWorldMatrix(grid.Owner));
            Entity<MapGridComponent, RoofComponent> roofed = (grid.Owner, grid.Comp, roof);
            var size = grid.Comp.TileSize;
            var tiles = _map.GetTilesIntersecting(grid.Owner, grid.Comp, args.WorldBounds);
            while (tiles.MoveNext(out var tile))
            {
                if (!_roof.IsRooved(roofed, tile.GridIndices))
                    continue;

                var box = Box2.FromDimensions(new Vector2(tile.GridIndices.X * size, tile.GridIndices.Y * size),
                    new Vector2(size, size));
                handle.DrawTextureRect(TextureFor(tile.Tile.TypeId), box);
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    /// <summary>Крыша по полу под ней: металл над техническими и стальными полами, шифер над деревом, иначе бетон.</summary>
    private Texture TextureFor(int tileId)
    {
        if (_byTile.TryGetValue(tileId, out var cached))
            return cached;

        var id = _tiles[tileId].ID;
        var texture = id.Contains("Wood") || id.Contains("Carpet") ? _slate
            : id.Contains("Steel") || id.Contains("Plating") || id.Contains("Reinforced") || id.Contains("Dark") ||
              id.Contains("Tech") || id.Contains("Mono") || id.Contains("Plate") || id.Contains("Metal") ||
              id.Contains("Cargo") ? _metal
            : _concrete;
        _byTile[tileId] = texture;
        return texture;
    }
}
