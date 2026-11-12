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
    public event Action<Vector2>? OnPassPoint;
    public event Action? OnClearPass;
    public event Action? OnStartPass;
    public event Action<int>? OnAltitude;
    public event Action<bool>? OnCannon;
    public event Action? OnDrop;
    public event Action<int>? OnSelectHardpoint;
    public event Action<DHAircraftMapMode>? OnMapMode;
    public event Action<Vector2>? OnTarget;
    public event Action? OnFire;

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
    private readonly Label _weapons = new();
    private readonly Label _passInfo = new();
    private readonly Label _altitude = new() { MinWidth = 150 };
    private readonly Button _lower = new() { Text = "▼" };
    private readonly Button _higher = new() { Text = "▲" };
    private readonly Button _startPass = new();
    private readonly Button _clearPass = new();
    private readonly Button _cannon = new();
    private readonly Button _drop = new();
    private readonly Button[] _pylons = new Button[4];
    private readonly Button _modePass = new() { ToggleMode = true };
    private readonly Button _modeTarget = new() { ToggleMode = true };
    private readonly Button _fire = new();
    private readonly Label _target = new();
    private readonly Label _passBlocker = new() { FontColorOverride = Color.FromHex("#e8a24a") };
    private readonly DHTacticalMapControl _map = new() { HorizontalExpand = true, VerticalExpand = true, MinHeight = 320 };

    private DHAircraftBuiState? _state;

    public DHAircraftWindow()
    {
        DHFonts.RegisterTerminal(this);
        MinSize = new Vector2(560, 760);
        SetSize = new Vector2(640, 900);

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

        root.AddChild(_weapons);
        var altitude = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        altitude.AddChild(_altitude);
        altitude.AddChild(_lower);
        altitude.AddChild(_higher);
        root.AddChild(altitude);
        var pass = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        pass.AddChild(_startPass);
        pass.AddChild(_clearPass);
        pass.AddChild(_cannon);
        pass.AddChild(_drop);
        root.AddChild(pass);
        var pylons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        for (var i = 0; i < _pylons.Length; i++)
        {
            var slot = i;
            _pylons[i] = new Button { ToggleMode = true, MinWidth = 90 };
            _pylons[i].OnPressed += _ => OnSelectHardpoint?.Invoke(slot);
            pylons.AddChild(_pylons[i]);
        }

        pylons.AddChild(_fire);
        root.AddChild(pylons);

        var mode = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        mode.AddChild(new Label { Text = Loc.GetString("dh-aircraft-ui-map-mode") });
        mode.AddChild(_modePass);
        mode.AddChild(_modeTarget);
        root.AddChild(mode);
        root.AddChild(_target);
        root.AddChild(_passInfo);
        root.AddChild(_passBlocker);
        root.AddChild(_map);
        Contents.AddChild(root);

        _slower.OnPressed += _ => OnSpeed?.Invoke(Math.Max(0f, (_state?.Speed ?? 0.5f) - 0.1f));
        _faster.OnPressed += _ => OnSpeed?.Invoke(Math.Min(1f, (_state?.Speed ?? 0.5f) + 0.1f));
        _hook.OnPressed += _ => OnHook?.Invoke(!(_state?.Hooked ?? false));
        _launch.OnPressed += _ => OnLaunch?.Invoke();
        _return.OnPressed += _ => OnReturn?.Invoke();
        _control.OnPressed += _ => OnControl?.Invoke(!(_state?.Control ?? false));
        _lower.OnPressed += _ => OnAltitude?.Invoke(-1);
        _higher.OnPressed += _ => OnAltitude?.Invoke(1);
        _startPass.OnPressed += _ => OnStartPass?.Invoke();
        _clearPass.OnPressed += _ => OnClearPass?.Invoke();
        _cannon.OnPressed += _ => OnCannon?.Invoke(!(_state?.CannonOn ?? false));
        _drop.OnPressed += _ => OnDrop?.Invoke();
        _map.OnWorldClick += point =>
        {
            if (_state?.MapMode == DHAircraftMapMode.Target)
                OnTarget?.Invoke(point);
            else
                OnPassPoint?.Invoke(point);
        };
        _modePass.OnPressed += _ => OnMapMode?.Invoke(DHAircraftMapMode.Pass);
        _modeTarget.OnPressed += _ => OnMapMode?.Invoke(DHAircraftMapMode.Target);
        _fire.OnPressed += _ => OnFire?.Invoke();
    }

    public void UpdateState(DHAircraftBuiState state)
    {
        _state = state;
        var airborne = state.State is DHAircraftState.Airborne or DHAircraftState.Returning;

        _status.Text = Loc.GetString("dh-aircraft-ui-status", ("state", Loc.GetString("dh-aircraft-state-" + state.State.ToString().ToLowerInvariant())));
        _fuel.Text = Loc.GetString("dh-aircraft-ui-fuel", ("fuel", (int) state.Fuel), ("max", (int) state.FuelMax), ("takeoff", (int) state.TakeoffFuel));
        _integrity.Text = Loc.GetString("dh-aircraft-ui-integrity", ("integrity", (int) state.Integrity), ("max", (int) state.MaxIntegrity));
        _flares.Text = Loc.GetString(!state.Modules.Contains(DHAircraftModule.FlareDispenser) ? "dh-aircraft-ui-flares-none"
                : state.FlaresActive ? "dh-aircraft-ui-flares-active" : "dh-aircraft-ui-flares",
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

        var inPass = state.State == DHAircraftState.Pass;
        var loaded = state.Hardpoints.FindAll(h => h != DHAircraftMunition.None).Count;
        var pods = state.Hardpoints.FindAll(h => h == DHAircraftMunition.CannonPod).Count;
        var bombs = state.Hardpoints.FindAll(h => h is not (DHAircraftMunition.None or DHAircraftMunition.CannonPod)).Count;
        var modules = state.Modules.FindAll(m => m != DHAircraftModule.None);
        _weapons.Text = Loc.GetString("dh-aircraft-ui-weapons", ("ammo", state.CannonAmmo), ("maxAmmo", state.MaxCannonAmmo),
            ("pods", pods),
            ("pylons", string.Join(" ", state.Hardpoints.ConvertAll(h => Loc.GetString("dh-aircraft-munition-" + h.ToString().ToLowerInvariant())))),
            ("loaded", loaded)) + "\n" + Loc.GetString("dh-aircraft-ui-modules",
            ("modules", modules.Count == 0
                ? Loc.GetString("dh-aircraft-module-none")
                : string.Join(", ", modules.ConvertAll(m => Loc.GetString("dh-aircraft-module-" + m.ToString().ToLowerInvariant()))))));
        _altitude.Text = Loc.GetString("dh-aircraft-ui-altitude", ("altitude", Loc.GetString("dh-aircraft-altitude-name-" + state.Altitude.ToString().ToLowerInvariant())));
        _startPass.Text = Loc.GetString("dh-aircraft-ui-start-pass");
        _startPass.Disabled = !state.CanPass;
        _clearPass.Text = Loc.GetString("dh-aircraft-ui-clear-pass");
        _clearPass.Disabled = inPass || state.PassStart == null;
        _cannon.Text = Loc.GetString(state.CannonOn ? "dh-aircraft-ui-cannon-off" : "dh-aircraft-ui-cannon-on");
        _cannon.Disabled = pods == 0;
        _drop.Text = Loc.GetString("dh-aircraft-ui-drop");
        _drop.Disabled = !inPass || bombs == 0;
        _passInfo.Text = state.MapMode == DHAircraftMapMode.Target
            ? Loc.GetString("dh-aircraft-ui-target-hint")
            : Loc.GetString(state.PassStart == null ? "dh-aircraft-ui-pass-hint-start"
                : state.PassEnd == null ? "dh-aircraft-ui-pass-hint-end" : "dh-aircraft-ui-pass-ready");
        _passBlocker.Text = airborne && !inPass && state.PassBlocker is { } passBlocker ? Loc.GetString(passBlocker) : string.Empty;

        for (var i = 0; i < _pylons.Length; i++)
        {
            var munition = i < state.Hardpoints.Count ? state.Hardpoints[i] : DHAircraftMunition.None;
            _pylons[i].Text = munition == DHAircraftMunition.CannonPod
                ? Loc.GetString("dh-aircraft-ui-pylon-pod", ("slot", i + 1),
                    ("rounds", i < state.HardpointAmmo.Count ? state.HardpointAmmo[i] : 0))
                : Loc.GetString("dh-aircraft-ui-pylon", ("slot", i + 1),
                    ("munition", Loc.GetString("dh-aircraft-munition-" + munition.ToString().ToLowerInvariant())));
            _pylons[i].Pressed = state.SelectedHardpoint == i;
        }

        _fire.Text = Loc.GetString("dh-aircraft-ui-fire");
        _modePass.Text = Loc.GetString("dh-aircraft-ui-map-mode-pass");
        _modePass.Pressed = state.MapMode == DHAircraftMapMode.Pass;
        _modeTarget.Text = Loc.GetString("dh-aircraft-ui-map-mode-target");
        _modeTarget.Pressed = state.MapMode == DHAircraftMapMode.Target;
        _target.Text = state.TargetLabel is { } label
            ? Loc.GetString("dh-aircraft-ui-target", ("target", label))
            : Loc.GetString("dh-aircraft-ui-target-none");

        _map.Target = state.TargetPoint;
        _map.Signatures = state.Signatures.ConvertAll(s => (s.Position,
            Loc.GetString("dh-aircraft-signature-" + s.Kind.ToString().ToLowerInvariant())));

        if (state.Map is { } map)
            _map.SetState(map);
        _map.Self = state.Position;
        _map.PathStart = state.PassStart;
        _map.PathEnd = state.PassEnd;
    }
}
