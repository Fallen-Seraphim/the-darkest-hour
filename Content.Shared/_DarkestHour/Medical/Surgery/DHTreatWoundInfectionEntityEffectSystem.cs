using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Medical.Surgery;

/// <summary>
/// Антибиотик против раневой инфекции: снижает её тяжесть, при нуле инфекция проходит.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
public sealed partial class DHTreatWoundInfectionEntityEffectSystem : EntityEffectSystem<DHWoundInfectionComponent, DHTreatWoundInfection>
{
    protected override void Effect(Entity<DHWoundInfectionComponent> entity, ref EntityEffectEvent<DHTreatWoundInfection> args)
    {
        entity.Comp.Severity -= args.Effect.Amount * args.Scale;
        if (entity.Comp.Severity <= 0f)
            RemComp<DHWoundInfectionComponent>(entity);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class DHTreatWoundInfection : EntityEffectBase<DHTreatWoundInfection>
{
    /// <summary>На сколько снижается тяжесть инфекции за срабатывание.</summary>
    [DataField]
    public float Amount = 0.1f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("dh-entity-effect-guidebook-treat-wound-infection", ("chance", Probability));
}
