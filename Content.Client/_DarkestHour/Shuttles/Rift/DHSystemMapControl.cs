using System.Numerics;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._DarkestHour.Shuttles.Rift;

/// <summary>
/// Карта звёздной системы: тела, орбиты линиями, зоны радиации, корабли и станции, точка выхода прыжка.
/// Колесо — масштаб, перетаскивание — сдвиг, щелчок — точка выхода.
/// </summary>
public sealed class DHSystemMapControl : Control
{
    private static readonly Color Background = Color.FromHex("#05080D");
    private static readonly Color OrbitColor = Color.FromHex("#8FA6C8").WithAlpha(0.3f);
    private static readonly Color RadiationColor = Color.FromHex("#E8D44D").WithAlpha(0.4f);
    private static readonly Color TargetColor = Color.FromHex("#FFD24D");
    private static readonly Color TextColor = Color.FromHex("#C8D4E6");

    private const float MinBodyPixels = 3f;
    private const float ClickTolerance = 4f;

    public event Action<Vector2>? OnTargetSelected;

    public DHStarSystemPrototype? StarSystem { get; private set; }
    public List<DHRiftNavGrid> Grids = new();
    public Vector2? Target;

    /// <summary>
    /// Квадрат с половиной стороны ZoneHalfSize вокруг начала координат: зона, в которой можно выбрать точку
    /// (зона посадки). 0 — без ограничений.
    /// </summary>
    public float ZoneHalfSize;

    private readonly Font _font;
    private readonly List<Vector2> _buffer = new();

    private Vector2 _center;
    private float _scale = 0.001f;
    private bool _needsFit = true;

    private bool _dragging;
    private float _dragDistance;

    public DHSystemMapControl()
    {
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        _font = new VectorFont(IoCManager.Resolve<IResourceCache>().GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 11);
    }

    public void SetSystem(DHStarSystemPrototype? system)
    {
        if (StarSystem?.ID == system?.ID)
            return;

        StarSystem = system;
        Target = null;
        _needsFit = true;
    }

    public void Fit()
    {
        _needsFit = true;
    }

    #region Координаты

    private Vector2 ToPixel(Vector2 world)
    {
        var mid = (Vector2) PixelSize / 2f;
        var d = (world - _center) * _scale;
        return mid + new Vector2(d.X, -d.Y);
    }

    private Vector2 FromPixel(Vector2 pixel)
    {
        var mid = (Vector2) PixelSize / 2f;
        var d = pixel - mid;
        return _center + new Vector2(d.X, -d.Y) / _scale;
    }

    private void DoFit()
    {
        var size = (Vector2) PixelSize;
        if (size.X < 1f || size.Y < 1f)
            return;

        _needsFit = false;

        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        var any = false;

        if (ZoneHalfSize > 0f)
        {
            min = new Vector2(-ZoneHalfSize);
            max = new Vector2(ZoneHalfSize);
            any = true;
        }

        if (StarSystem != null)
        {
            foreach (var body in StarSystem.Bodies)
            {
                min = Vector2.Min(min, body.Position - new Vector2(body.Radius));
                max = Vector2.Max(max, body.Position + new Vector2(body.Radius));
                any = true;
            }
        }

        foreach (var grid in Grids)
        {
            min = Vector2.Min(min, grid.Position);
            max = Vector2.Max(max, grid.Position);
            any = true;
        }

        if (!any)
        {
            _center = Vector2.Zero;
            _scale = 0.001f;
            return;
        }

        _center = (min + max) / 2f;
        var extent = Vector2.Max(max - min, new Vector2(2000f));
        _scale = MathF.Min(size.X / extent.X, size.Y / extent.Y) * 0.9f;
    }

    #endregion

