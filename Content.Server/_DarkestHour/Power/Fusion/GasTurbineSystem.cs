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
/// Runs <see cref="GasTurbineComponent"/>: takes heat from the gas passing through and feeds the HV network.
/// </summary>
public sealed class GasTurbineSystem : EntitySystem
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
        if (TryComp<MachineGasPortsComponent>(uid, out var ports)
            && _ports.TryGetPipes((uid, ports), out var inlet, out var outlet))
        {
            comp.LastInletPressure = inlet.Air.Pressure;
            comp.LastOutletPressure = outlet.Air.Pressure;

            var volume = _ports.GetTransferVolume(inlet, outlet, comp.FlowRate, comp.MaxOutletPressure, dt);
            if (volume > 0f)
            {
                var gas = inlet.Air.RemoveVolume(volume);
                var hot = gas.Temperature;
                comp.LastInletTemperature = hot;
                comp.LastMolesPerSecond = gas.TotalMoles / MathF.Max(dt, 0.001f);

                // The gas expands towards a fixed exhaust reference temperature; hotter gas gives more work.
                var cold = comp.ExhaustReferenceTemperature;
                var capacity = _atmos.GetHeatCapacity(gas, true);

                if (!comp.Broken && capacity > 0f && hot > cold)
                {
                    var cooled = hot - (hot - cold) * comp.ExpansionFraction;
                    var heat = capacity * (hot - cooled);
                    gas.Temperature = cooled;
                    power = heat * comp.Efficiency / MathF.Max(dt, 0.001f);
                }

                if (!comp.Broken && hot > comp.MaxInletTemperature)
                    Damage(ent, (hot - comp.MaxInletTemperature) / comp.MaxInletTemperature * comp.OverheatDamage * dt);

                _atmos.Merge(outlet.Air, gas);
            }
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
            ("temperature", MathF.Round(comp.LastInletTemperature)),
            ("integrity", MathF.Round(comp.Integrity / comp.MaxIntegrity * 100f))));
        args.PushMarkup(Loc.GetString("gas-turbine-examine-flow",
            ("inlet", MathF.Round(comp.LastInletPressure)),
            ("outlet", MathF.Round(comp.LastOutletPressure)),
            ("flow", MathF.Round(comp.LastMolesPerSecond))));

        if (comp.LastMolesPerSecond < 1f)
        {
            var hint = comp.LastOutletPressure >= comp.MaxOutletPressure - 1f
                ? "gas-turbine-examine-outlet-full"
                : "gas-turbine-examine-no-gas";
            args.PushMarkup(Loc.GetString(hint));
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
