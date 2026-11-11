using Content.Shared.Inventory;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._DarkestHour.Clothing.ModularArmor;

/// <summary>Экзоскелет усиливает удар в ближнем бою (и оружием, и без него) по своему классу.</summary>
public sealed partial class DHExoskeletonSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
    }

    /// <summary>Надетый экзоскелет существа.</summary>
    public DHExoskeletonComponent? GetWorn(EntityUid wearer)
    {
        return _inventory.TryGetSlotEntity(wearer, "outerClothing", out var outer) &&
               TryComp<DHExoskeletonComponent>(outer, out var exo)
            ? exo
            : null;
    }

    private void OnGetMeleeDamage(Entity<MeleeWeaponComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (GetWorn(args.User) is not { } exo || MathF.Abs(exo.MeleeMultiplier - 1f) < 0.001f)
            return;

        args.Damage *= exo.MeleeMultiplier;
    }
}
