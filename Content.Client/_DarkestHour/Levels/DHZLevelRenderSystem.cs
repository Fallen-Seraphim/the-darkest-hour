using System.Numerics;
using Content.Shared._DarkestHour.CCVar;
using Content.Shared._DarkestHour.Levels;
using Robust.Shared.Configuration;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
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
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SpriteTreeSystem _spriteTree = default!;

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
    private bool _cachedFog;

    /// <summary>Глубже трёх уровней вниз не видно (решение автора 2026-11-06).</summary>
    private const int MaxRenderDepth = 3;

    /// <summary>Серое туманное небо Лост-Энджелса — то, что видно вниз глубже трёх уровней.</summary>
    private static readonly Color FogColor = Color.FromHex("#8C939C");

    private readonly List<Entity<SpriteComponent, TransformComponent>> _treeCheck = new();
    private TimeSpan _staleLoggedAt;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new DHZDepthShadeOverlay());
        _overlays.AddOverlay(new DHZRoofOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<DHZDepthShadeOverlay>();
        _overlays.RemoveOverlay<DHZRoofOverlay>();
    }

    /// <summary>С уровня воздуха (пилот на бреющем) здания внизу видны крышами.</summary>
    public bool ShowRoofs(IEye eye)
    {
        return eye.Position.MapId != MapId.Nullspace && _z.IsAirLevel(eye.Position.MapId);
    }

    /// <summary>
    /// Какие уровни рисовать под глазом (от самого нижнего): только те, что реально видны сквозь проёмы вокруг
    /// (<see cref="DHZLevelSystem.VisibleLowerLevels"/>), не глубже трёх уровней (решение автора 2026-11-06; меньше —
    /// настройкой <c>dh.zlevels_render_depth</c>). Что глубже, закрашивается <paramref name="clear"/>: над землёй —
    /// серая дымка неба Лост-Энджелса (в тон свету карты), под землёй — темнота.
    /// false — проёмов нет, рисовать как обычно. Пересчёт — не чаще раза в 0,25 с и при сдвиге глаза больше 2 тайлов.
    /// </summary>
    public bool TryGetLowerPasses(IEye eye, List<(MapId Map, int Depth)> passes, out Color clear)
    {
        passes.Clear();
        clear = Color.Black;
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
            var depth = _z.ViewDepth(at.MapId, Math.Clamp(_cfg.GetCVar(DHCVars.ZLevelsRenderDepth), 0, MaxRenderDepth));
            _cachedFog = _z.VisibleLowerLevels(at, OpeningRadius, OpeningStep, depth, _visible);

            _cached.Clear();
            foreach (var (map, level) in _visible)
            {
                if (TryComp<MapComponent>(map, out var mapComp))
                    _cached.Add((mapComp.MapId, own.Level - level));
            }
        }

        passes.AddRange(_cached);
        if (passes.Count == 0 && !_cachedFog)
            return false;

        if (own.Level > 0)
        {
            // Дымка — в тон свету карты: днём серая, в сумерках темнеет, ночью почти чёрная.
            var light = TryComp<MapLightComponent>(_map.GetMapOrInvalid(at.MapId), out var mapLight)
                ? mapLight.AmbientLightColor
                : Color.White;
            clear = new Color(FogColor.R * light.R, FogColor.G * light.G, FogColor.B * light.B, 1f);
        }

        return true;
    }
    /// <summary>
    /// Чинит «застрявшие» записи дерева спрайтов перед проходом уровня. Движок при обновлении дерева бросает всю
    /// очередь, если сущность ушла на грид/карту без дерева (удаляемый грид) — и сущности, перешедшие в тот же кадр
    /// на другой уровень (падение, лестница, лифт), остаются числиться в дереве старой карты. Основной вид этих мест
    /// обычно не видит, а проход нижнего уровня рисует их: «Target entity (Уровень +5) not in transform hierarchy»
    /// и IndexOutOfRangeException в RenderSprite — экран перестаёт рисоваться. Здесь такие записи переставляются
    /// по одной (чтобы сброс очереди не задел остальные).
    /// </summary>
    public void FixSpriteTree(MapId map, Vector2 center, float radius)
    {
        if (map == MapId.Nullspace)
            return;

        _treeCheck.Clear();
        _spriteTree.QueryAabb(_treeCheck, map, Box2.CenteredAround(center, new Vector2(radius * 2f)));
        var stale = 0;
        foreach (var (uid, sprite, xform) in _treeCheck)
        {
            if (sprite.TreeUid == (xform.GridUid ?? xform.MapUid))
                continue;

            stale++;
            _spriteTree.QueueTreeUpdate(uid, sprite, xform);
            _spriteTree.UpdateTreePositions();
        }

        _treeCheck.Clear();
        if (stale == 0 || _timing.RealTime < _staleLoggedAt + TimeSpan.FromSeconds(10))
            return;

        _staleLoggedAt = _timing.RealTime;
        Log.Debug($"Z-уровни: переставлено застрявших спрайтов в дереве карты {map}: {stale}.");
    }
}

/// <summary>Глаз прохода нижнего уровня: глубина — насколько уровень ниже глаза.</summary>
public sealed class DHZPassEye : Robust.Shared.Graphics.Eye
{
    public int Depth;

    /// <summary>Смотрят с уровня воздуха: здания этого уровня рисуются крышами.</summary>
    public bool Roofs;
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
