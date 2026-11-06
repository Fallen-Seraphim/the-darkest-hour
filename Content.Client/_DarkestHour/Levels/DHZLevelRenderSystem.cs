using System.Numerics;
using Content.Shared._DarkestHour.CCVar;
using Content.Shared._DarkestHour.Levels;
using Robust.Shared.Configuration;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.Levels;

/// <summary>
/// Отрисовка нижних Z-уровней: если вокруг глаза на этаже есть проёмы (нет пола), основной вид рисуется в несколько
/// проходов — сначала нижние уровни (самый нижний — с очисткой), потом свой поверх: сквозь пустоту видно, что внизу.
/// Каждый уровень вниз немного затемнён (<see cref="DHZDepthShadeOverlay"/>). Сущности нижних уровней приходят
/// с сервера через «зонды» видимости (DHZRenderSystem на сервере).
/// </summary>
public sealed partial class DHZLevelRenderSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DHZLevelSystem _z = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    /// <summary>Радиус поиска проёмов вокруг глаза, тайлов.</summary>
    private const int OpeningRadius = 14;

    /// <summary>Шаг выборки дыр: проёмы лестниц — 3 тайла, шаг 2 их не пропускает.</summary>
    private const int OpeningStep = 2;

    private static readonly TimeSpan Recheck = TimeSpan.FromSeconds(0.25);

    private MapId _cachedMap = MapId.Nullspace;
    private Vector2i _cachedTile;
    private TimeSpan _cachedAt;
    private readonly List<(EntityUid Map, int Level)> _visible = new();
    private readonly List<(MapId Map, int Depth)> _cached = new();

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
    /// Какие уровни рисовать под глазом (от самого нижнего): только те, что реально видны сквозь проёмы вокруг
    /// (<see cref="DHZLevelSystem.VisibleLowerLevels"/>), не больше настройки <c>dh.zlevels_render_depth</c>.
    /// Пусто — рисовать как обычно. Пересчёт — не чаще раза в 0,25 с и при сдвиге глаза больше чем на 2 тайла.
    /// </summary>
    public bool TryGetLowerPasses(IEye eye, List<(MapId Map, int Depth)> passes)
    {
        passes.Clear();
        var at = eye.Position;
        if (at.MapId == MapId.Nullspace || !_z.TryGetLevel(at.MapId, out var own) || own.Level == 0)
            return false;

        var tile = new Vector2i((int) MathF.Floor(at.Position.X), (int) MathF.Floor(at.Position.Y));
        var now = _timing.RealTime;
        if (at.MapId != _cachedMap || now >= _cachedAt + Recheck ||
            Math.Abs(tile.X - _cachedTile.X) + Math.Abs(tile.Y - _cachedTile.Y) > 2)
        {
            _cachedMap = at.MapId;
            _cachedTile = tile;
            _cachedAt = now;
            _z.VisibleLowerLevels(at, OpeningRadius, OpeningStep, _cfg.GetCVar(DHCVars.ZLevelsRenderDepth), _visible);

            _cached.Clear();
            foreach (var (map, level) in _visible)
            {
                if (TryComp<MapComponent>(map, out var mapComp))
                    _cached.Add((mapComp.MapId, own.Level - level));
            }
        }

        passes.AddRange(_cached);
        return passes.Count > 0;
    }
}

/// <summary>Глаз прохода нижнего уровня: глубина — насколько уровень ниже глаза.</summary>
public sealed class DHZPassEye : Robust.Shared.Graphics.Eye
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
