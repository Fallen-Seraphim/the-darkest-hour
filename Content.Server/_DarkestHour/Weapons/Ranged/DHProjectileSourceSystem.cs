using Content.Shared.Projectiles;

namespace Content.Server._DarkestHour.Weapons.Ranged;

/// <summary>
/// Снаряд пережил своё оружие или стрелка (одноразовая труба удаляется сразу после выстрела, тело разорвало,
/// машину разбило): ссылки <see cref="ProjectileComponent.Shooter"/> и <see cref="ProjectileComponent.Weapon"/>
/// на удалённую сущность сеть пыталась передать клиенту — «Can't resolve MetaDataComponent» в PVS. Каждый тик такие
/// ссылки обнуляются.
/// </summary>
public sealed partial class DHProjectileSourceSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ProjectileComponent>();
        while (query.MoveNext(out var uid, out var projectile))
        {
            var changed = false;
            if (projectile.Shooter is { } shooter && TerminatingOrDeleted(shooter))
            {
                projectile.Shooter = null;
                changed = true;
            }

            if (projectile.Weapon is { } weapon && TerminatingOrDeleted(weapon))
            {
                projectile.Weapon = null;
                changed = true;
            }

            if (changed)
                Dirty(uid, projectile);
        }
    }
}