    #region Ввод

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragging = true;
        _dragDistance = 0f;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick || !_dragging)
            return;

        _dragging = false;
        if (_dragDistance > ClickTolerance)
            return;

        var target = FromPixel(args.RelativePixelPosition);
        if (ZoneHalfSize > 0f)
            target = Vector2.Clamp(target, new Vector2(-ZoneHalfSize), new Vector2(ZoneHalfSize));

        Target = target;
        OnTargetSelected?.Invoke(target);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (!_dragging)
            return;

        var delta = args.Relative * UIScale;
        _dragDistance += delta.Length();
        if (_dragDistance <= ClickTolerance)
            return;

        _center -= new Vector2(delta.X, -delta.Y) / _scale;
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        var cursor = args.RelativePixelPosition;
        var before = FromPixel(cursor);
        _scale = Math.Clamp(_scale * MathF.Pow(1.25f, args.Delta.Y), 1e-6f, 2f);
        _center += before - FromPixel(cursor);
        args.Handle();
    }

    #endregion

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_needsFit)
            DoFit();

        var size = (Vector2) PixelSize;
        handle.DrawRect(new UIBox2(Vector2.Zero, size), Background);

        if (StarSystem != null)
            DrawSystem(handle, StarSystem, size);

        if (ZoneHalfSize > 0f)
            DrawZone(handle);

        DrawGrids(handle);
        DrawTarget(handle);
        DrawScale(handle, size);
    }

    private void DrawSystem(DrawingHandleScreen handle, DHStarSystemPrototype system, Vector2 size)
    {
        foreach (var body in system.Bodies)
        {
            if (body.OrbitParent == null)
                continue;

            var parent = system.Bodies.Find(b => b.Id == body.OrbitParent);
            if (parent == null)
                continue;

            var radius = Vector2.Distance(parent.Position, body.Position) * _scale;
            DHCelestialDraw.Circle(handle, ToPixel(parent.Position), radius, size, OrbitColor, null, _buffer);
        }

        foreach (var body in system.Bodies)
        {
            var center = ToPixel(body.Position);

            if (body.Radiation > 0f && body.RadiationFalloff > 0f)
            {
                DHCelestialDraw.Circle(handle, center, (body.Radius + body.RadiationFalloff) * _scale, size,
                    RadiationColor, RadiationColor.WithAlpha(0.05f), _buffer);
            }

            // Радиационный пояс гиганта: кольцо по краям пояса (пик — между ними).
            if (body.BeltRadiation > 0f)
            {
                var beltColor = RadiationColor.WithAlpha(0.6f);
                DHCelestialDraw.Circle(handle, center, (body.Radius + body.BeltAltitude + body.BeltWidth) * _scale, size,
                    beltColor, null, _buffer);
                DHCelestialDraw.Circle(handle, center,
                    (body.Radius + MathF.Max(0f, body.BeltAltitude - body.BeltWidth)) * _scale, size, beltColor, null,
                    _buffer);
            }

            var radius = body.Radius * _scale;
            if (radius < MinBodyPixels)
            {
                handle.DrawCircle(center, MinBodyPixels, body.Color);
            }
            else
            {
                var fill = body.Color.WithAlpha(body.Kind == DHCelestialKind.Star ? 0.6f : 0.35f);
                DHCelestialDraw.Circle(handle, center, radius, size, body.Color, fill, _buffer);

                if (body.EnterableDepth > 0f)
                {
                    DHCelestialDraw.Circle(handle, center, DHCelestial.SolidRadius(body) * _scale, size,
                        body.Color.WithAlpha(0.5f), null, _buffer);
                }
            }
        }

        foreach (var body in system.Bodies)
        {
            var center = ToPixel(body.Position);
            var radius = MathF.Max(body.Radius * _scale, MinBodyPixels);
            var text = Loc.GetString(body.Name);
            var dims = handle.GetDimensions(_font, text, 1f);
            var position = center + new Vector2(-dims.X / 2f, radius + 2f);

            if (position.X > size.X || position.Y > size.Y || position.X + dims.X < 0f || position.Y + dims.Y < 0f)
                continue;

            handle.DrawString(_font, position, text, body.Color);
        }
    }

    private void DrawZone(DrawingHandleScreen handle)
    {
        var a = ToPixel(new Vector2(-ZoneHalfSize, ZoneHalfSize));
        var b = ToPixel(new Vector2(ZoneHalfSize, -ZoneHalfSize));
        var box = new UIBox2(Vector2.Min(a, b), Vector2.Max(a, b));
        handle.DrawRect(box, OrbitColor.WithAlpha(0.06f));
        handle.DrawRect(box, OrbitColor, false);

        // Сетка по 500 м.
        const float step = 500f;
        for (var x = -ZoneHalfSize + step; x < ZoneHalfSize; x += step)
        {
            handle.DrawLine(ToPixel(new Vector2(x, -ZoneHalfSize)), ToPixel(new Vector2(x, ZoneHalfSize)), OrbitColor.WithAlpha(0.12f));
            handle.DrawLine(ToPixel(new Vector2(-ZoneHalfSize, x)), ToPixel(new Vector2(ZoneHalfSize, x)), OrbitColor.WithAlpha(0.12f));
        }
    }

    private void DrawGrids(DrawingHandleScreen handle)
    {
        foreach (var grid in Grids)
        {
            var p = ToPixel(grid.Position);
            if (grid.Self)
            {
                var verts = new[]
                {
                    p + new Vector2(0f, -6f),
                    p + new Vector2(4f, 0f),
                    p + new Vector2(0f, 6f),
                    p + new Vector2(-4f, 0f),
                };
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, verts, Color.Lime);
            }
            else
            {
                handle.DrawRect(UIBox2.FromDimensions(p - new Vector2(2f), new Vector2(4f)), grid.Color);
            }

            if (!string.IsNullOrEmpty(grid.Name))
                handle.DrawString(_font, p + new Vector2(7f, -7f), grid.Name, grid.Self ? Color.Lime : grid.Color);
        }
    }

    private void DrawTarget(DrawingHandleScreen handle)
    {
        if (Target == null)
            return;

        var p = ToPixel(Target.Value);

        foreach (var grid in Grids)
        {
            if (grid.Self)
                handle.DrawLine(ToPixel(grid.Position), p, TargetColor.WithAlpha(0.4f));
        }

        handle.DrawCircle(p, 7f, TargetColor, false);
        handle.DrawLine(p - new Vector2(11f, 0f), p - new Vector2(4f, 0f), TargetColor);
        handle.DrawLine(p + new Vector2(4f, 0f), p + new Vector2(11f, 0f), TargetColor);
        handle.DrawLine(p - new Vector2(0f, 11f), p - new Vector2(0f, 4f), TargetColor);
        handle.DrawLine(p + new Vector2(0f, 4f), p + new Vector2(0f, 11f), TargetColor);
    }

    private void DrawScale(DrawingHandleScreen handle, Vector2 size)
    {
        const float bar = 100f;
        var meters = bar / _scale;
        var from = new Vector2(10f, size.Y - 12f);
        handle.DrawLine(from, from + new Vector2(bar, 0f), TextColor);
        handle.DrawLine(from, from - new Vector2(0f, 4f), TextColor);
        handle.DrawLine(from + new Vector2(bar, 0f), from + new Vector2(bar, -4f), TextColor);
        handle.DrawString(_font, from + new Vector2(0f, -18f), DHCelestial.FormatDistance(meters), TextColor);
    }
}
