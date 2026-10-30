using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Weapons.Ballistics;

/// <summary>
/// Extra terminal ballistics for a bullet: armour penetration and expanding / non-expanding behaviour.
/// Applied right before the bullet deals its damage.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHBallisticsComponent : Component
{
    /// <summary>
    /// DarkestHour: класс бронепробития (0–3). Модульную броню и шлем пуля пробивает, если её класс не ниже класса
    /// брони части тела: тогда полный урон, иначе заброневая травма (<see cref="Content.Shared._DarkestHour.Clothing.ModularArmor.DHModularArmor.BlockedFraction"/>).
    /// </summary>
    [DataField]
    public int PenetrationClass;

    /// <summary>
    /// Share of the target's armour this bullet ignores, 0..1. Used against ordinary (non-modular) armour only.
    /// 0 = armour works as usual, 1 = armour is ignored entirely.
    /// </summary>
    [DataField]
    public float ArmorPenetration;

    /// <summary>
    /// Damage multiplier against targets without meaningful armour (expanding bullets are above 1).
    /// </summary>
    [DataField]
    public float UnarmoredMultiplier = 1f;

    /// <summary>
    /// Damage multiplier against armoured targets (expanding bullets are below 1).
    /// </summary>
    [DataField]
    public float ArmoredMultiplier = 1f;

    /// <summary>
    /// A target counts as armoured if its armour lets through less than this share of piercing damage.
    /// </summary>
    [DataField]
    public float ArmoredThreshold = 0.9f;
}

/// <summary>
/// DarkestHour: пуля попадает в цель — поднимается на цели до урона. Обработчик модульной брони (сервер) выбирает часть
/// тела, сравнивает классы и выставляет итоговый урон; <see cref="Handled"/> — урон посчитан, обычная логика
/// бронепробития не нужна.
/// </summary>
[ByRefEvent]
public struct DHBulletHitEvent
{
    public readonly EntityUid Bullet;
    public readonly DHBallisticsComponent Ballistics;
    public readonly EntityUid? Shooter;
    public DamageSpecifier Damage;
    public bool Handled;

    public DHBulletHitEvent(EntityUid bullet, DHBallisticsComponent ballistics, DamageSpecifier damage, EntityUid? shooter)
    {
        Bullet = bullet;
        Ballistics = ballistics;
        Damage = damage;
        Shooter = shooter;
        Handled = false;
    }
}
