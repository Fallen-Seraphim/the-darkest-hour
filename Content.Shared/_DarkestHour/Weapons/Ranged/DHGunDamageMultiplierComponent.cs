namespace Content.Shared._DarkestHour.Weapons.Ranged;

/// <summary>
/// Множитель урона пуль, выпущенных из этого оружия (ствол, а не патрон: патроны общие у разных фракций).
/// Решение автора 2026-11-07 — бафф оружия военной полиции.
/// </summary>
[RegisterComponent]
public sealed partial class DHGunDamageMultiplierComponent : Component
{
    [DataField]
    public float Multiplier = 1f;
}
