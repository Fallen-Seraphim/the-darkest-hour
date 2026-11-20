using Content.Shared.Clothing.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;

namespace Content.Server._DarkestHour.Vendors;

/// <summary>Кейс выбора комплекта «Осады»: выбранный комплект сразу надевается (<see cref="DHAutoEquipSystem"/>).</summary>
[RegisterComponent]
public sealed partial class DHKitAutoEquipComponent : Component
{
}

/// <summary>
/// Комплект из автомата снаряжения и из кейса выбора надевается сразу, как у морпехов в RMC (план осады п. 13,
/// решение автора 2026-11-20). Броня с экзоскелетом, шлем, ботинки, перчатки, накидка — на себя; если слот занят,
/// старая вещь падает под ноги. Остальное — в свободный подходящий слот (оружие — на спину или в слот брони),
/// иначе в руки или под ноги.
/// </summary>
public sealed class DHAutoEquipSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;

    /// <summary>Слоты «носимого»: в них новая вещь заменяет старую.</summary>
    private const SlotFlags WearSlots = SlotFlags.OUTERCLOTHING | SlotFlags.HEAD | SlotFlags.FEET | SlotFlags.GLOVES |
                                        SlotFlags.NECK | SlotFlags.MASK | SlotFlags.EYES;

    /// <summary>Свободные слоты, куда можно положить остальное (карманы — нет, туда сами решат).</summary>
    private const SlotFlags FreeSlots = SlotFlags.BACK | SlotFlags.SUITSTORAGE | SlotFlags.BELT;

    /// <summary>Надеть и раздать выданные предметы. Сначала носимое (броня даёт слот для оружия), потом остальное.</summary>
    public void EquipAll(EntityUid user, IReadOnlyList<EntityUid> items)
    {
        var rest = new List<EntityUid>();
        foreach (var item in items)
        {
            if (TerminatingOrDeleted(item))
                continue;

            if (!TryWear(user, item))
                rest.Add(item);
        }

        foreach (var item in rest)
        {
            if (!TryPutInFreeSlot(user, item))
                _hands.PickupOrDrop(user, item, checkActionBlocker: false);
        }
    }

    private bool TryWear(EntityUid user, EntityUid item)
    {
        if (!TryComp<ClothingComponent>(item, out var clothing) || (clothing.Slots & WearSlots) == 0)
            return false;

        // Сначала свободный подходящий слот, потом — заменить то, что в нём.
        string? occupied = null;
        var slots = _inventory.GetSlotEnumerator(user, clothing.Slots & WearSlots);
        while (slots.NextItem(out var current, out var slot))
        {
            if (current == item)
                return true;

            occupied ??= slot.Name;
        }

        slots = _inventory.GetSlotEnumerator(user, clothing.Slots & WearSlots);
        while (slots.MoveNext(out var container))
        {
            if (container.ContainedEntity == null &&
                _inventory.TryEquip(user, item, container.ID, silent: true, force: false))
            {
                return true;
            }
        }

        if (occupied == null)
            return false;

        // Занято — старое под ноги (reparent кладёт рядом с бойцом), новое на его место.
        if (!_inventory.TryUnequip(user, occupied, silent: true, force: true))
            return false;

        return _inventory.TryEquip(user, item, occupied, silent: true, force: false);
    }

    private bool TryPutInFreeSlot(EntityUid user, EntityUid item)
    {
        if (!TryComp<ClothingComponent>(item, out var clothing) || (clothing.Slots & FreeSlots) == 0)
            return false;

        var slots = _inventory.GetSlotEnumerator(user, clothing.Slots & FreeSlots);
        while (slots.MoveNext(out var container))
        {
            if (container.ContainedEntity == null &&
                _inventory.TryEquip(user, item, container.ID, silent: true, force: false))
            {
                return true;
            }
        }

        return false;
    }
}
