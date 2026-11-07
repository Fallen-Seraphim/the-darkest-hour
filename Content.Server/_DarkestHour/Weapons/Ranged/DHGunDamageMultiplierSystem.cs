using Content.Shared._DarkestHour.Weapons.Ranged;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server._DarkestHour.Weapons.Ranged;

public sealed partial class DHGunDamageMultiplierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHGunDamageMultiplierComponent, AmmoShotEvent>(OnAmmoShot);
    }

    private void OnAmmoShot(Entity<DHGunDamageMultiplierComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp<ProjectileComponent>(projectile, out var comp))
                continue;

            // Оператор * возвращает новый DamageSpecifier — общий из прототипа не меняется.
            comp.Damage = comp.Damage * ent.Comp.Multiplier;
        }
    }
}
