using Content.Shared._DarkestHour.City;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;

namespace Content.Server._DarkestHour.City;

/// <summary>Когда игрок появился — убирает «пустые» заготовки случайной одежды (выпало «без этой вещи»).</summary>
public sealed partial class DHRandomOutfitCleanupSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        var slots = _inventory.GetSlotEnumerator(ev.Mob);
        while (slots.NextItem(out var item))
        {
            if (TryComp<DHRandomOutfitComponent>(item, out var outfit) && outfit.Empty)
                QueueDel(item);
        }
    }
}
