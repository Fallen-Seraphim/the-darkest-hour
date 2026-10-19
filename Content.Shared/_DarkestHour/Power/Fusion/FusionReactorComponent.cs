using Content.Shared.Explosion;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Power.Fusion;

/// <summary>
/// Compact fusion reactor. Gas from the inlet port passes through the reactor core and leaves heated
/// through the outlet port. The core is heated by burning the fuel cassette; the containment field
/// needs APC power and weakens when the core overheats. If the field collapses, the reactor breaches.
/// </summary>
[RegisterComponent]
public sealed partial class FusionReactorComponent : Component
{
    #region Settings

    /// <summary>
    /// Id of the item slot holding the fuel cassette.
    /// </summary>
    [DataField]
    public string FuelSlotId = "fuel_slot";

    /// <summary>
    /// Heat output at 100% power, in watts (scaled atmos heat, like the TEG and thermomachines).
    /// </summary>
    [DataField]
    public float MaxHeatOutput = 2_000_000f;

    /// <summary>
    /// How fast the heat output follows the power setting, as a fraction of <see cref="MaxHeatOutput"/> per second.
    /// </summary>
    [DataField]
    public float RampRate = 0.05f;

    /// <summary>
    /// Fuel units burned per second at 100% power.
    /// </summary>
    [DataField]
    public float FuelBurnRate = 0.055f;

    /// <summary>
    /// Heat capacity of the reactor core, J/K.
    /// </summary>
    [DataField]
    public float CoreHeatCapacity = 50_000f;

    /// <summary>
    /// Fraction of the core heat lost to the surroundings per second (very small).
    /// </summary>
    [DataField]
    public float PassiveCooling = 0.002f;

    /// <summary>
    /// Volume of gas pulled through the core per second, in litres.
    /// </summary>
    [DataField]
    public float FlowRate = 600f;

    /// <summary>
    /// The reactor circulator keeps pushing gas until the outlet reaches this pressure, kPa.
    /// Hot gas is denser in pressure than cold gas, so this is well above a normal pump's 4500 kPa.
    /// </summary>
    [DataField]
    public float MaxOutletPressure = 15000f;

    /// <summary>
    /// How close the passing gas gets to thermal equilibrium with the core, 0..1.
    /// </summary>
    [DataField]
    public float HeatTransferEfficiency = 0.85f;

    /// <summary>
    /// Core temperature above which the containment field starts to weaken, K.
    /// </summary>
    [DataField]
    public float SafeTemperature = 3000f;

    /// <summary>
    /// Core temperature at which the field loses <see cref="OverheatFieldLoss"/> per second, K. Losses keep growing above it.
    /// </summary>
    [DataField]
    public float CriticalTemperature = 6000f;

    /// <summary>
    /// Field integrity lost per second at <see cref="CriticalTemperature"/>.
    /// </summary>
    [DataField]
    public float OverheatFieldLoss = 10f;

    /// <summary>
    /// Field integrity lost per second while the reactor runs without power.
    /// </summary>
    [DataField]
    public float UnpoweredFieldLoss = 2f;

    /// <summary>
    /// Field integrity restored per second while powered and not overheating.
    /// </summary>
    [DataField]
    public float FieldRegeneration = 1f;

    /// <summary>
    /// APC load while switched off, W.
    /// </summary>
    [DataField]
    public float IdlePowerLoad = 1_000f;

    /// <summary>
    /// APC load of the containment field while switched on, W.
    /// </summary>
    [DataField]
    public float ActivePowerLoad = 30_000f;

    [DataField]
    public ProtoId<RadioChannelPrototype> AlertChannel = "Engineering";

    #endregion

    #region Breach

    [DataField]
    public ProtoId<ExplosionPrototype> BreachExplosionType = "Default";

    [DataField]
    public float BreachExplosionIntensity = 400f;

    [DataField]
    public float BreachExplosionSlope = 4f;

    [DataField]
    public float BreachExplosionMaxTileIntensity = 20f;

    [DataField]
    public float BreachEmpRange = 12f;

    [DataField]
    public float BreachEmpEnergy = 50_000f;

    [DataField]
    public float BreachEmpDuration = 30f;

    /// <summary>
    /// Moles of hot tritium released into the room on breach.
    /// </summary>
    [DataField]
    public float BreachTritiumMoles = 60f;

    #endregion

    #region State

    /// <summary>
    /// Whether the operator has switched the reactor on.
    /// </summary>
    [DataField]
    public bool Active;

    /// <summary>
    /// Target power, 0..1.
    /// </summary>
    [DataField]
    public float PowerSetting = 0.5f;

    /// <summary>
    /// Current heat output, W.
    /// </summary>
    [DataField]
    public float HeatOutput;

    [DataField]
    public float CoreTemperature = 293.15f;

    /// <summary>
    /// Containment field integrity, 0..100.
    /// </summary>
    [DataField]
    public float Integrity = 100f;

    /// <summary>
    /// The containment field collapsed. The reactor is wrecked and does nothing.
    /// </summary>
    [DataField]
    public bool Breached;

    [ViewVariables]
    public float LastInletTemperature;

    [ViewVariables]
    public float LastOutletTemperature;

    [ViewVariables]
    public float LastInletPressure;

    [ViewVariables]
    public float LastOutletPressure;

    /// <summary>
    /// Coolant flow through the core, mol/s.
    /// </summary>
    [ViewVariables]
    public float LastMolesPerSecond;

    [ViewVariables]
    public bool Powered;

    [ViewVariables]
    public bool SlotLocked;

    /// <summary>
    /// Last warning level announced over the radio, so each warning is sent once.
    /// 0 = none, 1 = overheating, 2 = field below 50%, 3 = field below 20%.
    /// </summary>
    [ViewVariables]
    public int WarningLevel;

    #endregion
}
