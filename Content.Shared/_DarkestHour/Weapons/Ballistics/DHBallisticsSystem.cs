using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Inventory;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Projectiles;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Weapons.Ballistics;

/// <summary>
/// Applies <see cref="DHBallisticsComponent"/> before a bullet hits.
/// Armour penetration works by scaling the damage up so that, after the target's armour is applied as usual,
/// the armour only stopped (1 - penetration) of what it normally would. No vanilla code is changed.
/// </summary>
public sealed class DHBallisticsSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> PiercingType = "Piercing";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHBallisticsComponent, BeforeProjectileHitEvent>(OnBeforeHit);
    }

    private void OnBeforeHit(Entity<DHBallisticsComponent> ent, ref BeforeProjectileHitEvent args)
    {
        var comp = ent.Comp;

        var query = new CoefficientQueryEvent(~SlotFlags.POCKET);
        RaiseLocalEvent(args.Target, query);
        var coefficients = query.DamageModifiers.Coefficients;

        var piercingCoefficient = coefficients.TryGetValue(PiercingType, out var piercing) ? piercing : 1f;
        var armored = piercingCoefficient < comp.ArmoredThreshold;
        var multiplier = armored ? comp.ArmoredMultiplier : comp.UnarmoredMultiplier;
        var penetration = Math.Clamp(comp.ArmorPenetration, 0f, 1f);

        var result = new DamageSpecifier();
        foreach (var (type, value) in args.Damage.DamageDict)
        {
            var factor = multiplier;

            if (penetration > 0f
                && coefficients.TryGetValue(type, out var coefficient)
                && coefficient > 0f
                && coefficient < 1f)
            {
                // After armour multiplies by c, the damage should be (c + (1 - c) * p) of the original.
                factor *= (coefficient + (1f - coefficient) * penetration) / coefficient;
            }

            result.DamageDict[type] = value * factor;
        }

        args.Damage = result;
    }
}
