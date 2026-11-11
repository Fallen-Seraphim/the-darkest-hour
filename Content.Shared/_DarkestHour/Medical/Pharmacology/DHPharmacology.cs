using Content.Shared.EntityConditions;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Medical.Pharmacology;

/// <summary>
/// Фармакологический класс препаратов (опиоиды, седативные, стимуляторы, НПВС…). Лекарство этого класса,
/// пока действует, создаёт «нагрузку» класса; от неё растут привыкание (толерантность) и зависимость,
/// а сочетание классов даёт взаимодействия (опиоиды + седативные — угнетение дыхания).
/// Этап 2 переработки медицины (claude/chemistry-rework.md).
/// </summary>
[Prototype("dhDrugClass")]
public sealed partial class DHDrugClassPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>Нагрузка спадает на столько в секунду.</summary>
    [DataField]
    public float Decay = 1f;

    /// <summary>Потолок нагрузки (сколько «секунд действия» может накопиться).</summary>
    [DataField]
    public float MaxLoad = 60f;

    /// <summary>Прирост толерантности за секунду действия (0 — привыкания нет).</summary>
    [DataField]
    public float ToleranceGain;

    /// <summary>Предел толерантности: на эту долю слабее действует препарат класса.</summary>
    [DataField]
    public float ToleranceMax = 0.5f;

    /// <summary>Спад толерантности за секунду без препарата.</summary>
    [DataField]
    public float ToleranceDecay = 0.001f;

    /// <summary>Сколько секунд суммарного действия до зависимости (0 — зависимости нет).</summary>
    [DataField]
    public float DependenceThreshold;

    /// <summary>Через сколько секунд без препарата у зависимого начинается ломка.</summary>
    [DataField]
    public TimeSpan WithdrawalDelay = TimeSpan.FromSeconds(90);

    /// <summary>Боль при ломке (снимается обезболивающим, но не препаратами того же класса — те снимают ломку).</summary>
    [DataField]
    public float WithdrawalPain;

    /// <summary>Тремор при ломке.</summary>
    [DataField]
    public bool WithdrawalJitter;

    /// <summary>Угнетает дыхание: нагрузки таких классов складываются (опиоиды + седативные + алкоголь).</summary>
    [DataField]
    public bool RespiratoryDepressant;

    /// <summary>Сколько строк ломки в локализации: dh-drug-withdrawal-{ID}-{1..N}.</summary>
    [DataField]
    public int WithdrawalLines;
}

/// <summary>Состояние одного класса препаратов у пациента.</summary>
[DataDefinition]
public sealed partial class DHDrugState
{
    [DataField]
    public float Load;

    [DataField]
    public float Tolerance;

    /// <summary>Накопленная «история приёма» в секундах действия.</summary>
    [DataField]
    public float Exposure;

    [DataField]
    public bool Dependent;

    [DataField]
    public TimeSpan LastActive;

    [DataField]
    public bool Withdrawal;

    [DataField]
    public TimeSpan NextWithdrawalMessage;
}

/// <summary>
/// Фармакология пациента: нагрузка, толерантность, зависимость по классам. Появляется при первом приёме.
/// </summary>
[RegisterComponent]
public sealed partial class DHPharmacologyComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<DHDrugClassPrototype>, DHDrugState> Classes = new();

    [DataField]
    public TimeSpan NextUpdate;

    [DataField]
    public TimeSpan NextRespiratoryWarning;

    public float LoadOf(ProtoId<DHDrugClassPrototype> id) => Classes.TryGetValue(id, out var s) ? s.Load : 0f;

    public float ToleranceOf(ProtoId<DHDrugClassPrototype>? id) =>
        id is { } c && Classes.TryGetValue(c, out var s) ? s.Tolerance : 0f;
}

/// <summary>
/// Препарат класса действует: добавляет нагрузку класса (обычно 1 за срабатывание метаболизма ≈ 1 с).
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
public sealed partial class DHDrugEntityEffectSystem : EntityEffectSystem<MetaDataComponent, DHDrug>
{
    [Dependency] private IPrototypeManager _proto = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<DHDrug> args)
    {
        if (!_proto.TryIndex(args.Effect.Class, out var proto))
            return;

        var comp = EnsureComp<DHPharmacologyComponent>(entity);
        if (!comp.Classes.TryGetValue(args.Effect.Class, out var state))
        {
            state = new DHDrugState();
            comp.Classes[args.Effect.Class] = state;
        }

        // Нагрузка держится чуть дольше срабатываний, чтобы не мигать между тиками.
        state.Load = MathF.Min(proto.MaxLoad, state.Load + args.Effect.Load * args.Scale);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class DHDrug : EntityEffectBase<DHDrug>
{
    [DataField(required: true)]
    public ProtoId<DHDrugClassPrototype> Class;

    [DataField]
    public float Load = 1.5f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        prototype.TryIndex(Class, out var proto)
            ? Loc.GetString("dh-entity-effect-guidebook-drug", ("chance", Probability), ("class", Loc.GetString(proto.Name)))
            : string.Empty;
}

/// <summary>
/// Антагонист (налоксон, флумазенил): снимает нагрузку класса. У зависимых — сразу вызывает ломку.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
public sealed partial class DHDrugAntagonistEntityEffectSystem : EntityEffectSystem<DHPharmacologyComponent, DHDrugAntagonist>
{
    protected override void Effect(Entity<DHPharmacologyComponent> entity, ref EntityEffectEvent<DHDrugAntagonist> args)
    {
        if (!entity.Comp.Classes.TryGetValue(args.Effect.Class, out var state))
            return;

        state.Load = MathF.Max(0f, state.Load - args.Effect.Amount * args.Scale);
        if (state.Load <= 0f && state.Dependent)
            state.LastActive = TimeSpan.Zero; // ломка начнётся на следующем тике
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class DHDrugAntagonist : EntityEffectBase<DHDrugAntagonist>
{
    [DataField(required: true)]
    public ProtoId<DHDrugClassPrototype> Class;

    [DataField]
    public float Amount = 10f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        prototype.TryIndex(Class, out var proto)
            ? Loc.GetString("dh-entity-effect-guidebook-drug-antagonist", ("chance", Probability), ("class", Loc.GetString(proto.Name)))
            : string.Empty;
}

/// <summary>
/// Условие: нагрузка класса препаратов в пределах (для взаимодействий в YAML: «если в крови есть опиоид…»).
/// </summary>
/// <inheritdoc cref="EntityConditionSystem{T, TCondition}"/>
public sealed partial class DHDrugLoadEntityConditionSystem : EntityConditionSystem<MetaDataComponent, DHDrugLoadCondition>
{
    protected override void Condition(Entity<MetaDataComponent> entity, ref EntityConditionEvent<DHDrugLoadCondition> args)
    {
        var load = TryComp<DHPharmacologyComponent>(entity, out var comp) ? comp.LoadOf(args.Condition.Class) : 0f;
        args.Result = load >= args.Condition.Min && load <= args.Condition.Max;
    }
}

/// <inheritdoc cref="EntityCondition"/>
public sealed partial class DHDrugLoadCondition : EntityConditionBase<DHDrugLoadCondition>
{
    [DataField(required: true)]
    public ProtoId<DHDrugClassPrototype> Class;

    [DataField]
    public float Min = 0.01f;

    [DataField]
    public float Max = float.MaxValue;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype) =>
        prototype.TryIndex(Class, out var proto)
            ? Loc.GetString("dh-entity-condition-guidebook-drug-load", ("class", Loc.GetString(proto.Name)))
            : string.Empty;
}
