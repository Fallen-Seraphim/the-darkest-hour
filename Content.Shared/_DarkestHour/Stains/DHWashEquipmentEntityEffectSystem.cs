using Content.Shared.EntityEffects;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;

namespace Content.Shared._DarkestHour.Stains;

/// <summary>
/// Вода смывает пятна (решение автора 2026-11-26): зашёл в воду — кровь и масло с надетой одежды, снаряжения и того,
/// что в руках, смываются.
/// </summary>
public sealed partial class DHWashEquipmentEntityEffectSystem : EntityEffectSystem<InventoryComponent, DHWashEquipment>
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private DHItemStainSystem _stains = default!;

    protected override void Effect(Entity<InventoryComponent> entity, ref EntityEffectEvent<DHWashEquipment> args)
    {
        var slots = _inventory.GetSlotEnumerator((entity, entity.Comp));
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is { } item)
                _stains.TryClean(item);
        }

        foreach (var held in _hands.EnumerateHeld(entity.Owner))
        {
            _stains.TryClean(held);
        }
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class DHWashEquipment : EntityEffectBase<DHWashEquipment>;
