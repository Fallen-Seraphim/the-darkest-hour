using System.Numerics;
using Content.Shared._DarkestHour.Levels;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.Levels;

/// <summary>
/// Отрисовка нижних Z-уровней: если вокруг глаза на этаже есть проёмы (нет пола), основной вид рисуется в несколько
/// проходов — сначала нижние уровни (самый нижний — с очисткой), потом свой поверх: сквозь пустоту видно, что внизу.
/// Каждый уровень вниз немного затемнён (<see cref="DHZDepthShadeOverlay"/>). Сущности нижних уровней приходят
/// с сервера через «зонды» видимости (DHZRenderSystem на сервере).
/// </summary>
public sealed class DHZLevelRenderSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DHZLevelSystem _z = default!;

    /// <summary>Радиус поиска проёмов вокруг глаза, тайлов.</summary>
    private const int OpeningRadius = 14;

    private static readonly TimeSpan OpeningRecheck = TimeSpan.FromSeconds(0.25);

    private MapId _openingMap = MapId.Nullspace;
    private Vector2i _openingTile;
    private bool _opening;
    private TimeSpan _openingChecked;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new DHZDepthShadeOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<DHZDepthShadeOverlay>();
    }

    /// <summary>
    /// Какие уровни рисовать под глазом (от самого нижнего): с этажа — до земли, из подземелья — до самого низа.
    /// Пусто — рисовать как обычно.
    /// </summary>
    public bool TryGetLowerPasses(IEye eye, List<(MapId Map, int Depth)> passes)
    {
        passes.Clear();
        var mapId = eye.Position.MapId;
        if (mapId == MapId.Nullspace || !_z.TryGetLevel(mapId, out var own) || own.Level == 0)
            return false;

        if (!HasOpening(eye.Position))
            return false;

        foreach (var (map, level) in _z.Levels(own.Stack))
        {
            if (level >= own.Level || own.Level > 0 && level < 0)
                continue;

            if (!TryComp<MapComponent>(map, out var mapComp))
                continue;

            passes.Add((mapComp.MapId, own.Level - level));
        }

        // Levels() — по возрастанию: первым идёт самый нижний.
        return passes.Count > 0;
    }

    private bool HasOpening(MapCoordinates at)
    {
        var tile = new Vector2i((int) MathF.Floor(at.Position.X), (int) MathF.Floor(at.Position.Y));
        var now = _timing.RealTime;
        if (at.MapId == _openingMap && now < _openingChecked + OpeningRecheck &&
            Math.Abs(tile.X - _openingTile.X) + Math.Abs(tile.Y - _openingTile.Y) <= 2)
        {
            return _opening;
        }

        _openingMap = at.MapId;
        _openingTile = tile;
        _openingChecked = now;
        _opening = false;

        for (var x = -OpeningRadius; x <= OpeningRadius && !_opening; x++)
        {
            for (var y = -OpeningRadius; y <= OpeningRadius; y++)
            {
                var point = new MapCoordinates(new Vector2(tile.X + x + 0.5f, tile.Y + y + 0.5f), at.MapId);
                if (_z.HasFloor(point))
                    continue;

                _opening = true;
                break;
            }
        }

        return _opening;
    }
}

/// <summary>Глаз прохода нижнего уровня: глубина — насколько уровень ниже глаза.</summary>
public sealed class DHZPassEye : Eye
{
    public int Depth;
}

/// <summary>
/// Затемнение нижних уровней: каждый проход нижнего уровня притеняет всё, что нарисовано до него, — чем глубже
/// уровень, тем больше проходов поверх него и тем он темнее (на 8 уровней — около 57 %).
/// </summary>
public sealed class DHZDepthShadeOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private const float ShadePerPass = 0.1f;

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.Viewport.Eye is DHZPassEye { Depth: > 0 };
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye is not DHZPassEye)
            return;

        args.WorldHandle.DrawRect(args.WorldBounds, Color.Black.WithAlpha(ShadePerPass));
    }
}
