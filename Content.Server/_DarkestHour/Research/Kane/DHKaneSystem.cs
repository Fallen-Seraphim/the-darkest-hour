using Content.Server.Power.EntitySystems;
using Content.Shared._DarkestHour.Research.Kane;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Research.Kane;

/// <summary>
/// Кристаллы Кейна: случайная плотность осколков, рост в кристаллизаторе, литограф, сборка ячейки Кейна.
/// </summary>
public sealed partial class DHKaneSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHKaneDensityComponent, MapInitEvent>(OnDensityMapInit);

        SubscribeLocalEvent<DHKaneCrystallizerComponent, EntInsertedIntoContainerMessage>(OnCrystallizerInserted);
        SubscribeLocalEvent<DHKaneCrystallizerComponent, EntRemovedFromContainerMessage>(OnCrystallizerRemoved);

        SubscribeLocalEvent<DHKaneLithographComponent, EntInsertedIntoContainerMessage>(OnLithographInserted);

        SubscribeLocalEvent<DHKaneCasingComponent, InteractUsingEvent>(OnCasingInteractUsing);
    }

    private void OnDensityMapInit(Entity<DHKaneDensityComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Density > 0f || ent.Comp.RandomMax <= 0f)
            return;

        ent.Comp.Density = _random.NextFloat(ent.Comp.RandomMin, ent.Comp.RandomMax);
        Dirty(ent);
    }

    #region Crystallizer

    private void OnCrystallizerInserted(Entity<DHKaneCrystallizerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SlotId || !TryComp<DHKaneDensityComponent>(args.Entity, out var density))
            return;

        ent.Comp.SeedDensity = density.Density;
        ent.Comp.Grown = 0f;
        // Затравку заменим на кристалл в следующем тике — не трогаем контейнер внутри его же события.
        ent.Comp.PendingSwap = HasComp<DHKaneSeedComponent>(args.Entity);
        _appearance.SetData(ent, DHKaneMachineVisuals.Running, true);
    }

    private void OnCrystallizerRemoved(Entity<DHKaneCrystallizerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SlotId)
            return;

        _appearance.SetData(ent, DHKaneMachineVisuals.Running, false);
    }

    private void UpdateCrystallizers(float frameTime)
    {
        var query = EntityQueryEnumerator<DHKaneCrystallizerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_itemSlots.TryGetSlot(uid, comp.SlotId, out var slot) || slot.Item is not { } item)
                continue;

            if (comp.PendingSwap)
            {
                comp.PendingSwap = false;
                if (TryComp<DHKaneSeedComponent>(item, out var seed))
                {
                    var crystal = Spawn(seed.Crystal, Transform(uid).Coordinates);
                    EnsureComp<DHKaneDensityComponent>(crystal).Density = comp.SeedDensity;
                    Del(item); // освобождает слот сразу
                    _itemSlots.TryInsert(uid, slot, crystal, null);
                    continue;
                }
            }

            if (!_power.IsPowered(uid) || !TryComp<DHKaneDensityComponent>(item, out var density))
                continue;

            comp.Grown += frameTime;
            var target = comp.SeedDensity + (comp.MaxDensity - comp.SeedDensity) * (1f - MathF.Exp(-comp.Grown / comp.GrowthTime));
            if (target > density.Density + 0.5f)
            {
                density.Density = target;
                Dirty(item, density);
            }
        }
    }

    #endregion

    #region Lithograph

    private void OnLithographInserted(Entity<DHKaneLithographComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SlotId || ent.Comp.End != null || !HasComp<DHKaneProductComponent>(args.Entity))
            return;

        ent.Comp.End = _timing.CurTime + ent.Comp.Duration;
        _appearance.SetData(ent, DHKaneMachineVisuals.Running, true);
        _itemSlots.SetLock(ent.Owner, ent.Comp.SlotId, true);
    }

    private void UpdateLithographs()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHKaneLithographComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.End is not { } end || now < end)
                continue;

            // Без питания — ждём.
            if (!_power.IsPowered(uid))
                continue;

            comp.End = null;
            _appearance.SetData(uid, DHKaneMachineVisuals.Running, false);
            _itemSlots.SetLock(uid, comp.SlotId, false);

            if (!_itemSlots.TryGetSlot(uid, comp.SlotId, out var slot) || slot.Item is not { } item ||
                !TryComp<DHKaneProductComponent>(item, out var product))
            {
                continue;
            }

            var density = TryComp<DHKaneDensityComponent>(item, out var d) ? d.Density : 0f;
            var result = Spawn(product.Next, Transform(uid).Coordinates);
            var resultDensity = EnsureComp<DHKaneDensityComponent>(result);
            resultDensity.Density = density * product.Retain;
            Dirty(result, resultDensity);
            QueueDel(item);
        }
    }

    #endregion

    #region Casing

    private void OnCasingInteractUsing(Entity<DHKaneCasingComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<DHKaneMicroboardComponent>(args.Used))
            return;

        args.Handled = true;
        var density = TryComp<DHKaneDensityComponent>(args.Used, out var d) ? d.Density : 0f;
        var charge = ent.Comp.BaseCharge + ent.Comp.ChargePerDensity * density;

        var cell = Spawn(ent.Comp.Result, Transform(ent).Coordinates);
        _battery.SetMaxCharge(cell, charge);
        _battery.SetCharge(cell, charge);

        QueueDel(args.Used);
        QueueDel(ent);
        _hands.PickupOrDrop(args.User, cell);
        _popup.PopupEntity(Loc.GetString("dh-kane-cell-assembled", ("charge", (int) charge)), args.User, args.User);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateCrystallizers(frameTime);
        UpdateLithographs();
    }
}
