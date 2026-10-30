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

    /// <summary>Пули по стенам и скалам: остаётся только урон по постройкам.</summary>
    private void OnBeforeProjectileHit(Entity<ProjectileComponent> ent, ref BeforeProjectileHitEvent args)
    {
        if (!_fortifiedQuery.HasComp(args.Target))
            return;

        var remaining = new DamageSpecifier();
        if (args.Damage.DamageDict.TryGetValue(Structural, out var structural) && structural > 0)
            remaining.DamageDict[Structural] = structural;

        args.Damage = remaining;
    }
}
