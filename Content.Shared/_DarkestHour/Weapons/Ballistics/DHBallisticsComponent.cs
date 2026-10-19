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
    /// Share of the target's armour this bullet ignores, 0..1.
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
