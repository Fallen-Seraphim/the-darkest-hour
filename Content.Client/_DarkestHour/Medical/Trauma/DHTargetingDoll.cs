using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._DarkestHour.Medical.Trauma;

/// <summary>
/// Кукла выбора зоны попадания: голова, торс, руки, кисти, ноги, ступни. Рисуется в коде (прямоугольники), без спрайтов.
/// Правая сторона тела — слева на экране (кукла смотрит на игрока).
/// </summary>
public sealed class DHTargetingDoll : Control
{
    public event Action<string>? OnZoneSelected;

    private const float Scale = 2f;
    private static readonly Vector2 DollSize = new(32, 58);

    private static readonly (string Zone, Box2 Rect)[] Zones =
    {
        ("Head", new Box2(12, 1, 20, 10)),
        ("Torso", new Box2(10, 11, 22, 30)),
        ("ArmRight", new Box2(5, 11, 9, 26)),
        ("HandRight", new Box2(4, 27, 9, 32)),
        ("ArmLeft", new Box2(23, 11, 27, 26)),
        ("HandLeft", new Box2(23, 27, 28, 32)),
        ("LegRight", new Box2(10, 31, 15, 51)),
        ("FootRight", new Box2(8, 52, 15, 57)),
        ("LegLeft", new Box2(17, 31, 22, 51)),
        ("FootLeft", new Box2(17, 52, 24, 57)),
    };

    private static readonly Color Fill = Color.FromHex("#2e3236");
    private static readonly Color Border = Color.FromHex("#6b7076");
    private static readonly Color Hover = Color.FromHex("#55606a");
    private static readonly Color Selected = Color.FromHex("#c9a35a");

    public string Selection = "Torso";

    private string? _hovered;

    public DHTargetingDoll()
    {
        MouseFilter = MouseFilterMode.Stop;
        MinSize = DollSize * Scale;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return DollSize * Scale;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var scale = Scale * UIScale;
        var cursor = UserInterfaceManager.MousePositionScaled.Position * UIScale - GlobalPixelPosition;
        _hovered = null;

        foreach (var (zone, rect) in Zones)
        {
            var box = new UIBox2(rect.Left * scale, rect.Bottom * scale, rect.Right * scale, rect.Top * scale);
            var hovered = box.Contains(cursor);
            if (hovered)
                _hovered = zone;

            var color = zone == Selection ? Selected : hovered ? Hover : Fill;
            handle.DrawRect(box, color);
            handle.DrawRect(box, Border, filled: false);
        }
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Handled || args.Function != EngineKeyFunctions.UIClick || _hovered == null)
            return;

        Selection = _hovered;
        OnZoneSelected?.Invoke(_hovered);
        UserInterfaceManager.ClickSound();
        args.Handle();
    }
}
