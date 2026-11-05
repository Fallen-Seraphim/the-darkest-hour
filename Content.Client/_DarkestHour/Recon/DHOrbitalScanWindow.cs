using Content.Client._DarkestHour.UI;
using System.Numerics;
using Content.Shared._DarkestHour.Recon;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Recon;

/// <summary>Глобальный скан: поверхность под авианосцем, готовность, кнопка скана и последний отчёт.</summary>
public sealed class DHOrbitalScanWindow : DefaultWindow
{
    public event Action? OnScan;

    private static readonly Color Ok = Color.FromHex("#8FD66B");
    private static readonly Color Warn = Color.FromHex("#E8B04A");
    private static readonly Color Dim = Color.FromHex("#8A8A8A");

    private readonly Label _surface = new();
    private readonly Label _status = new();
    private readonly Button _scan = new() { HorizontalExpand = true };
    private readonly Label _report = new();

    public DHOrbitalScanWindow()
    {
        DHFonts.RegisterTerminal(this); // DarkestHour: терминальный шрифт
        Title = Loc.GetString("dh-scan-ui-title");
        MinSize = new Vector2(440, 380);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        root.AddChild(_surface);
        root.AddChild(_status);

        _scan.Text = Loc.GetString("dh-scan-ui-scan");
        _scan.OnPressed += _ => OnScan?.Invoke();
        root.AddChild(_scan);

        root.AddChild(new Label { Text = Loc.GetString("dh-scan-ui-report"), FontColorOverride = Warn });
        root.AddChild(_report);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(root);
        Contents.AddChild(scroll);
    }

    public void UpdateState(DHOrbitalScanState state)
    {
        _surface.Text = string.IsNullOrEmpty(state.Surface)
            ? Loc.GetString("dh-scan-ui-no-surface")
            : Loc.GetString("dh-scan-ui-surface", ("surface", state.Surface));
        _status.Text = state.Status;
        _status.FontColorOverride = state.Ready ? Ok : Warn;
        _scan.Disabled = !state.Ready;

        if (string.IsNullOrEmpty(state.Report))
        {
            _report.Text = Loc.GetString("dh-scan-ui-no-report");
            _report.FontColorOverride = Dim;
        }
        else
        {
            _report.Text = state.Report;
            _report.FontColorOverride = null;
        }
    }
}
