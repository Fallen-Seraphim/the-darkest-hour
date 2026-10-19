using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Power.Fusion;

[Serializable, NetSerializable]
public enum FusionReactorVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum FusionReactorVisualState : byte
{
    Off,
    Active,
    Unstable,
    Breached,
}

[Serializable, NetSerializable]
public enum FusionReactorVisualLayers : byte
{
    Body,
    Core,
}

[Serializable, NetSerializable]
public enum GasTurbineVisuals : byte
{
    Spinning,
    Broken,
}

[Serializable, NetSerializable]
public enum GasTurbineVisualLayers : byte
{
    Body,
    Rotor,
}

[Serializable, NetSerializable]
public enum FusionReactorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FusionReactorBuiState : BoundUserInterfaceState
{
    public bool Active;
    public bool Powered;
    public bool Breached;
    public bool HasCassette;
    public float Fuel;
    public float FuelMax;
    public float PowerSetting;
    public float HeatOutput;
    public float MaxHeatOutput;
    public float CoreTemperature;
    public float SafeTemperature;
    public float CriticalTemperature;
    public float Integrity;
    public float InletTemperature;
    public float OutletTemperature;
}

/// <summary>
/// Switch the reactor on or off.
/// </summary>
[Serializable, NetSerializable]
public sealed class FusionReactorToggleMessage : BoundUserInterfaceMessage
{
    public bool Active;

    public FusionReactorToggleMessage(bool active)
    {
        Active = active;
    }
}

/// <summary>
/// Set the target power, 0..1.
/// </summary>
[Serializable, NetSerializable]
public sealed class FusionReactorSetPowerMessage : BoundUserInterfaceMessage
{
    public float Power;

    public FusionReactorSetPowerMessage(float power)
    {
        Power = power;
    }
}

/// <summary>
/// Welding repair of a gas turbine.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class GasTurbineRepairDoAfterEvent : SimpleDoAfterEvent;
