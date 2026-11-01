using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Projectiles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Structures;

/// <summary>
/// DarkestHour (решение автора 2026-10-30): прочные стены и скалы. Висит на <c>BaseStructureWall</c> — значит,
/// на всех стенах, скалах и астероидном камне.
/// <list type="bullet">
/// <item>Весь урон, кроме <c>Structural</c>, делится на <see cref="Multiplier"/> — по сути прочность ×<see cref="Multiplier"/>.</item>
/// <item>Пули стенам ничего не делают (остаётся только их <c>Structural</c>-урон, если он есть).</item>
/// <item>Артиллерия (корабельная, полевая, миномёты) бьёт взрывом <c>DHArtillery</c> с <c>Structural</c>-уроном,
///       поэтому стены ломает как раньше и даже лучше; людей <c>Structural</c> не ранит.</item>
/// </list>
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHFortifiedComponent : Component
{
    [DataField]
    public float Multiplier = 5f;

    /// <summary>
    /// Доля урона по постройкам (<c>Structural</c>) от пуль. Стены — 1, броня техники — малая: её берут только
    /// бронебойные снаряды (<see cref="DHAntiArmorComponent"/>) и взрывы.
    /// </summary>
    [DataField]
    public float BulletStructuralMultiplier = 1f;

    /// <summary>
    /// Урон от одного попадания противотанкового снаряда (<see cref="DHAntiArmorComponent.AntiTank"/>), 0 — как обычный
    /// бронебойный. Техника задаёт его так, чтобы её уничтожало заданное число попаданий (танк — 4, БТР — 3).
    /// </summary>
    [DataField]
    public float AntiTankDamage;
}

/// <summary>Бронебойный снаряд: по броне техники бьёт полным уроном по постройкам.</summary>
[RegisterComponent]
public sealed partial class DHAntiArmorComponent : Component
{
    /// <summary>Противотанковый (танковая пушка): по технике — <see cref="DHFortifiedComponent.AntiTankDamage"/>.</summary>
    [DataField]
    public bool AntiTank;
}

public sealed partial class DHFortifiedSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> Structural = "Structural";

    private EntityQuery<DHFortifiedComponent> _fortifiedQuery;

    public override void Initialize()
    {
        base.Initialize();

        _fortifiedQuery = GetEntityQuery<DHFortifiedComponent>();

        SubscribeLocalEvent<DHFortifiedComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<ProjectileComponent, BeforeProjectileHitEvent>(OnBeforeProjectileHit);
    }

    private void OnDamageModify(Entity<DHFortifiedComponent> ent, ref DamageModifyEvent args)
    {
        if (ent.Comp.Multiplier <= 1f)
            return;

        var scaled = new DamageSpecifier();
        foreach (var (type, value) in args.Damage.DamageDict)
        {
            // Лечение (отрицательный урон) и урон по постройкам не трогаем.
            scaled.DamageDict[type] = type == Structural || value < 0 ? value : value / ent.Comp.Multiplier;
        }

        args.Damage = scaled;
    }

    /// <summary>Пули по стенам, скалам и броне: остаётся только урон по постройкам (по броне — ослабленный).</summary>
    private void OnBeforeProjectileHit(Entity<ProjectileComponent> ent, ref BeforeProjectileHitEvent args)
    {
        if (!_fortifiedQuery.TryComp(args.Target, out var fortified))
            return;

        var remaining = new DamageSpecifier();
        if (TryComp<DHAntiArmorComponent>(ent, out var antiArmor) && antiArmor.AntiTank && fortified.AntiTankDamage > 0)
        {
            remaining.DamageDict[Structural] = fortified.AntiTankDamage;
            args.Damage = remaining;
            return;
        }

        var multiplier = antiArmor != null ? 1f : fortified.BulletStructuralMultiplier;
        if (args.Damage.DamageDict.TryGetValue(Structural, out var structural) && structural > 0)
            remaining.DamageDict[Structural] = structural * multiplier;

        args.Damage = remaining;
    }
}
