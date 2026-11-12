using System.Numerics;
using Content.Client._DarkestHour.TacticalMap;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Aviation;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Aviation;

/// <summary>
/// Интерфейс полёта штурмовика: состояние, топливо, прочность, ЛТЦ, скорость; на палубе — катапульта и взлёт,
/// в воздухе — управление WASD, автопилот посадки и тактическая карта с отметкой самолёта.
/// </summary>
public sealed class DHAircraftWindow : DefaultWindow
{
    public event Action<bool>? OnHook;
    public event Action? OnLaunch;
    public event Action? OnReturn;
    public event Action<float>? OnSpeed;
    public event Action<bool>? OnControl;

    private readonly Label _status = new();
    private readonly Label _fuel = new();
    private readonly Label _integrity = new();
    private readonly Label _flares = new();
    private readonly Label _position = new();
    private readonly Label _blocker = new() { FontColorOverride = Color.FromHex("#e8a24a") };
    private readonly Label _speedLabel = new() { MinWidth = 110 };
    private readonly Button _hook = new();
    private readonly Button _launch = new();
    private readonly Button _return = new();
    private readonly Button _control = new();
    private readonly Button _slower = new() { Text = "−" };
    private readonly Button _faster = new() { Text = "+" };
    private readonly DHTacticalMapControl _map = new() { HorizontalExpand = true, VerticalExpand = true, MinHeight = 320 };

    private DHAircraftBuiState? _state;

    public DHAircraftWindow()
    {
        DHFonts.RegisterTerminal(this);
        MinSize = new Vector2(520, 640);
        SetSize = new Vector2(620, 760);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4, Margin = new Thickness(6) };
        root.AddChild(_status);
        root.AddChild(_fuel);
        root.AddChild(_integrity);
        root.AddChild(_flares);
        root.AddChild(_position);

        var speed = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        speed.AddChild(_speedLabel);
        speed.AddChild(_slower);
        speed.AddChild(_faster);
        root.AddChild(speed);

        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        buttons.AddChild(_hook);
        buttons.AddChild(_launch);
        buttons.AddChild(_return);
        buttons.AddChild(_control);
        root.AddChild(buttons);
        root.AddChild(_blocker);
        root.AddChild(_map);
        Contents.AddChild(root);

        _slower.OnPressed += _ => OnSpeed?.Invoke(Math.Max(0f, (_state?.Speed ?? 0.5f) - 0.1f));
        _faster.OnPressed += _ => OnSpeed?.Invoke(Math.Min(1f, (_state?.Speed ?? 0.5f) + 0.1f));
        _hook.OnPressed += _ => OnHook?.Invoke(!(_state?.Hooked ?? false));
        _launch.OnPressed += _ => OnLaunch?.Invoke();
        _return.OnPressed += _ => OnReturn?.Invoke();
        _control.OnPressed += _ => OnControl?.Invoke(!(_state?.Control ?? false));
    }

    public void UpdateState(DHAircraftBuiState state)
    {
        _state = state;
        var airborne = state.State is DHAircraftState.Airborne or DHAircraftState.Returning;

        _status.Text = Loc.GetString("dh-aircraft-ui-status", ("state", Loc.GetString("dh-aircraft-state-" + state.State.ToString().ToLowerInvariant())));
        _fuel.Text = Loc.GetString("dh-aircraft-ui-fuel", ("fuel", (int) state.Fuel), ("max", (int) state.FuelMax), ("takeoff", (int) state.TakeoffFuel));
        _integrity.Text = Loc.GetString("dh-aircraft-ui-integrity", ("integrity", (int) state.Integrity), ("max", (int) state.MaxIntegrity));
        _flares.Text = Loc.GetString(state.FlaresActive ? "dh-aircraft-ui-flares-active" : "dh-aircraft-ui-flares",
            ("flares", state.Flares), ("max", state.MaxFlares));
        _position.Text = state.Position is { } p
            ? Loc.GetString("dh-aircraft-ui-position", ("x", (int) p.X), ("y", (int) p.Y), ("heading", (int) ((360 - state.HeadingDegrees % 360 + 360) % 360)))
            : Loc.GetString("dh-aircraft-ui-position-deck");
        _speedLabel.Text = Loc.GetString("dh-aircraft-ui-speed", ("speed", (int) MathF.Round(state.Speed * 100)));

        _hook.Text = Loc.GetString(state.Hooked ? "dh-aircraft-ui-unhook" : "dh-aircraft-ui-hook");
        _hook.Disabled = state.State != DHAircraftState.Parked;
        _launch.Text = Loc.GetString("dh-aircraft-ui-launch");
        _launch.Disabled = !state.CanLaunch;
        _return.Text = Loc.GetString(state.State == DHAircraftState.Returning ? "dh-aircraft-ui-return-cancel" : "dh-aircraft-ui-return");
        _return.Disabled = !airborne;
        _control.Text = Loc.GetString(state.Control ? "dh-aircraft-ui-control-off" : "dh-aircraft-ui-control-on");
        _control.Disabled = !airborne;

        _blocker.Text = state.State == DHAircraftState.Parked && state.LaunchBlocker is { } blocker ? Loc.GetString(blocker) : string.Empty;

        if (state.Map is { } map)
            _map.SetState(map);
        _map.Self = state.Position;
    }
}
