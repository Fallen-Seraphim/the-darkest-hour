using Content.Shared._DarkestHour.Power.Fusion;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Power.Fusion;

/// <summary>
/// Control window of the fusion reactor. Built in code, no XAML.
/// </summary>
public sealed class FusionReactorWindow : DefaultWindow
{
    public event Action<bool>? OnToggle;
    public event Action<float>? OnSetPower;

    private readonly Label _status = new();
    private readonly Label _fuel = new();
    private readonly Label _power = new();
    private readonly Label _heat = new();
    private readonly Label _core = new();
    private readonly Label _field = new();
    private readonly Label _gas = new();
    private readonly ProgressBar _coreBar = new() { MinValue = 0f, MaxValue = 1f, MinHeight = 14f, HorizontalExpand = true };
    private readonly ProgressBar _fieldBar = new() { MinValue = 0f, MaxValue = 100f, MinHeight = 14f, HorizontalExpand = true };
    private readonly Button _toggle = new() { HorizontalExpand = true };

    private float _powerSetting;
    private bool _active;

    public FusionReactorWindow()
    {
        Title = Loc.GetString("fusion-reactor-ui-title");
        MinSize = new System.Numerics.Vector2(380, 340);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(6),
        };

        root.AddChild(_status);
        root.AddChild(_fuel);

        root.AddChild(_core);
        root.AddChild(_coreBar);
        root.AddChild(_field);
        root.AddChild(_fieldBar);
        root.AddChild(_heat);
        root.AddChild(_gas);

        var powerRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
        };
        powerRow.AddChild(PowerButton("-10", -0.10f));
        powerRow.AddChild(PowerButton("-1", -0.01f));
        _power.HorizontalExpand = true;
        _power.Align = Label.AlignMode.Center;
        powerRow.AddChild(_power);
        powerRow.AddChild(PowerButton("+1", 0.01f));
        powerRow.AddChild(PowerButton("+10", 0.10f));
        root.AddChild(powerRow);

        _toggle.OnPressed += _ => OnToggle?.Invoke(!_active);
        root.AddChild(_toggle);

        Contents.AddChild(root);
    }

    private Button PowerButton(string text, float delta)
    {
        var button = new Button { Text = text, MinWidth = 44f };
        button.OnPressed += _ => OnSetPower?.Invoke(Math.Clamp(_powerSetting + delta, 0f, 1f));
        return button;
    }

    public void UpdateState(FusionReactorBuiState state)
    {
        _active = state.Active;
        _powerSetting = state.PowerSetting;

        string status;
        if (state.Breached)
            status = Loc.GetString("fusion-reactor-ui-status-breached");
        else if (!state.Active)
            status = Loc.GetString("fusion-reactor-ui-status-off");
        else if (!state.Powered)
            status = Loc.GetString("fusion-reactor-ui-status-no-power");
        else if (!state.HasCassette || state.Fuel <= 0f)
            status = Loc.GetString("fusion-reactor-ui-status-no-fuel");
        else
            status = Loc.GetString("fusion-reactor-ui-status-running");
        _status.Text = status;

        _fuel.Text = state.HasCassette
            ? Loc.GetString("fusion-reactor-ui-fuel",
                ("percent", MathF.Round(state.FuelMax > 0f ? state.Fuel / state.FuelMax * 100f : 0f)))
            : Loc.GetString("fusion-reactor-ui-fuel-none");

        _power.Text = Loc.GetString("fusion-reactor-ui-power", ("percent", MathF.Round(state.PowerSetting * 100f)));
        _heat.Text = Loc.GetString("fusion-reactor-ui-heat",
            ("output", MathF.Round(state.HeatOutput / 1000f)),
            ("max", MathF.Round(state.MaxHeatOutput / 1000f)));

        _core.Text = Loc.GetString("fusion-reactor-ui-core",
            ("temperature", MathF.Round(state.CoreTemperature)),
            ("safe", MathF.Round(state.SafeTemperature)));
        _coreBar.Value = state.CriticalTemperature > 0f
            ? Math.Clamp(state.CoreTemperature / state.CriticalTemperature, 0f, 1f)
            : 0f;

        _field.Text = Loc.GetString("fusion-reactor-ui-field", ("integrity", MathF.Round(state.Integrity)));
        _fieldBar.Value = state.Integrity;

        _gas.Text = Loc.GetString("fusion-reactor-ui-gas",
            ("inlet", MathF.Round(state.InletTemperature)),
            ("outlet", MathF.Round(state.OutletTemperature)));

        _toggle.Text = Loc.GetString(state.Active ? "fusion-reactor-ui-turn-off" : "fusion-reactor-ui-turn-on");
        _toggle.Disabled = state.Breached;
    }
}
