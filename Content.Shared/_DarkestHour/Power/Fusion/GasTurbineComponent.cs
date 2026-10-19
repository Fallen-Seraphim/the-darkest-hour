using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Power.Fusion;

/// <summary>
/// Gas turbine with two separate gas circuits.
/// Hot gas from the reactor passes the hot side, cold gas from the cooling loop passes the cold side.
/// Heat flows from the hot gas into the cold gas, and part of it becomes electricity on the HV network.
/// The share that becomes electricity grows with the temperature difference (Carnot-like),
/// so the turbine makes nothing without a working, well-cooled cold loop.
/// </summary>
[RegisterComponent]
public sealed partial class GasTurbineComponent : Component
{
    /// <summary>
    /// Volume of hot gas pulled through the hot side per second, in litres.
    /// </summary>
    [DataField]
    public float HotFlowRate = 2500f;

    /// <summary>
    /// Volume of cold gas pulled through the cold side per second, in litres.
    /// </summary>
    [DataField]
    public float ColdFlowRate = 2500f;

    /// <summary>
    /// Both sides keep moving gas until their outlet reaches this pressure, kPa.
    /// </summary>
    [DataField]
    public float MaxOutletPressure = 15000f;

    /// <summary>
    /// How close the two gases get to a common temperature while passing each other, 0..1.
    /// </summary>
    [DataField]
    public float HeatExchange = 0.8f;

    /// <summary>
    /// Efficiency at an infinitely cold cold side. The actual efficiency is
    /// <c>MaxEfficiency * (1 - Tcold / Thot)</c>.
    /// </summary>
    [DataField]
    public float MaxEfficiency = 0.7f;

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
    /// Hot inlet gas temperature above which the blades get damaged, K.
    /// </summary>
    [DataField]
    public float MaxInletTemperature = 3500f;

    /// <summary>
    /// Integrity lost per second when the hot gas is twice as hot as <see cref="MaxInletTemperature"/>.
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
    public float LastEfficiency;

    [ViewVariables]
    public float LastInletTemperature;

    [ViewVariables]
    public float LastColdTemperature;

    [ViewVariables]
    public float LastInletPressure;

    [ViewVariables]
    public float LastOutletPressure;

    [ViewVariables]
    public float LastColdInletPressure;

    [ViewVariables]
    public float LastColdOutletPressure;

    /// <summary>
    /// Hot gas flow, mol/s.
    /// </summary>
    [ViewVariables]
    public float LastMolesPerSecond;

    /// <summary>
    /// Cold gas flow, mol/s.
    /// </summary>
    [ViewVariables]
    public float LastColdMolesPerSecond;
}
