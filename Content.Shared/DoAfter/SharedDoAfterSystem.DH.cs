using Content.Shared._DarkestHour.Clothing.ModularArmor;
using Content.Shared.Inventory;

namespace Content.Shared.DoAfter;

/// <summary>DarkestHour: тяжёлые перчатки замедляют действия с задержкой (перевязка, установка, ремонт и т.п.).</summary>
public abstract partial class SharedDoAfterSystem
{
    [Dependency] private InventorySystem _dhInventory = default!;

    private void DHApplyDelayModifiers(DoAfterArgs args)
    {
        if (args.Delay <= TimeSpan.Zero ||
            !_dhInventory.TryGetSlotEntity(args.User, "gloves", out var gloves) ||
            !TryComp<DHClumsyGlovesComponent>(gloves, out var clumsy))
        {
            return;
        }

        args.Delay *= clumsy.DoAfterMultiplier;
    }
}
