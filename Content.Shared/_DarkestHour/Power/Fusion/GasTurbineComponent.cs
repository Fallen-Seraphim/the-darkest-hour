using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Power.Fusion;

/// <summary>
/// Gas turbine. Hot gas from the inlet port passes through the turbine and leaves cooler through the outlet port;
/// part of the heat taken from the gas becomes electricity on the HV network.
/// The hotter and denser the incoming gas, the more power. The exhaust is still warm and must be vented or cooled
/// before it goes back into the reactor.
/// </summary>
[RegisterComponent]
public sealed partial class GasTurbineComponent : Component
{
    /// <summary>
    /// Volume of gas pulled through the turbine per second, in litres.
    /// </summary>
    [DataField]
    public float FlowRate = 400f;

    /// <summary>
    /// How far the gas cools towards <see cref="ExhaustReferenceTemperature"/>, 0..1.
    /// </summary>
    [DataField]
    public float ExpansionFraction = 0.5f;

    /// <summary>
    /// Share of the heat taken from the gas that becomes electricity, 0..1.
    /// </summary>
    [DataField]
    public float Efficiency = 0.5f;

    /// <summary>
    /// Temperature the gas expands towards, K. Gas at or below it gives no power.
    /// </summary>
    [DataField]
    public float ExhaustReferenceTemperature = 293.15f;

    /// <summary>
    /// How fast the reported power output follows the actual output, per second.
    /// </summary>
    [DataField]
    public float PowerSmoothing = 0.5f;

    /// <summary>
    /// Power output above which the rotor is shown spinning, W.
    /// </summary>
    [DataField]
    public float SpinThreshold = 1_000f;

    /// <summary>
    /// Inlet gas temperature above which the blades get damaged, K.
    /// </summary>
    [DataField]
    public float MaxInletTemperature = 3500f;

    /// <summary>
    /// Integrity lost per second when the inlet gas is twice as hot as <see cref="MaxInletTemperature"/>.
    /// </summary>
    [DataField]
    public float OverheatDamage = 20f;

    [DataField]
    public float MaxIntegrity = 100f;

    /// <summary>
    /// Integrity restored by one welding repair.
    /// </summary>
    [DataField]
    public float RepairAmount = 20f;

    /// <summary>
    /// A broken turbine starts working again once repaired to this integrity.
    /// </summary>
    [DataField]
    public float RestartIntegrity = 40f;

    [DataField]
    public float RepairDelay = 5f;

    [DataField]
    public float RepairFuelCost = 5f;

    [DataField]
    public ProtoId<ToolQualityPrototype> RepairQuality = "Welding";

    [DataField]
    public float Integrity = 100f;

    [DataField]
    public bool Broken;

    /// <summary>
    /// Current electric output, W.
    /// </summary>
    [ViewVariables]
    public float PowerOutput;

    [ViewVariables]
    public float LastInletTemperature;
}
