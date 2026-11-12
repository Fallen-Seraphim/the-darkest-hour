using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Clothing.ModularArmor;

/// <summary>
/// Экзоскелет усиливает удар в ближнем бою — и кулаками, и любым холодным оружием — по своему классу: множитель
/// и прибавка дробящего урона (у тяжёлого — сильнее всего).
/// </summary>
public sealed partial class DHExoskeletonSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

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
        if (GetWorn(args.User) is not { } exo)
            return;

        if (MathF.Abs(exo.MeleeMultiplier - 1f) >= 0.001f)
            args.Damage *= exo.MeleeMultiplier;

        if (exo.MeleeBluntBonus > 0f)
            args.Damage += new DamageSpecifier { DamageDict = { [Blunt] = FixedPoint2.New(exo.MeleeBluntBonus) } };
    }
}
