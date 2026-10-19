using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Atmos;

/// <summary>
/// Gives a large (multi-tile) machine a gas inlet and a gas outlet on tiles other than its origin.
/// The ports are separate invisible anchored entities with a single pipe node each,
/// spawned at <see cref="InletOffset"/> / <see cref="OutletOffset"/> (local space, rotated with the machine).
/// Pipes connect to a port from the neighbouring tile in the port's direction.
/// </summary>
[RegisterComponent]
public sealed partial class MachineGasPortsComponent : Component
{
    /// <summary>
    /// Prototype of the port entity. It must have a NodeContainer with a south-facing PipeNode named <see cref="PortNode"/>.
    /// </summary>
    [DataField]
    public EntProtoId PortPrototype = "DHMachineGasPort";

    /// <summary>
    /// Name of the pipe node on the port entity.
    /// </summary>
    [DataField]
    public string PortNode = "pipe";

    /// <summary>
    /// Tile offset of the inlet port from the machine origin, at rotation 0.
    /// </summary>
    [DataField]
    public Vector2 InletOffset = new(-1, 0);

    /// <summary>
    /// Direction the inlet faces (where the incoming pipe must be), at rotation 0.
    /// </summary>
    [DataField]
    public Direction InletDirection = Direction.West;

    /// <summary>
    /// Tile offset of the outlet port from the machine origin, at rotation 0.
    /// </summary>
    [DataField]
    public Vector2 OutletOffset = new(1, 0);

    /// <summary>
    /// Direction the outlet faces (where the outgoing pipe must be), at rotation 0.
    /// </summary>
    [DataField]
    public Direction OutletDirection = Direction.East;

    /// <summary>
    /// The machine has a second, separate gas circuit (for example the cold loop of a turbine).
    /// </summary>
    [DataField]
    public bool Secondary;

    [DataField]
    public Vector2 SecondaryInletOffset = new(0, -1);

    [DataField]
    public Direction SecondaryInletDirection = Direction.South;

    [DataField]
    public Vector2 SecondaryOutletOffset = new(0, 1);

    [DataField]
    public Direction SecondaryOutletDirection = Direction.North;

    /// <summary>
    /// The spawned inlet port. Saved with the map so the port is not duplicated on load.
    /// </summary>
    [DataField]
    public EntityUid? Inlet;

    /// <summary>
    /// The spawned outlet port. Saved with the map so the port is not duplicated on load.
    /// </summary>
    [DataField]
    public EntityUid? Outlet;

    [DataField]
    public EntityUid? SecondaryInlet;

    [DataField]
    public EntityUid? SecondaryOutlet;
}
