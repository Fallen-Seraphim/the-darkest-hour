using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Medical.Disease;

/// <summary>
/// Лечение болезней (лекарство): снижает прогресс болезней своей группы (или конкретной болезни) и/или усиливает
/// иммунный ответ. Работает на сервере — там течение болезни.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
public sealed partial class DHTreatDiseaseEntityEffectSystem : EntityEffectSystem<DHDiseaseCarrierComponent, DHTreatDisease>
{
    [Dependency] private IPrototypeManager _proto = default!;

    protected override void Effect(Entity<DHDiseaseCarrierComponent> entity, ref EntityEffectEvent<DHTreatDisease> args)
    {
        var effect = args.Effect;
        foreach (var (id, disease) in entity.Comp.Diseases)
        {
            if (effect.Disease is { } only && only != id)
                continue;

            if (effect.Group is { } group && (!_proto.TryIndex(id, out var proto) || proto.Group != group))
                continue;

            disease.Progress -= effect.Amount * args.Scale;
            disease.Immunity = MathF.Min(1f, disease.Immunity + effect.Immunity * args.Scale);
        }
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class DHTreatDisease : EntityEffectBase<DHTreatDisease>
{
    /// <summary>Лечит болезни этой группы.</summary>
    [DataField]
    public DHPathogenGroup? Group;

    /// <summary>Лечит только эту болезнь.</summary>
    [DataField]
    public ProtoId<DHDiseasePrototype>? Disease;

    /// <summary>На сколько снижается прогресс болезни за срабатывание.</summary>
    [DataField]
    public float Amount;

    /// <summary>На сколько растёт иммунный ответ за срабатывание.</summary>
    [DataField]
    public float Immunity;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var target = Disease is { } d && prototype.TryIndex(d, out var proto)
            ? Loc.GetString(proto.Name)
            : Group is { } g
                ? Loc.GetString($"dh-disease-group-{g}")
                : Loc.GetString("dh-disease-group-any");

        return Loc.GetString("dh-entity-effect-guidebook-treat-disease", ("chance", Probability), ("target", target));
    }
}

/// <summary>
/// Заразить болезнью (испорченная еда, банка с культурой, событие). Само заражение — на сервере.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
public sealed partial class DHInfectDiseaseEntityEffectSystem : EntityEffectSystem<DHDiseaseCarrierComponent, DHInfectDisease>
{
    protected override void Effect(Entity<DHDiseaseCarrierComponent> entity, ref EntityEffectEvent<DHInfectDisease> args)
    {
        var ev = new DHInfectAttemptEvent(args.Effect.Disease);
        RaiseLocalEvent(entity, ref ev);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class DHInfectDisease : EntityEffectBase<DHInfectDisease>
{
    [DataField(required: true)]
    public ProtoId<DHDiseasePrototype> Disease;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var name = prototype.TryIndex(Disease, out var proto) ? Loc.GetString(proto.Name) : Disease.Id;
        return Loc.GetString("dh-entity-effect-guidebook-infect-disease", ("chance", Probability), ("disease", name));
    }
}

/// <summary>
/// Попытка заразить носителя болезнью (обрабатывает серверная система).
/// </summary>
[ByRefEvent]
public readonly record struct DHInfectAttemptEvent(ProtoId<DHDiseasePrototype> Disease);
