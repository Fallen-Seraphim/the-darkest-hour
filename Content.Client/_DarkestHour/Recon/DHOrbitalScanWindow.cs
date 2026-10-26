using System.Globalization;
using System.Numerics;
using Content.Shared._DarkestHour.Recon;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Recon;

/// <summary>Орбитальный скан: поверхность под авианосцем, точка (координаты или метка ЛЦУ), готовность, скан.</summary>
public sealed class DHOrbitalScanWindow : DefaultWindow
{
    public event Action<Vector2>? OnScan;

    private static readonly Color Ok = Color.FromHex("#8FD66B");
    private static readonly Color Warn = Color.FromHex("#E8B04A");
    private static readonly Color Dim = Color.FromHex("#8A8A8A");

    private readonly Label _surface = new();
    private readonly Label _status = new();
    private readonly LineEdit _x = new() { Text = "0", MinWidth = 80 };
    private readonly LineEdit _y = new() { Text = "0", MinWidth = 80 };
    private readonly Button _scan = new() { HorizontalExpand = true };
    private readonly BoxContainer _marks = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };

    public DHOrbitalScanWindow()
    {
        Title = Loc.GetString("dh-scan-ui-title");
        MinSize = new Vector2(380, 340);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        root.AddChild(_surface);
        root.AddChild(_status);

        var coords = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        coords.AddChild(new Label { Text = "X" });
        coords.AddChild(_x);
        coords.AddChild(new Label { Text = "Y" });
        coords.AddChild(_y);
        root.AddChild(coords);

        _scan.Text = Loc.GetString("dh-scan-ui-scan");
        _scan.OnPressed += _ => OnScan?.Invoke(new Vector2(Parse(_x.Text), Parse(_y.Text)));
        root.AddChild(_scan);

        root.AddChild(new Label { Text = Loc.GetString("dh-scan-ui-marks"), FontColorOverride = Warn });
        root.AddChild(_marks);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(root);
        Contents.AddChild(scroll);
    }

    private static float Parse(string text)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
    }

    public void UpdateState(DHOrbitalScanState state)
    {
        _surface.Text = string.IsNullOrEmpty(state.Surface)
            ? Loc.GetString("dh-scan-ui-no-surface")
            : Loc.GetString("dh-scan-ui-surface", ("surface", state.Surface), ("radius", (int) state.Radius));
        _status.Text = state.Status;
        _status.FontColorOverride = state.Ready ? Ok : Warn;
        _scan.Disabled = !state.Ready;

        _marks.RemoveAllChildren();
        foreach (var mark in state.Marks)
        {
            var button = new Button
            {
                Text = Loc.GetString("dh-scan-ui-mark", ("code", mark.Code), ("x", (int) MathF.Round(mark.Position.X)),
                    ("y", (int) MathF.Round(mark.Position.Y))),
                HorizontalExpand = true,
            };
            var position = mark.Position;
            button.OnPressed += _ =>
            {
                _x.Text = MathF.Round(position.X).ToString("0", CultureInfo.InvariantCulture);
                _y.Text = MathF.Round(position.Y).ToString("0", CultureInfo.InvariantCulture);
            };
            _marks.AddChild(button);
        }

        if (state.Marks.Count == 0)
            _marks.AddChild(new Label { Text = Loc.GetString("dh-scan-ui-no-marks"), FontColorOverride = Dim });
    }
}
