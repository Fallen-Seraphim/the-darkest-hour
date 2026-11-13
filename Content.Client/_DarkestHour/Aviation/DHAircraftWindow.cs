using System.Numerics;
using Content.Client._DarkestHour.TacticalMap;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Aviation;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Aviation;

/// <summary>
/// Интерфейс полёта штурмовика: состояние, топливо, прочность, ЛТЦ, скорость; на палубе — катапульта и взлёт,
/// в воздухе — ручка (WASD), высота и линия бреющего захода на тактической карте, отстрел ЛТЦ и автопилот посадки.
/// Вооружение — в отдельном окне (кнопка «Вооружение», решение автора 2026-11-13).
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
    public event Action? OnFlares;
    public event Action? OnWeapons;

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
    private readonly Button _fireFlares = new();
    private readonly Button _weaponsButton = new();
    private readonly Label _weapons = new();
    private readonly Label _passInfo = new();
    private readonly Label _altitude = new() { MinWidth = 150 };
    private readonly Button _lower = new() { Text = "▼" };
    private readonly Button _higher = new() { Text = "▲" };
    private readonly Button _startPass = new();
    private readonly Button _clearPass = new();
    private readonly Label _passBlocker = new() { FontColorOverride = Color.FromHex("#e8a24a") };
    private readonly DHTacticalMapControl _map = new() { HorizontalExpand = true, VerticalExpand = true, MinHeight = 320 };

    private DHAircraftBuiState? _state;

    public DHAircraftWindow()
    {
        DHFonts.RegisterTerminal(this);
        MinSize = new Vector2(560, 700);
        SetSize = new Vector2(640, 840);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4, Margin = new Thickness(6) };
        root.AddChild(_status);
        root.AddChild(_fuel);
        root.AddChild(_integrity);

        var flares = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        flares.AddChild(_flares);
        flares.AddChild(_fireFlares);
        root.AddChild(flares);
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

        var weapons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        weapons.AddChild(_weaponsButton);
        weapons.AddChild(_weapons);
        root.AddChild(weapons);

        var altitude = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        altitude.AddChild(_altitude);
        altitude.AddChild(_lower);
        altitude.AddChild(_higher);
        root.AddChild(altitude);
        var pass = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        pass.AddChild(_startPass);
        pass.AddChild(_clearPass);
        root.AddChild(pass);
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
        _fireFlares.OnPressed += _ => OnFlares?.Invoke();
        _weaponsButton.OnPressed += _ => OnWeapons?.Invoke();
        _map.OnWorldClick += point => OnPassPoint?.Invoke(point);
    }

    public void UpdateState(DHAircraftBuiState state)
    {
        _state = state;
        var airborne = state.State is DHAircraftState.Airborne or DHAircraftState.Returning;
        var flying = airborne || state.State == DHAircraftState.Pass;

        _status.Text = Loc.GetString("dh-aircraft-ui-status", ("state", Loc.GetString("dh-aircraft-state-" + state.State.ToString().ToLowerInvariant())));
        _fuel.Text = Loc.GetString("dh-aircraft-ui-fuel", ("fuel", (int) state.Fuel), ("max", (int) state.FuelMax), ("takeoff", (int) state.TakeoffFuel));
        _integrity.Text = Loc.GetString("dh-aircraft-ui-integrity", ("integrity", (int) state.Integrity), ("max", (int) state.MaxIntegrity));
        var dispenser = state.Modules.Contains(DHAircraftModule.FlareDispenser);
        _flares.Text = Loc.GetString(!dispenser ? "dh-aircraft-ui-flares-none"
                : state.FlaresActive ? "dh-aircraft-ui-flares-active" : "dh-aircraft-ui-flares",
            ("flares", state.Flares), ("max", state.MaxFlares));
        _fireFlares.Text = Loc.GetString("dh-aircraft-ui-fire-flares");
        _fireFlares.Disabled = !flying || !dispenser || state.Flares <= 0;
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
        var modules = state.Modules.FindAll(m => m != DHAircraftModule.None);
        _weaponsButton.Text = Loc.GetString("dh-aircraft-ui-open-weapons");
        _weapons.Text = Loc.GetString("dh-aircraft-ui-weapons", ("ammo", state.CannonAmmo), ("maxAmmo", state.MaxCannonAmmo),
            ("pods", pods),
            ("pylons", string.Join(" ", state.Hardpoints.ConvertAll(h => Loc.GetString("dh-aircraft-munition-" + h.ToString().ToLowerInvariant())))),
            ("loaded", loaded)) + "\n" + Loc.GetString("dh-aircraft-ui-modules",
            ("modules", modules.Count == 0
                ? Loc.GetString("dh-aircraft-module-none")
                : string.Join(", ", modules.ConvertAll(m => Loc.GetString("dh-aircraft-module-" + m.ToString().ToLowerInvariant())))));
        _altitude.Text = Loc.GetString("dh-aircraft-ui-altitude", ("altitude", Loc.GetString("dh-aircraft-altitude-name-" + state.Altitude.ToString().ToLowerInvariant())));
        _startPass.Text = Loc.GetString("dh-aircraft-ui-start-pass");
        _startPass.Disabled = !state.CanPass;
        _clearPass.Text = Loc.GetString("dh-aircraft-ui-clear-pass");
        _clearPass.Disabled = inPass || state.PassStart == null;
        _passInfo.Text = Loc.GetString(state.PassStart == null ? "dh-aircraft-ui-pass-hint-start"
            : state.PassEnd == null ? "dh-aircraft-ui-pass-hint-end" : "dh-aircraft-ui-pass-ready");
        _passBlocker.Text = airborne && !inPass && state.PassBlocker is { } passBlocker ? Loc.GetString(passBlocker) : string.Empty;

        _map.Target = state.TargetPoint;
        if (state.Map is { } map)
            _map.SetState(map);
        _map.Self = state.Position;
        _map.PathStart = state.PassStart;
        _map.PathEnd = state.PassEnd;
    }
}
