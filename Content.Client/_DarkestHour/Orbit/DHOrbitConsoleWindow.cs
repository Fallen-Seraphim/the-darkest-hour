using Content.Client._DarkestHour.UI;
using System.Numerics;
using Content.Shared._DarkestHour.Orbit;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Orbit;

/// <summary>
/// Компьютер орбиты: схема орбиты над спутником, высота и её изменение, пороги кодов угрозы, двигатели и тяга.
/// </summary>
public sealed class DHOrbitConsoleWindow : DefaultWindow
{
    public event Action<bool>? OnSetBurn;
    public event Action<float>? OnSetThrottle;

    private readonly DHOrbitDiagram _diagram = new() { MinSize = new Vector2(220, 220) };
    private readonly Label _status = new();
    private readonly Label _altitude = new();
    private readonly Label _rate = new();
    private readonly Label _thresholds = new();
    private readonly Label _thrusters = new();
    private readonly Label _flow = new();
    private readonly Label _throttle = new() { HorizontalExpand = true, Align = Label.AlignMode.Center };
    private readonly Label _warning = new() { FontColorOverride = Color.OrangeRed };
    private readonly Button _burn = new() { HorizontalExpand = true };

    private bool _burning;
    private float _throttleValue = 1f;

    public DHOrbitConsoleWindow()
    {
        DHFonts.RegisterTerminal(this); // DarkestHour: терминальный шрифт
        Title = Loc.GetString("dh-orbit-ui-title");
        MinSize = new Vector2(520, 320);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(6),
        };

        root.AddChild(_diagram);

        var info = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };

        info.AddChild(_status);
        info.AddChild(_altitude);
        info.AddChild(_rate);
        info.AddChild(_thresholds);
        info.AddChild(_warning);
        info.AddChild(_thrusters);
        info.AddChild(_flow);

        var throttleRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
        };
        throttleRow.AddChild(ThrottleButton("-25", -0.25f));
        throttleRow.AddChild(ThrottleButton("-5", -0.05f));
        throttleRow.AddChild(_throttle);
        throttleRow.AddChild(ThrottleButton("+5", 0.05f));
        throttleRow.AddChild(ThrottleButton("+25", 0.25f));
        info.AddChild(throttleRow);

        _burn.OnPressed += _ => OnSetBurn?.Invoke(!_burning);
        info.AddChild(_burn);

        root.AddChild(info);
        Contents.AddChild(root);
    }

    private Button ThrottleButton(string text, float delta)
    {
        var button = new Button { Text = text, MinWidth = 44f };
        button.OnPressed += _ => OnSetThrottle?.Invoke(Math.Clamp(_throttleValue + delta, 0f, 1f));
        return button;
    }

    public void UpdateState(DHOrbitConsoleState state)
    {
        _burning = state.Burning;
        _throttleValue = state.Throttle;
        _diagram.State = state;

        if (state.Lost)
            _status.Text = Loc.GetString("dh-orbit-ui-status-lost");
        else if (state.Altitude <= state.TerrinumAltitude)
            _status.Text = Loc.GetString("dh-orbit-ui-status-decaying");
        else
            _status.Text = Loc.GetString("dh-orbit-ui-status-stable");

        _altitude.Text = Loc.GetString("dh-orbit-ui-altitude", ("body", state.Body), ("altitude", MathF.Round(state.Altitude, 1)));
        _rate.Text = Loc.GetString("dh-orbit-ui-rate", ("rate", MathF.Round(state.RatePerMinute, 2)));
        _thresholds.Text = Loc.GetString("dh-orbit-ui-thresholds",
            ("terrinum", MathF.Round(state.TerrinumAltitude)),
            ("ex", MathF.Round(state.ExAltitude)));

        _warning.Visible = state.ImpactIn != null;
        if (state.ImpactIn is { } impact)
            _warning.Text = Loc.GetString("dh-orbit-ui-impact", ("time", $"{(int) impact.TotalMinutes:00}:{impact.Seconds:00}"));

        _thrusters.Text = Loc.GetString("dh-orbit-ui-thrusters", ("working", state.ThrustersWorking), ("total", state.ThrustersTotal));
        _flow.Text = Loc.GetString("dh-orbit-ui-flow", ("moles", MathF.Round(state.MolesPerSecond, 2)));
        _throttle.Text = Loc.GetString("dh-orbit-ui-throttle", ("percent", MathF.Round(state.Throttle * 100f)));

        _burn.Text = Loc.GetString(state.Burning ? "dh-orbit-ui-burn-stop" : "dh-orbit-ui-burn-start");
        _burn.Disabled = state.Lost;
    }
}

/// <summary>Схема: спутник, пороги «Терринум» и «Экс», орбита станции и сама станция.</summary>
public sealed class DHOrbitDiagram : Control
{
    private static readonly Color MoonColor = Color.FromHex("#CFE6F2");
    private static readonly Color TerrinumColor = Color.PaleVioletRed;
    private static readonly Color ExColor = Color.DarkRed;
    private static readonly Color OrbitColor = Color.FromHex("#8FA6C8");

    public DHOrbitConsoleState? State;

    /// <summary>Радиус спутника на схеме в километрах: высоты орбиты рисуются в том же масштабе.</summary>
    private const float MoonRadiusKm = 400f;

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = (Vector2) PixelSize;
        var center = size / 2f;
        handle.DrawRect(new UIBox2(Vector2.Zero, size), Color.FromHex("#0B0F16"));

        if (State == null)
            return;

        var maxKm = MoonRadiusKm + MathF.Max(State.MaxAltitude, State.Altitude) + 40f;
        var scale = MathF.Min(size.X, size.Y) / 2f / maxKm;

        handle.DrawCircle(center, MoonRadiusKm * scale, MoonColor.WithAlpha(0.35f));
        handle.DrawCircle(center, MoonRadiusKm * scale, MoonColor, false);
        handle.DrawCircle(center, (MoonRadiusKm + State.ExAltitude) * scale, ExColor.WithAlpha(0.8f), false);
        handle.DrawCircle(center, (MoonRadiusKm + State.TerrinumAltitude) * scale, TerrinumColor.WithAlpha(0.8f), false);

        var orbitRadius = (MoonRadiusKm + MathF.Max(0f, State.Altitude)) * scale;
        handle.DrawCircle(center, orbitRadius, OrbitColor.WithAlpha(0.6f), false);

        var stationColor = State.Lost ? ExColor : State.Altitude <= State.TerrinumAltitude ? TerrinumColor : Color.Lime;
        var station = center + new Vector2(0f, -orbitRadius);
        handle.DrawCircle(station, 4f, stationColor);

        // Стрелка скорости изменения высоты.
        if (MathF.Abs(State.RatePerMinute) > 0.01f)
        {
            var arrow = Math.Clamp(State.RatePerMinute * 6f, -30f, 30f);
            handle.DrawLine(station, station + new Vector2(0f, -arrow), stationColor);
        }
    }
}
