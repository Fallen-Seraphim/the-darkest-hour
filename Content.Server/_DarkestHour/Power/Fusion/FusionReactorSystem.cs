using Content.Server._DarkestHour.Atmos;
using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Emp;
using Content.Server.Explosion.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._DarkestHour.Atmos;
using Content.Shared._DarkestHour.Power.Fusion;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Station.Systems;
using Robust.Server.GameObjects;

namespace Content.Server._DarkestHour.Power.Fusion;

/// <summary>
/// Runs <see cref="FusionReactorComponent"/>: heats gas passing through the core, burns fuel,
/// keeps the containment field up and breaches the reactor when the field collapses.
/// </summary>
public sealed class FusionReactorSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private MachineGasPortsSystem _ports = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private FusionFuelCassetteSystem _cassette = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private EmpSystem _emp = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FusionReactorComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
        SubscribeLocalEvent<FusionReactorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FusionReactorComponent, FusionReactorToggleMessage>(OnToggle);
        SubscribeLocalEvent<FusionReactorComponent, FusionReactorSetPowerMessage>(OnSetPower);
        SubscribeLocalEvent<FusionReactorComponent, BoundUIOpenedEvent>(OnUiOpened);
    }

    #region Main loop

    private void OnAtmosUpdate(Entity<FusionReactorComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        var comp = ent.Comp;
        var uid = ent.Owner;
        var dt = args.dt;

        if (comp.Breached)
        {
            UpdateAppearance(ent);
            UpdateUi(ent);
            return;
        }

        comp.Powered = _power.IsPowered(uid);
        _power.SetLoad(uid, comp.Active ? comp.ActivePowerLoad : comp.IdlePowerLoad);

        var cassette = GetCassette(ent);
        var hasFuel = cassette != null && cassette.Value.Comp.Fuel > 0f;

        // Fusion only runs with the field powered and fuel in the cassette. Otherwise the output winds down.
        var running = comp.Active && comp.Powered && hasFuel;
        var target = running ? Math.Clamp(comp.PowerSetting, 0f, 1f) * comp.MaxHeatOutput : 0f;
        comp.HeatOutput = MoveTowards(comp.HeatOutput, target, comp.RampRate * comp.MaxHeatOutput * dt);

        if (comp.HeatOutput > 0f && cassette != null)
            _cassette.Burn(cassette.Value, comp.HeatOutput / comp.MaxHeatOutput * comp.FuelBurnRate * dt);

        // Core heating and passive losses.
        comp.CoreTemperature += comp.HeatOutput * dt / comp.CoreHeatCapacity;
        comp.CoreTemperature -= (comp.CoreTemperature - Atmospherics.T20C) * comp.PassiveCooling * dt;
        comp.CoreTemperature = MathF.Max(comp.CoreTemperature, Atmospherics.TCMB);

        // Coolant gas through the core.
        if (TryComp<MachineGasPortsComponent>(uid, out var ports)
            && _ports.TryGetPipes((uid, ports), out var inlet, out var outlet))
        {
            ExchangeHeat(comp, inlet, outlet, dt);
        }

        UpdateField(ent, dt);
        if (comp.Breached)
            return;

        UpdateSlotLock(ent);
        UpdateWarnings(ent);
        UpdateAppearance(ent);
        UpdateUi(ent);
    }

    private void ExchangeHeat(FusionReactorComponent comp, PipeNode inlet, PipeNode outlet, float dt)
    {
        var transferVolume = CalculateTransferVolume(comp.FlowRate, inlet, outlet, dt);
        if (transferVolume <= 0f)
            return;

        var gas = inlet.Air.RemoveVolume(transferVolume);
        comp.LastInletTemperature = gas.Temperature;

        var gasCapacity = _atmos.GetHeatCapacity(gas, true);
        if (gasCapacity > 0f)
        {
            // Move the gas a fraction of the way towards thermal equilibrium with the core; energy is conserved.
            var equilibrium = (gasCapacity * gas.Temperature + comp.CoreHeatCapacity * comp.CoreTemperature)
                              / (gasCapacity + comp.CoreHeatCapacity);
            var oldGasTemperature = gas.Temperature;
            gas.Temperature += (equilibrium - gas.Temperature) * comp.HeatTransferEfficiency;
            var energy = gasCapacity * (gas.Temperature - oldGasTemperature);
            comp.CoreTemperature = MathF.Max(comp.CoreTemperature - energy / comp.CoreHeatCapacity, Atmospherics.TCMB);
        }

        comp.LastOutletTemperature = gas.Temperature;
        _atmos.Merge(outlet.Air, gas);
    }

    private float CalculateTransferVolume(float flowRate, PipeNode inlet, PipeNode outlet, float dt)
    {
        if (inlet.Air.TotalMoles <= 0f || inlet.Air.Pressure <= 0f)
            return 0f;

        // Same limits as a gas pump: do not push the outlet above the max output pressure.
        var wanted = MathF.Min(inlet.Air.Volume, flowRate * _atmos.PumpSpeedup() * dt);
        var wantedMoles = inlet.Air.Pressure * wanted / (inlet.Air.Temperature * Atmospherics.R);
        var spaceLeft = (Atmospherics.MaxOutputPressure - outlet.Air.Pressure) * outlet.Air.Volume
                        / (MathF.Max(outlet.Air.Temperature, Atmospherics.TCMB) * Atmospherics.R);
        var moles = Math.Clamp(wantedMoles, 0f, MathF.Max(0f, spaceLeft));

        return moles * inlet.Air.Temperature * Atmospherics.R / inlet.Air.Pressure;
    }

    private void UpdateField(Entity<FusionReactorComponent> ent, float dt)
    {
        var comp = ent.Comp;

        if (comp.HeatOutput > 0f)
        {
            var loss = 0f;
            if (!comp.Powered)
                loss += comp.UnpoweredFieldLoss;

            if (comp.CoreTemperature > comp.SafeTemperature)
            {
                var overheat = (comp.CoreTemperature - comp.SafeTemperature)
                               / MathF.Max(comp.CriticalTemperature - comp.SafeTemperature, 1f);
                loss += overheat * comp.OverheatFieldLoss;
            }

            if (loss > 0f)
                comp.Integrity -= loss * dt;
            else
                comp.Integrity += comp.FieldRegeneration * dt;
        }
        else if (comp.Powered)
        {
            comp.Integrity += comp.FieldRegeneration * dt;
        }

        comp.Integrity = Math.Clamp(comp.Integrity, 0f, 100f);

        if (comp.Integrity <= 0f)
            Breach(ent);
    }

    private void UpdateSlotLock(Entity<FusionReactorComponent> ent)
    {
        // The cassette cannot be pulled out while the reactor is on or still burning.
        var locked = ent.Comp.Active || ent.Comp.HeatOutput > 0f;
        if (locked == ent.Comp.SlotLocked)
            return;

        ent.Comp.SlotLocked = locked;
        _itemSlots.SetLock(ent.Owner, ent.Comp.FuelSlotId, locked);
    }

    private void UpdateWarnings(Entity<FusionReactorComponent> ent)
    {
        var comp = ent.Comp;

        var level = 0;
        if (comp.Integrity < 20f)
            level = 3;
        else if (comp.Integrity < 50f)
            level = 2;
        else if (comp.CoreTemperature > comp.SafeTemperature)
            level = 1;

        if (level == comp.WarningLevel)
            return;

        // Only announce when things get worse; going back to normal is announced once.
        if (level > comp.WarningLevel)
        {
            var message = level switch
            {
                1 => Loc.GetString("fusion-reactor-radio-overheat", ("temperature", MathF.Round(comp.CoreTemperature))),
                2 => Loc.GetString("fusion-reactor-radio-field-50"),
                _ => Loc.GetString("fusion-reactor-radio-field-20"),
            };
            _radio.SendRadioMessage(ent.Owner, message, comp.AlertChannel, ent.Owner);
        }
        else if (level == 0)
        {
            _radio.SendRadioMessage(ent.Owner, Loc.GetString("fusion-reactor-radio-normal"), comp.AlertChannel, ent.Owner);
        }

        comp.WarningLevel = level;
    }

    #endregion

    #region Breach

    private void Breach(Entity<FusionReactorComponent> ent)
    {
        var comp = ent.Comp;
        var uid = ent.Owner;

        comp.Breached = true;
        comp.Active = false;
        comp.HeatOutput = 0f;
        comp.Integrity = 0f;

        _adminLog.Add(LogType.Explosion, LogImpact.Extreme,
            $"{ToPrettyString(uid):reactor} containment field collapsed at core temperature {comp.CoreTemperature}K");

        // The cassette is destroyed with the core.
        if (_itemSlots.GetItemOrNull(uid, comp.FuelSlotId) is { } item)
            QueueDel(item);

        // Hot plasma and tritium vent into the room.
        if (_atmos.GetTileMixture(uid, excite: true) is { } tile)
        {
            var release = new GasMixture(Atmospherics.CellVolume)
            {
                Temperature = MathF.Max(comp.CoreTemperature, 1000f),
            };
            release.AdjustMoles(Gas.Tritium, comp.BreachTritiumMoles);
            _atmos.Merge(tile, release);
        }

        var coordinates = _transform.GetMapCoordinates(uid);
        _emp.EmpPulse(coordinates, comp.BreachEmpRange, comp.BreachEmpEnergy, TimeSpan.FromSeconds(comp.BreachEmpDuration));
        _explosion.QueueExplosion(uid,
            comp.BreachExplosionType,
            comp.BreachExplosionIntensity,
            comp.BreachExplosionSlope,
            comp.BreachExplosionMaxTileIntensity,
            canCreateVacuum: true);

        _radio.SendRadioMessage(uid, Loc.GetString("fusion-reactor-radio-breach"), comp.AlertChannel, uid);
        var station = _station.GetOwningStation(uid);
        _chat.DispatchStationAnnouncement(station ?? uid,
            Loc.GetString("fusion-reactor-breach-announcement"),
            Loc.GetString("fusion-reactor-announcement-sender"),
            true,
            null,
            Color.Red);

        UpdateAppearance(ent);
        UpdateUi(ent);
    }

    #endregion

    #region UI and visuals

    private void OnToggle(Entity<FusionReactorComponent> ent, ref FusionReactorToggleMessage args)
    {
        if (ent.Comp.Breached || ent.Comp.Active == args.Active)
            return;

        ent.Comp.Active = args.Active;
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(args.Actor):player} switched {ToPrettyString(ent.Owner):reactor} {(args.Active ? "on" : "off")}");
        UpdateSlotLock(ent);
        UpdateUi(ent);
    }

    private void OnSetPower(Entity<FusionReactorComponent> ent, ref FusionReactorSetPowerMessage args)
    {
        if (ent.Comp.Breached || !float.IsFinite(args.Power))
            return;

        ent.Comp.PowerSetting = Math.Clamp(args.Power, 0f, 1f);
        var percent = (int) MathF.Round(ent.Comp.PowerSetting * 100f);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} set {ToPrettyString(ent.Owner):reactor} power to {percent}%");
        UpdateUi(ent);
    }

    private void OnUiOpened(Entity<FusionReactorComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnExamined(Entity<FusionReactorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.Breached)
        {
            args.PushMarkup(Loc.GetString("fusion-reactor-examine-breached"));
            return;
        }

        args.PushMarkup(Loc.GetString("fusion-reactor-examine-status",
            ("temperature", MathF.Round(ent.Comp.CoreTemperature)),
            ("integrity", MathF.Round(ent.Comp.Integrity))));
    }

    private void UpdateAppearance(Entity<FusionReactorComponent> ent)
    {
        var comp = ent.Comp;

        FusionReactorVisualState state;
        if (comp.Breached)
            state = FusionReactorVisualState.Breached;
        else if (comp.HeatOutput <= 0f)
            state = FusionReactorVisualState.Off;
        else if (comp.Integrity < 50f || comp.CoreTemperature > comp.SafeTemperature)
            state = FusionReactorVisualState.Unstable;
        else
            state = FusionReactorVisualState.Active;

        _appearance.SetData(ent.Owner, FusionReactorVisuals.State, state);
        _light.SetEnabled(ent.Owner, state is FusionReactorVisualState.Active or FusionReactorVisualState.Unstable);
    }

    private void UpdateUi(Entity<FusionReactorComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, FusionReactorUiKey.Key))
            return;

        var comp = ent.Comp;
        var cassette = GetCassette(ent);

        var state = new FusionReactorBuiState
        {
            Active = comp.Active,
            Powered = comp.Powered,
            Breached = comp.Breached,
            HasCassette = cassette != null,
            Fuel = cassette?.Comp.Fuel ?? 0f,
            FuelMax = cassette?.Comp.MaxFuel ?? 0f,
            PowerSetting = comp.PowerSetting,
            HeatOutput = comp.HeatOutput,
            MaxHeatOutput = comp.MaxHeatOutput,
            CoreTemperature = comp.CoreTemperature,
            SafeTemperature = comp.SafeTemperature,
            CriticalTemperature = comp.CriticalTemperature,
            Integrity = comp.Integrity,
            InletTemperature = comp.LastInletTemperature,
            OutletTemperature = comp.LastOutletTemperature,
        };

        _ui.SetUiState(ent.Owner, FusionReactorUiKey.Key, state);
    }

    #endregion

    private Entity<FusionFuelCassetteComponent>? GetCassette(Entity<FusionReactorComponent> ent)
    {
        if (_itemSlots.GetItemOrNull(ent.Owner, ent.Comp.FuelSlotId) is not { } item)
            return null;

        if (!TryComp<FusionFuelCassetteComponent>(item, out var cassette))
            return null;

        return (item, cassette);
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
            return target;

        return current + MathF.Sign(target - current) * maxDelta;
    }
}
