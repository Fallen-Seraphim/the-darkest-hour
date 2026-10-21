using Content.Shared.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Research.Materials;

/// <summary>
/// Реверс-инжиниринг (решение автора 2026-10-21): трофей сканируют в химико-материальном сканере лаборатории
/// материаловедения — связанные технологии дешевеют на сервере РнД станции, плюс немного очков исследований.
/// Трофей не тратится. Каждый вид трофея засчитывается один раз за раунд.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHReverseEngineerableComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<TechnologyPrototype>> Technologies = new();

    /// <summary>Скидка на каждую технологию (доля цены).</summary>
    [DataField]
    public float Discount = 0.25f;

    [DataField]
    public int ResearchPoints = 2000;
}

/// <summary>
/// Реверс-инжиниринг по прототипу предмета — для предметов, чьи прототипы генерируются (оружие генератора) или
/// принадлежат апстриму: список прототипов → технологии со скидкой.
/// </summary>
[Prototype("dhReverseEngineering")]
public sealed partial class DHReverseEngineeringPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<EntProtoId> Items = new();

    [DataField(required: true)]
    public List<ProtoId<TechnologyPrototype>> Technologies = new();

    [DataField]
    public float Discount = 0.25f;

    [DataField]
    public int ResearchPoints = 2000;
}

/// <summary>
/// Скидки на технологии сервера РнД (от реверс-инжиниринга). Скидки на одну технологию складываются до предела.
/// </summary>
[RegisterComponent]
public sealed partial class DHResearchDiscountComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<TechnologyPrototype>, float> Discounts = new();

    [DataField]
    public float MaxDiscount = 0.5f;
}
