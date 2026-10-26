using System.Numerics;
using Content.Client.Parallax;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client._DarkestHour.Shuttles.Rift;

/// <summary>
/// Корабль в облаках газового гиганта: за бортом вместо звёзд — сплошная толща цвета гиганта с полосами облаков,
/// несущимися по ветру, поверх всего — дымка; чем глубже, тем гуще и темнее.
/// </summary>
public sealed partial class DHGasLayerOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private DHGasLayerBackOverlay _back = default!;
    private DHGasLayerFrontOverlay _front = default!;
    private readonly DHGasLayerView _view = new();

    public override void Initialize()
    {
        base.Initialize();

        var rsi = new ResPath("/Textures/_DarkestHour/Effects/gas_layer.rsi");
        _back = new DHGasLayerBackOverlay(_view, _sprite.Frame0(new SpriteSpecifier.Rsi(rsi, "far")),
            _sprite.Frame0(new SpriteSpecifier.Rsi(rsi, "near")));
        _front = new DHGasLayerFrontOverlay(_view);
        _overlays.AddOverlay(_back);
        _overlays.AddOverlay(_front);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_back);
        _overlays.RemoveOverlay(_front);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        DHGasLayerComponent? layer = null;
        if (_player.LocalEntity is { } local && Transform(local).GridUid is { } grid)
            TryComp(grid, out layer);

        // Вход в облака и выход — за ~2 с, глубина меняется плавно.
        var target = layer != null ? 1f : 0f;
        _view.Strength += Math.Clamp(target - _view.Strength, -frameTime / 2f, frameTime / 2f);
        if (layer != null)
        {
            _view.Color = layer.Color;
            _view.Depth += (layer.Depth - _view.Depth) * MathF.Min(1f, frameTime * 0.8f);
        }

        _view.Time += frameTime;
    }
}

public sealed class DHGasLayerView
{
    public float Strength;
    public float Depth;
    public Color Color = Color.Gray;
    public float Time;
}

/// <summary>Толща облаков за бортом: над фоном звёзд, под кораблём.</summary>
public sealed class DHGasLayerBackOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    private readonly DHGasLayerView _view;
    private readonly Texture _far;
    private readonly Texture _near;

    public DHGasLayerBackOverlay(DHGasLayerView view, Texture far, Texture near)
    {
        _view = view;
        _far = far;
        _near = near;
        ZIndex = ParallaxSystem.ParallaxZIndex + 1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _view.Strength > 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var s = _view.Strength;
        var depth = _view.Depth;
        var color = _view.Color;

        // Сплошная толща: у верхней кромки облаков ещё просвечивают звёзды, глубже — нет.
        var deep = Color.InterpolateBetween(color, Color.Black, 0.35f + 0.45f * depth);
        handle.DrawRect(args.WorldBounds, deep.WithAlpha(s * (0.75f + 0.25f * depth)));

        var eye = args.WorldAABB.Center;
        Tile(handle, args.WorldAABB, _far, 4f, eye * 0.9f + new Vector2(_view.Time * 6f, 0f),
            Color.InterpolateBetween(color, Color.Black, 0.2f + 0.4f * depth).WithAlpha(s * 0.85f));
        Tile(handle, args.WorldAABB, _near, 3f, eye * 0.75f + new Vector2(_view.Time * 14f, 0f),
            Color.InterpolateBetween(color, Color.White, 0.15f - 0.15f * depth).WithAlpha(s * (0.55f + 0.3f * depth)));
    }

    /// <summary>Текстура плиткой по видимой области; origin сдвигает узор (параллакс и ветер).</summary>
    private static void Tile(DrawingHandleWorld handle, Box2 bounds, Texture texture, float scale, Vector2 origin,
        Color color)
    {
        var size = texture.Size / (float) EyeManager.PixelsPerMeter * scale;
        var start = ((bounds.BottomLeft - origin) / size).Floored() * size + origin;
        for (var x = start.X; x < bounds.Right; x += size.X)
        {
            for (var y = start.Y; y < bounds.Top; y += size.Y)
            {
                handle.DrawTextureRect(texture, Box2.FromDimensions(new Vector2(x, y), size), color);
            }
        }
    }
}

/// <summary>Дымка поверх всего: облака обтекают корабль, свет тускнеет с глубиной.</summary>
public sealed class DHGasLayerFrontOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly DHGasLayerView _view;

    public DHGasLayerFrontOverlay(DHGasLayerView view)
    {
        _view = view;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _view.Strength > 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var s = _view.Strength;
        var depth = _view.Depth;

        handle.DrawRect(args.WorldBounds, _view.Color.WithAlpha(s * (0.06f + 0.16f * depth)));
        handle.DrawRect(args.WorldBounds, Color.Black.WithAlpha(s * 0.3f * depth));
    }
}
