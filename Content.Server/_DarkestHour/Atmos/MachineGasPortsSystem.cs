using System.Diagnostics.CodeAnalysis;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared._DarkestHour.Atmos;
using Content.Shared.Atmos;
using Content.Shared.Examine;
using Robust.Shared.Map;

namespace Content.Server._DarkestHour.Atmos;

/// <summary>
/// Spawns, keeps and removes the gas port entities of large machines (see <see cref="MachineGasPortsComponent"/>).
/// </summary>
public sealed class MachineGasPortsSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private NodeContainerSystem _nodeContainer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MachineGasPortsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MachineGasPortsComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<MachineGasPortsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MachineGasPortsComponent, GasAnalyzerScanEvent>(OnAnalyze);
        SubscribeLocalEvent<MachineGasPortsComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<MachineGasPortsComponent> ent, ref MapInitEvent args)
    {
        EnsurePorts(ent);
    }

    private void OnAnchorChanged(Entity<MachineGasPortsComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored || args.Detaching)
        {
            DeletePorts(ent);
            return;
        }

        // Machines anchored while their map is being loaded or set up get their ports on MapInit
        // (or lazily on the first atmos update).
        if (MetaData(ent).EntityLifeStage >= EntityLifeStage.MapInitialized)
            EnsurePorts(ent);
    }

    private void OnShutdown(Entity<MachineGasPortsComponent> ent, ref ComponentShutdown args)
    {
        DeletePorts(ent);
    }

    private void OnAnalyze(Entity<MachineGasPortsComponent> ent, ref GasAnalyzerScanEvent args)
    {
        if (!TryGetPipes(ent, out var inlet, out var outlet))
            return;

        args.GasMixtures ??= new List<(string, GasMixture?)>();
        args.GasMixtures.Add((Loc.GetString("gas-analyzer-window-text-inlet"), inlet.Air));
        args.GasMixtures.Add((Loc.GetString("gas-analyzer-window-text-outlet"), outlet.Air));
    }

    private void OnExamined(Entity<MachineGasPortsComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var rotation = Transform(ent).LocalRotation;
        var inlet = (rotation + DirectionAngle(ent.Comp.InletDirection)).GetCardinalDir();
        var outlet = (rotation + DirectionAngle(ent.Comp.OutletDirection)).GetCardinalDir();

        args.PushMarkup(Loc.GetString("dh-machine-gas-ports-examine",
            ("inlet", Loc.GetString($"dh-direction-{inlet.ToString().ToLowerInvariant()}")),
            ("outlet", Loc.GetString($"dh-direction-{outlet.ToString().ToLowerInvariant()}"))));
    }

    /// <summary>
    /// Gets the inlet and outlet pipe nodes, spawning the ports if they are missing.
    /// </summary>
    public bool TryGetPipes(Entity<MachineGasPortsComponent> ent,
        [NotNullWhen(true)] out PipeNode? inlet,
        [NotNullWhen(true)] out PipeNode? outlet)
    {
        inlet = null;
        outlet = null;

        EnsurePorts(ent);

        if (ent.Comp.Inlet is not { } inletUid || ent.Comp.Outlet is not { } outletUid)
            return false;

        if (!_nodeContainer.TryGetNode(inletUid, ent.Comp.PortNode, out inlet))
            return false;

        if (!_nodeContainer.TryGetNode(outletUid, ent.Comp.PortNode, out outlet))
            return false;

        return true;
    }

    public void EnsurePorts(Entity<MachineGasPortsComponent> ent)
    {
        var xform = Transform(ent);
        if (!xform.Anchored || xform.GridUid == null || TerminatingOrDeleted(ent))
            return;

        ent.Comp.Inlet = EnsurePort(ent, xform, ent.Comp.Inlet, ent.Comp.InletOffset, ent.Comp.InletDirection);
        ent.Comp.Outlet = EnsurePort(ent, xform, ent.Comp.Outlet, ent.Comp.OutletOffset, ent.Comp.OutletDirection);
    }

    public void DeletePorts(Entity<MachineGasPortsComponent> ent)
    {
        if (ent.Comp.Inlet is { } inlet && !TerminatingOrDeleted(inlet))
            QueueDel(inlet);

        if (ent.Comp.Outlet is { } outlet && !TerminatingOrDeleted(outlet))
            QueueDel(outlet);

        ent.Comp.Inlet = null;
        ent.Comp.Outlet = null;
    }

    private EntityUid EnsurePort(Entity<MachineGasPortsComponent> ent,
        TransformComponent xform,
        EntityUid? existing,
        System.Numerics.Vector2 offset,
        Direction direction)
    {
        if (existing is { } port && !TerminatingOrDeleted(port))
            return port;

        // Port position and facing follow the machine's rotation. The pipe node of the port faces south at rotation 0.
        var position = xform.LocalPosition + xform.LocalRotation.RotateVec(offset);
        var coordinates = new EntityCoordinates(xform.ParentUid, position);
        var rotation = xform.LocalRotation + DirectionAngle(direction);

        var newPort = SpawnAttachedTo(ent.Comp.PortPrototype, coordinates, rotation: rotation);
        if (!Transform(newPort).Anchored)
            _transform.AnchorEntity(newPort);

        return newPort;
    }

    /// <summary>
    /// Angle that turns a south-facing direction into <paramref name="direction"/>. South is 0, east is 90 degrees.
    /// </summary>
    private static Angle DirectionAngle(Direction direction)
    {
        return new Angle(MathHelper.PiOver4 * (int) direction);
    }
}
