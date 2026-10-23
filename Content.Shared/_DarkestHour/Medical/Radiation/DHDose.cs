using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.EntityEffects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Medical.Radiation;

// Накопленные дозы The Darkest Hour: радиация (лучевая болезнь) и хронит (хронитовая кожно-покровная болезнь).
// Доза копится в теле; по порогам дозы назначается стадия соответствующей болезни (движок болезней DH).

[Serializable, NetSerializable]
public enum DHDoseKind : byte
{
    Radiation,
    Chronite,
}

/// <summary>
/// Накопленные дозы радиации и хронита. Радиация копится из полученного урона «Radiation» (то есть с учётом брони
/// и йодида калия) и медленно выводится сама; хронит копится от источников хронита и сам не выводится.
/// </summary>
[RegisterComponent]
public sealed partial class DHDoseComponent : Component
{
    [DataField]
    public float Radiation;

    [DataField]
    public float Chronite;

    /// <summary>Естественное выведение радиации в секунду.</summary>
    [DataField]
    public float RadiationDecay = 0.02f;

    /// <summary>Пороги стадий лучевой болезни (накопленная доза).</summary>
    [DataField]
    public List<float> RadiationStages = new() { 30f, 80f, 150f, 250f };

    /// <summary>Пороги стадий хронитовой болезни. Последняя стадия неизлечима.</summary>
    [DataField]
    public List<float> ChroniteStages = new() { 15f, 40f, 80f, 130f };

    [DataField]
    public TimeSpan NextUpdate;

    /// <summary>Свет хронитового свечения добавлен этой системой (и должен быть убран ею же).</summary>
    [DataField]
    public bool GlowAdded;
}

/// <summary>
/// Источник хронита: облучает хронитом всех в радиусе (доза в секунду в центре, линейно спадает к краю).
/// </summary>
[RegisterComponent]
public sealed partial class DHChroniteEmitterComponent : Component
{
    [DataField]
    public float Rate = 0.05f;

    [DataField]
    public float Range = 2f;
}

/// <summary>
/// Дозиметр: в руке — своя доза, на другом — его доза.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHDosimeterComponent : Component;

/// <summary>
/// Кожный трансплантат для пересадки кожи после иссечения поражённой хронитом.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHSkinGraftComponent : Component;

/// <summary>
/// Выведение накопленной дозы (Hyronalin/Arithrazine — радиация, Хроносорб — хронит).
/// Хронит на последней (неизлечимой) стадии лекарством не снижается.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
public sealed partial class DHReduceDoseEntityEffectSystem : EntityEffectSystem<DHDoseComponent, DHReduceDose>
{
    protected override void Effect(Entity<DHDoseComponent> entity, ref EntityEffectEvent<DHReduceDose> args)
    {
        var amount = args.Effect.Amount * args.Scale;
        var comp = entity.Comp;
        switch (args.Effect.Kind)
        {
            case DHDoseKind.Radiation:
                comp.Radiation = MathF.Max(0f, comp.Radiation - amount);
                break;
            case DHDoseKind.Chronite:
                var terminal = comp.ChroniteStages.Count > 0 ? comp.ChroniteStages[^1] : float.MaxValue;
                if (comp.Chronite >= terminal && !args.Effect.BypassTerminal)
                    break;
                comp.Chronite = MathF.Max(0f, comp.Chronite - amount);
                break;
        }
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class DHReduceDose : EntityEffectBase<DHReduceDose>
{
    [DataField(required: true)]
    public DHDoseKind Kind;

    [DataField]
    public float Amount = 1f;

    /// <summary>Действует и на последней (неизлечимой лекарствами) стадии хронитовой болезни — экспериментальный Хронолизин.</summary>
    [DataField]
    public bool BypassTerminal;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("dh-entity-effect-guidebook-reduce-dose", ("chance", Probability),
            ("kind", Loc.GetString($"dh-dose-kind-{Kind}")));
}

/// <summary>
/// Добавить дозу хронита (реагенты и предметы с хронитом).
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
public sealed partial class DHAddChroniteDoseEntityEffectSystem : EntityEffectSystem<DHDoseComponent, DHAddChroniteDose>
{
    protected override void Effect(Entity<DHDoseComponent> entity, ref EntityEffectEvent<DHAddChroniteDose> args)
    {
        entity.Comp.Chronite += args.Effect.Amount * args.Scale;
        entity.Comp.NextUpdate = TimeSpan.Zero; // разбудить тик дозы
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class DHAddChroniteDose : EntityEffectBase<DHAddChroniteDose>
{
    [DataField]
    public float Amount = 1f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("dh-entity-effect-guidebook-add-chronite", ("chance", Probability));
}

/// <summary>
/// Боль (симптом болезни): кратковременно добавляет боль системы травм.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
public sealed partial class DHAddPainEntityEffectSystem : EntityEffectSystem<DHTraumaComponent, DHAddPain>
{
    protected override void Effect(Entity<DHTraumaComponent> entity, ref EntityEffectEvent<DHAddPain> args)
    {
        entity.Comp.PainSpike = MathF.Min(150f, entity.Comp.PainSpike + args.Effect.Amount * args.Scale);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class DHAddPain : EntityEffectBase<DHAddPain>
{
    [DataField]
    public float Amount = 10f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("dh-entity-effect-guidebook-add-pain", ("chance", Probability));
}
