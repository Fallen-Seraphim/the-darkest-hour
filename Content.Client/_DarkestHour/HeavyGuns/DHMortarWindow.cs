using System.Globalization;
using System.Numerics;
using Content.Shared._DarkestHour.HeavyGuns;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.HeavyGuns;

/// <summary>Прицел миномёта: своё место, точка прицеливания (координаты или метка ЛЦУ), дальность, готовность.</summary>
public sealed class DHMortarWindow : DefaultWindow
{
    public event Action<Vector2>? OnAim;

    private static readonly Color Ok = Color.FromHex("#8FD66B");
    private static readonly Color Warn = Color.FromHex("#E8B04A");
    private static readonly Color Dim = Color.FromHex("#8A8A8A");

    private readonly Label _position = new();
    private readonly Label _aimed = new();
    private readonly Label _range = new() { FontColorOverride = Dim };
    private readonly Label _status = new();
    private readonly LineEdit _x = new() { Text = "0", MinWidth = 80 };
    private readonly LineEdit _y = new() { Text = "0", MinWidth = 80 };
    private readonly BoxContainer _marks = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };

    private bool _filled;

    public DHMortarWindow()
    {
        Title = Loc.GetString("dh-mortar-ui-title");
        MinSize = new Vector2(380, 360);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        root.AddChild(_status);
        root.AddChild(_position);
        root.AddChild(_aimed);
        root.AddChild(_range);

        root.AddChild(new Label { Text = Loc.GetString("dh-mortar-ui-coordinates"), FontColorOverride = Warn });
        root.AddChild(Row("X", _x));
        root.AddChild(Row("Y", _y));

        var aim = new Button { Text = Loc.GetString("dh-mortar-ui-aim") };
        aim.OnPressed += _ => OnAim?.Invoke(new Vector2(Parse(_x.Text), Parse(_y.Text)));
        root.AddChild(aim);

        root.AddChild(new Label { Text = Loc.GetString("dh-mortar-ui-marks"), FontColorOverride = Warn });
        root.AddChild(_marks);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(root);
        Contents.AddChild(scroll);
    }

    private BoxContainer Row(string axis, LineEdit edit)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        row.AddChild(new Label { Text = axis, MinWidth = 16 });
        foreach (var delta in new[] { -10, -1 })
            row.AddChild(Nudge(edit, delta));
        row.AddChild(edit);
        foreach (var delta in new[] { 1, 10 })
            row.AddChild(Nudge(edit, delta));
        return row;
    }

    private static Button Nudge(LineEdit edit, int delta)
    {
        var button = new Button { Text = delta > 0 ? $"+{delta}" : delta.ToString(), MinWidth = 36 };
        button.OnPressed += _ => edit.Text = (Parse(edit.Text) + delta).ToString("0", CultureInfo.InvariantCulture);
        return button;
    }

    private static float Parse(string text)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
    }

    public void UpdateState(DHMortarState state)
    {
        _status.Text = state.Status;
        _status.FontColorOverride = state.Target != null ? Ok : Warn;
        _position.Text = Loc.GetString("dh-mortar-ui-position", ("x", (int) MathF.Round(state.Position.X)),
            ("y", (int) MathF.Round(state.Position.Y)));
        _range.Text = Loc.GetString("dh-mortar-ui-range", ("min", (int) state.MinRange), ("max", (int) state.MaxRange));

        if (state.Target is { } target)
        {
            _aimed.Text = Loc.GetString("dh-mortar-ui-aimed", ("x", (int) MathF.Round(target.X)),
                ("y", (int) MathF.Round(target.Y)), ("distance", (int) Vector2.Distance(target, state.Position)));
        }
        else
        {
            _aimed.Text = Loc.GetString("dh-mortar-ui-not-aimed");
        }

        // Поля координат: при первом открытии — точка прицеливания или своё место.
        if (!_filled)
        {
            var start = state.Target ?? state.Position;
            _x.Text = MathF.Round(start.X).ToString("0", CultureInfo.InvariantCulture);
            _y.Text = MathF.Round(start.Y).ToString("0", CultureInfo.InvariantCulture);
            _filled = true;
        }

        _marks.RemoveAllChildren();
        foreach (var mark in state.Marks)
        {
            var inRange = mark.Distance >= state.MinRange && mark.Distance <= state.MaxRange;
            var button = new Button
            {
                Text = Loc.GetString("dh-mortar-ui-mark", ("code", mark.Code), ("x", (int) MathF.Round(mark.Position.X)),
                    ("y", (int) MathF.Round(mark.Position.Y)), ("distance", (int) mark.Distance)),
                Disabled = !inRange,
                HorizontalExpand = true,
            };
            var position = mark.Position;
            button.OnPressed += _ =>
            {
                _x.Text = MathF.Round(position.X).ToString("0", CultureInfo.InvariantCulture);
                _y.Text = MathF.Round(position.Y).ToString("0", CultureInfo.InvariantCulture);
                OnAim?.Invoke(position);
            };
            _marks.AddChild(button);
        }

        if (state.Marks.Count == 0)
            _marks.AddChild(new Label { Text = Loc.GetString("dh-mortar-ui-no-marks"), FontColorOverride = Dim });
    }
}
