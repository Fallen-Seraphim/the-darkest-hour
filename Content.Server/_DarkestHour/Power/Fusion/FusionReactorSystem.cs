using Content.Server.Administration.Logs;
using Content.Server.Power.Components;
using Content.Shared._DarkestHour.Power.Fusion;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.Examine;
using Robust.Server.GameObjects;

namespace Content.Server._DarkestHour.Power.Fusion;

/// <summary>
/// Работа <see cref="FusionReactorComponent"/>: раз в секунду отдача в сеть догоняет заданную мощность, топливо в
/// кассете сгорает пропорционально отдаче. Без топлива или выключенный реактор плавно глохнет.
/// </summary>
public sealed partial class FusionReactorSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private FusionFuelCassetteSystem _cassette = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    private const float Interval = 1f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FusionReactorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FusionReactorComponent, FusionReactorToggleMessage>(OnToggle);
        SubscribeLocalEvent<FusionReactorComponent, FusionReactorSetPowerMessage>(OnSetPower);
        SubscribeLocalEvent<FusionReactorComponent, BoundUIOpenedEvent>(OnUiOpened);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FusionReactorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.Accumulator += frameTime;
            if (comp.Accumulator < Interval)
                continue;

            var dt = comp.Accumulator;
            comp.Accumulator = 0f;
            Tick((uid, comp), dt);
        }
    }

    private void Tick(Entity<FusionReactorComponent> ent, float dt)
    {
        var comp = ent.Comp;
        var cassette = GetCassette(ent);
        var hasFuel = cassette != null && cassette.Value.Comp.Fuel > 0f;

        var target = comp.Active && hasFuel ? Math.Clamp(comp.PowerSetting, 0f, 1f) * comp.MaxPowerOutput : 0f;
        comp.PowerOutput = MoveTowards(comp.PowerOutput, target, comp.RampRate * comp.MaxPowerOutput * dt);

        if (comp.PowerOutput > 0f && cassette != null && comp.MaxPowerOutput > 0f)
            _cassette.Burn(cassette.Value, comp.PowerOutput / comp.MaxPowerOutput * comp.FuelBurnRate * dt);

        if (TryComp<PowerSupplierComponent>(ent, out var supplier))
            supplier.MaxSupply = comp.PowerOutput;

        UpdateSlotLock(ent);
        UpdateAppearance(ent);
        UpdateUi(ent);
    }

    private void UpdateSlotLock(Entity<FusionReactorComponent> ent)
    {
        // Кассету не вынуть, пока реактор включён или ещё отдаёт энергию.
        var locked = ent.Comp.Active || ent.Comp.PowerOutput > 0f;
        if (locked == ent.Comp.SlotLocked)
            return;

        ent.Comp.SlotLocked = locked;
        _itemSlots.SetLock(ent.Owner, ent.Comp.FuelSlotId, locked);
    }

    private void OnToggle(Entity<FusionReactorComponent> ent, ref FusionReactorToggleMessage args)
    {
        if (ent.Comp.Active == args.Active)
            return;

        ent.Comp.Active = args.Active;
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(args.Actor):player} switched {ToPrettyString(ent.Owner):reactor} {(args.Active ? "on" : "off")}");
        UpdateSlotLock(ent);
        UpdateUi(ent);
    }

    private void OnSetPower(Entity<FusionReactorComponent> ent, ref FusionReactorSetPowerMessage args)
    {
        if (!float.IsFinite(args.Power))
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

        var cassette = GetCassette(ent);
        args.PushMarkup(Loc.GetString("fusion-reactor-examine-output",
            ("output", MathF.Round(ent.Comp.PowerOutput / 1000f)),
            ("max", MathF.Round(ent.Comp.MaxPowerOutput / 1000f))));
        args.PushMarkup(cassette is { } c
            ? Loc.GetString("fusion-reactor-examine-fuel",
                ("percent", MathF.Round(c.Comp.MaxFuel > 0f ? c.Comp.Fuel / c.Comp.MaxFuel * 100f : 0f)))
            : Loc.GetString("fusion-reactor-examine-no-fuel"));
    }

    private void UpdateAppearance(Entity<FusionReactorComponent> ent)
    {
        var state = ent.Comp.PowerOutput > 0f ? FusionReactorVisualState.Active : FusionReactorVisualState.Off;
        _appearance.SetData(ent.Owner, FusionReactorVisuals.State, state);
        _light.SetEnabled(ent.Owner, state == FusionReactorVisualState.Active);
    }

    private void UpdateUi(Entity<FusionReactorComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, FusionReactorUiKey.Key))
            return;

        var comp = ent.Comp;
        var cassette = GetCassette(ent);
        _ui.SetUiState(ent.Owner, FusionReactorUiKey.Key, new FusionReactorBuiState
        {
            Active = comp.Active,
            HasCassette = cassette != null,
            Fuel = cassette?.Comp.Fuel ?? 0f,
            FuelMax = cassette?.Comp.MaxFuel ?? 0f,
            PowerSetting = comp.PowerSetting,
            PowerOutput = comp.PowerOutput,
            MaxPowerOutput = comp.MaxPowerOutput,
        });
    }

    private Entity<FusionFuelCassetteComponent>? GetCassette(Entity<FusionReactorComponent> ent)
    {
        if (_itemSlots.GetItemOrNull(ent.Owner, ent.Comp.FuelSlotId) is not { } item ||
            !TryComp<FusionFuelCassetteComponent>(item, out var cassette))
        {
            return null;
        }

        return (item, cassette);
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
            return target;

        return current + MathF.Sign(target - current) * maxDelta;
    }
}
