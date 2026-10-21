using Content.Shared.Body;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Medical.Trauma;

// Травмы The Darkest Hour: урон по частям тела поверх общего урона апстрима. Общий урон по-прежнему решает крит и смерть,
// а урон по частям — куда попали и что из этого следует: переломы, внутреннее кровотечение, осколки, повреждение органов,
// отрыв конечности и боль (болевой шок, потеря сознания).

/// <summary>
/// Выбранная зона попадания (кукла в интерфейсе). Ближний бой почти всегда попадает в неё, стрельба — чаще всего.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHTargetingComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<OrganCategoryPrototype> Target = "Torso";
}

/// <summary>
/// Клиент просит сменить зону попадания.
/// </summary>
[Serializable, NetSerializable]
public sealed class DHSetTargetRequestEvent(string category) : EntityEventArgs
{
    public readonly string Category = category;
}

/// <summary>
/// Травмы одной части тела.
/// </summary>
[DataDefinition]
public sealed partial class DHPartTrauma
{
    /// <summary>Механический урон части (удары, порезы, пули).</summary>
    [DataField]
    public float Brute;

    /// <summary>Ожоги и прочий «поверхностный» урон.</summary>
    [DataField]
    public float Burn;

    [DataField]
    public bool Fracture;

    /// <summary>Перелом зафиксирован шиной: последствия слабее, но кость не срослась.</summary>
    [DataField]
    public bool Splinted;

    /// <summary>Потеря крови за тик от внутреннего кровотечения (0 — нет).</summary>
    [DataField]
    public float InternalBleeding;

    /// <summary>Осколков и пуль в части тела.</summary>
    [DataField]
    public int Shrapnel;

    public bool HasInjury => Fracture || InternalBleeding > 0f || Shrapnel > 0;

    public bool IsEmpty => Brute <= 0.01f && Burn <= 0.01f && !HasInjury;
}

/// <summary>
/// Травмы тела. Сетевое только замедление (для предсказания движения), остальное — на сервере.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHTraumaComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, DHPartTrauma> Parts = new();

    /// <summary>Повреждение внутренних органов по категориям (0…<see cref="OrganMaxDamage"/>).</summary>
    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, float> Organs = new();

    /// <summary>Орган с таким повреждением отказывает (как если бы его не было).</summary>
    [DataField]
    public float OrganMaxDamage = 60f;

    /// <summary>Прочность части тела: после неё — переломы чаще, после двойной — возможен отрыв конечности.</summary>
    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, float> PartMax = new()
    {
        ["Head"] = 80f,
        ["Torso"] = 120f,
        ["ArmLeft"] = 60f,
        ["ArmRight"] = 60f,
        ["HandLeft"] = 40f,
        ["HandRight"] = 40f,
        ["LegLeft"] = 70f,
        ["LegRight"] = 70f,
        ["FootLeft"] = 40f,
        ["FootRight"] = 40f,
    };

    /// <summary>Кратковременная боль (удары, операции без наркоза); спадает со временем.</summary>
    [DataField]
    public float PainSpike;

    /// <summary>Обезболивание (морфин и т.п.) и до какого времени оно действует.</summary>
    [DataField]
    public float PainSuppression;

    [DataField]
    public TimeSpan PainSuppressionUntil;

    /// <summary>Внутреннее кровотечение замедлено (транексамовая кислота) до этого времени.</summary>
    [DataField]
    public TimeSpan BleedingSlowedUntil;

    /// <summary>Текущая боль (для осмотра и интерфейса).</summary>
    [ViewVariables]
    public float Pain;

    [DataField]
    public TimeSpan NextUpdate;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>Множитель скорости от переломов ног и боли.</summary>
    [DataField, AutoNetworkedField]
    public float SpeedModifier = 1f;

    /// <summary>
    /// Готовые строки травм для анализатора здоровья (переломы, кровотечения, осколки, органы, боль).
    /// Сервер обновляет раз в секунду и шлёт клиенту, только если изменились.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> Summary = new();
}

/// <summary>
/// Шина: накладывается на сломанную конечность в выбранной зоне.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHSplintComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(4);
}

public static class DHTraumaConstants
{
    public static readonly string[] Parts =
    {
        "Head", "Torso", "ArmLeft", "HandLeft", "ArmRight", "HandRight", "LegLeft", "FootLeft", "LegRight", "FootRight",
    };

    /// <summary>Какие органы внутри какой части тела.</summary>
    public static readonly Dictionary<string, string[]> OrgansIn = new()
    {
        ["Head"] = new[] { "Brain", "Eyes" },
        ["Torso"] = new[] { "Heart", "Lungs", "Liver", "Kidneys", "Stomach" },
    };

    /// <summary>Куда переходит попадание, если части тела нет (кисть → рука → торс).</summary>
    public static readonly Dictionary<string, string> ParentOf = new()
    {
        ["HandLeft"] = "ArmLeft",
        ["HandRight"] = "ArmRight",
        ["FootLeft"] = "LegLeft",
        ["FootRight"] = "LegRight",
        ["ArmLeft"] = "Torso",
        ["ArmRight"] = "Torso",
        ["LegLeft"] = "Torso",
        ["LegRight"] = "Torso",
        ["Head"] = "Torso",
    };
}

[Serializable, NetSerializable]
public sealed partial class DHSplintDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
