using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Clothing;

/// <summary>
/// Шлем со встроенным противогазом занимает и слот головы, и слот маски: с надетой маской его не надеть (и не
/// развернуть из брони), а пока он надет, маску не надеть (<see cref="SlotBlockComponent"/> на маску в прототипе).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHIntegratedMaskComponent : Component
{
    [DataField]
    public string MaskSlot = "mask";
}

public sealed partial class DHIntegratedMaskSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHIntegratedMaskComponent, BeingEquippedAttemptEvent>(OnBeingEquipped);
    }

    private void OnBeingEquipped(Entity<DHIntegratedMaskComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled || (args.SlotFlags & SlotFlags.HEAD) == 0)
            return;

        if (!_inventory.TryGetSlotEntity(args.EquipTarget, ent.Comp.MaskSlot, out _))
            return;

        args.Reason = "dh-integrated-mask-occupied";
        args.Cancel();
    }
}
