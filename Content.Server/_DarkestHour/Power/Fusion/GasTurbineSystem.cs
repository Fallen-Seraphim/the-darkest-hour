using Content.Server.NodeContainer.Nodes;
using Content.Server._DarkestHour.Atmos;
using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared._DarkestHour.Atmos;
using Content.Shared._DarkestHour.Power.Fusion;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;

namespace Content.Server._DarkestHour.Power.Fusion;

/// <summary>
/// Runs <see cref="GasTurbineComponent"/>: passes heat from the hot circuit into the cold circuit
/// and turns part of it into power on the HV network.
/// </summary>
public sealed partial class GasTurbineSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private MachineGasPortsSystem _ports = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GasTurbineComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
        SubscribeLocalEvent<GasTurbineComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<GasTurbineComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<GasTurbineComponent, GasTurbineRepairDoAfterEvent>(OnRepairDoAfter);
    }

    private void OnAtmosUpdate(Entity<GasTurbineComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        var comp = ent.Comp;
        var uid = ent.Owner;
        var dt = args.dt;
        var power = 0f;

        comp.LastMolesPerSecond = 0f;
        comp.LastColdMolesPerSecond = 0f;
        comp.LastEfficiency = 0f;

        if (TryComp<MachineGasPortsComponent>(uid, out var ports)
            && _ports.TryGetPipes((uid, ports), out var inlet, out var outlet))
        {
            comp.LastInletPressure = inlet.Air.Pressure;
            comp.LastOutletPressure = outlet.Air.Pressure;

            GasMixture? hotGas = null;
            var hotVolume = _ports.GetTransferVolume(inlet, outlet, comp.HotFlowRate, comp.MaxOutletPressure, dt);
            if (hotVolume > 0f)
            {
                hotGas = inlet.Air.RemoveVolume(hotVolume);
                comp.LastInletTemperature = hotGas.Temperature;
                comp.LastMolesPerSecond = hotGas.TotalMoles / MathF.Max(dt, 0.001f);
            }

            GasMixture? coldGas = null;
            PipeNode? coldOutlet = null;
            if (_ports.TryGetSecondaryPipes((uid, ports), out var coldInlet, out coldOutlet))
            {
                comp.LastColdInletPressure = coldInlet.Air.Pressure;
                comp.LastColdOutletPressure = coldOutlet.Air.Pressure;

                var coldVolume = _ports.GetTransferVolume(coldInlet, coldOutlet, comp.ColdFlowRate, comp.MaxOutletPressure, dt);
                if (coldVolume > 0f)
                {
                    coldGas = coldInlet.Air.RemoveVolume(coldVolume);
                    comp.LastColdTemperature = coldGas.Temperature;
                    comp.LastColdMolesPerSecond = coldGas.TotalMoles / MathF.Max(dt, 0.001f);
                }
            }

            if (!comp.Broken && hotGas != null && coldGas != null)
            {
                var hotCapacity = _atmos.GetHeatCapacity(hotGas, true);
                var coldCapacity = _atmos.GetHeatCapacity(coldGas, true);
                var hot = hotGas.Temperature;
                var cold = coldGas.Temperature;

                if (hotCapacity > 0f && coldCapacity > 0f && hot > cold)
                {
                    // Heat flows from the hot gas into the cold gas; part of it is turned into work on the way.
                    var equilibrium = (hotCapacity * hot + coldCapacity * cold) / (hotCapacity + coldCapacity);
                    var heat = comp.HeatExchange * hotCapacity * (hot - equilibrium);
                    var efficiency = Math.Clamp(comp.MaxEfficiency * (1f - cold / hot), 0f, 1f);
                    var work = heat * efficiency;

                    hotGas.Temperature = hot - heat / hotCapacity;
                    coldGas.Temperature = cold + (heat - work) / coldCapacity;

                    comp.LastEfficiency = efficiency;
                    power = work / MathF.Max(dt, 0.001f);
                }
            }

            if (hotGas != null)
            {
                if (!comp.Broken && comp.LastInletTemperature > comp.MaxInletTemperature)
                {
                    Damage(ent, (comp.LastInletTemperature - comp.MaxInletTemperature) / comp.MaxInletTemperature
                                * comp.OverheatDamage * dt);
                }

                _atmos.Merge(outlet.Air, hotGas);
            }

            if (coldGas != null && coldOutlet != null)
                _atmos.Merge(coldOutlet.Air, coldGas);
        }

        // Smooth the output so the power network does not jitter every atmos tick.
        comp.PowerOutput += (power - comp.PowerOutput) * MathF.Min(1f, comp.PowerSmoothing * dt);
        if (comp.Broken || comp.PowerOutput < 1f)
            comp.PowerOutput = 0f;

        if (TryComp<PowerSupplierComponent>(uid, out var supplier))
            supplier.MaxSupply = comp.PowerOutput;

        _appearance.SetData(uid, GasTurbineVisuals.Spinning, !comp.Broken && comp.PowerOutput > comp.SpinThreshold);
        _appearance.SetData(uid, GasTurbineVisuals.Broken, comp.Broken);
    }

    private void Damage(Entity<GasTurbineComponent> ent, float amount)
    {
        var comp = ent.Comp;
        comp.Integrity = MathF.Max(0f, comp.Integrity - amount);

        if (comp.Integrity > 0f || comp.Broken)
            return;

        comp.Broken = true;
        comp.PowerOutput = 0f;
        _popup.PopupEntity(Loc.GetString("gas-turbine-broken", ("turbine", ent.Owner)), ent.Owner, PopupType.LargeCaution);
        _adminLog.Add(LogType.Damaged, LogImpact.High, $"{ToPrettyString(ent.Owner):turbine} broke from overheating");
    }

    private void OnExamined(Entity<GasTurbineComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var comp = ent.Comp;
        if (comp.Broken)
        {
            args.PushMarkup(Loc.GetString("gas-turbine-examine-broken"));
            return;
        }

        args.PushMarkup(Loc.GetString("gas-turbine-examine-status",
            ("power", MathF.Round(comp.PowerOutput / 1000f)),
            ("efficiency", MathF.Round(comp.LastEfficiency * 100f)),
            ("integrity", MathF.Round(comp.Integrity / comp.MaxIntegrity * 100f))));
        args.PushMarkup(Loc.GetString("gas-turbine-examine-hot",
            ("temperature", MathF.Round(comp.LastInletTemperature)),
            ("inlet", MathF.Round(comp.LastInletPressure)),
            ("outlet", MathF.Round(comp.LastOutletPressure)),
            ("flow", MathF.Round(comp.LastMolesPerSecond))));
        args.PushMarkup(Loc.GetString("gas-turbine-examine-cold",
            ("temperature", MathF.Round(comp.LastColdTemperature)),
            ("inlet", MathF.Round(comp.LastColdInletPressure)),
            ("outlet", MathF.Round(comp.LastColdOutletPressure)),
            ("flow", MathF.Round(comp.LastColdMolesPerSecond))));

        if (comp.LastMolesPerSecond < 1f)
        {
            args.PushMarkup(Loc.GetString(comp.LastOutletPressure >= comp.MaxOutletPressure - 1f
                ? "gas-turbine-examine-hot-outlet-full"
                : "gas-turbine-examine-no-hot-gas"));
        }

        if (comp.LastColdMolesPerSecond < 1f)
        {
            args.PushMarkup(Loc.GetString(comp.LastColdOutletPressure >= comp.MaxOutletPressure - 1f
                ? "gas-turbine-examine-cold-outlet-full"
                : "gas-turbine-examine-no-cold-gas"));
        }
        else if (comp.LastMolesPerSecond >= 1f && comp.LastEfficiency < 0.15f)
        {
            args.PushMarkup(Loc.GetString("gas-turbine-examine-cold-too-warm"));
        }
    }

    private void OnInteractUsing(Entity<GasTurbineComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Integrity >= ent.Comp.MaxIntegrity)
            return;

        args.Handled = _tool.UseTool(args.Used,
            args.User,
            ent.Owner,
            ent.Comp.RepairDelay,
            ent.Comp.RepairQuality,
            new GasTurbineRepairDoAfterEvent(),
            ent.Comp.RepairFuelCost);
    }

    private void OnRepairDoAfter(Entity<GasTurbineComponent> ent, ref GasTurbineRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var comp = ent.Comp;
        comp.Integrity = MathF.Min(comp.MaxIntegrity, comp.Integrity + comp.RepairAmount);
        if (comp.Broken && comp.Integrity >= comp.RestartIntegrity)
            comp.Broken = false;

        _popup.PopupEntity(Loc.GetString("gas-turbine-repaired",
                ("integrity", MathF.Round(comp.Integrity / comp.MaxIntegrity * 100f))),
            ent.Owner,
            args.User);
    }
}
