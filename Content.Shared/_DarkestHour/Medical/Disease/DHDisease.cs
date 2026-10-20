using Content.Shared.EntityEffects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Medical.Disease;

// Болезни The Darkest Hour. Движок по образцу Goob Station Disease (носитель, иммунитет, распространение, защита одеждой),
// но без мутаций и процедурных симптомов: каждая болезнь — фиксированный прототип со стадиями, симптомы стадии — обычные
// EntityEffect апстрима (урон, эмоции, сообщения, рвота, сон…), как у реагентов.

/// <summary>
/// Группа возбудителя: определяет, чем лечится (антибиотики — бактерии, противовирусное — вирусы и т.д.).
/// </summary>
[Serializable, NetSerializable]
public enum DHPathogenGroup : byte
{
    Virus,
    Bacteria,
    Fungus,
    Parasite,
    Chronite,
    Radiation,
}

/// <summary>
/// Путь передачи.
/// </summary>
[Serializable, NetSerializable]
public enum DHSpreadVector : byte
{
    /// <summary>Воздушно-капельный: рядом стоящие, защищают маски и капюшоны.</summary>
    Airborne,
    /// <summary>Контакт: вплотную, защищают перчатки и костюмы.</summary>
    Contact,
    /// <summary>Кровь: рубящие и колющие удары заражённого в ближнем бою.</summary>
    Blood,
}

/// <summary>
/// Основное лечение болезни — вариант, который вирусолог выбирает в карточке случая.
/// </summary>
[Serializable, NetSerializable]
public enum DHTreatmentKind : byte
{
    /// <summary>Покой, организм справится сам (противовирусное ускоряет).</summary>
    Rest,
    /// <summary>Вакцина (рецепт печатает компьютер вирусолога).</summary>
    Vaccine,
    Antibiotic,
    ReserveAntibiotic,
    Antifungal,
    Antiparasitic,
    /// <summary>Хирургическое извлечение.</summary>
    Surgery,
}

[DataDefinition]
public sealed partial class DHDiseaseStage
{
    /// <summary>Симптомы стадии; применяются раз в <see cref="Interval"/>, у каждого свой шанс (probability).</summary>
    [DataField]
    public EntityEffect[] Effects = [];

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(5);

    /// <summary>Заразна ли болезнь на этой стадии (инкубация обычно нет).</summary>
    [DataField]
    public bool Contagious = true;

    /// <summary>Видна ли стадия на медицинском HUD (инкубация не видна).</summary>
    [DataField]
    public bool Symptomatic = true;
}

[Prototype("dhDisease")]
public sealed partial class DHDiseasePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    [DataField(required: true)]
    public DHPathogenGroup Group;

    [DataField]
    public List<DHSpreadVector> Spread = new();

    /// <summary>Шанс заразить при одном контакте (до защиты одеждой).</summary>
    [DataField]
    public float Infectivity = 0.2f;

    /// <summary>Рост прогресса в секунду; прогресс N = стадия N (0 — первая стадия).</summary>
    [DataField]
    public float ProgressRate = 0.005f;

    /// <summary>Рост иммунного ответа в секунду (0…1); 0 — организм сам не справится.</summary>
    [DataField]
    public float ImmuneGain = 0.001f;

    /// <summary>Снижение прогресса в секунду при полном иммунном ответе.</summary>
    [DataField]
    public float ImmuneStrength = 0.01f;

    /// <summary>После выздоровления — иммунитет к этой болезни (вирусы).</summary>
    [DataField]
    public bool GrantsImmunity;

    [DataField(required: true)]
    public List<DHDiseaseStage> Stages = new();

    // --- Вирусология ---

    /// <summary>Определяется вирусологом (лучевая и хронитовая — нет: их видно по дозиметру).</summary>
    [DataField]
    public bool Identifiable = true;

    /// <summary>Наблюдаемые симптомы (для сканера тела и карточки случая).</summary>
    [DataField]
    public List<LocId> Symptoms = new();

    /// <summary>Характерные признаки строения (распечатка анализатора, вдобавок к маркерам группы).</summary>
    [DataField]
    public LocId? Structure;

    [DataField]
    public DHTreatmentKind Treatment = DHTreatmentKind.Rest;

    /// <summary>Текст рецепта, который печатает компьютер вирусолога при верном определении.</summary>
    [DataField]
    public LocId? Recipe;

    /// <summary>Очки исследований за первое верное определение за раунд.</summary>
    [DataField]
    public int ResearchPoints = 5000;
}

/// <summary>
/// Текущее течение болезни у носителя.
/// </summary>
[DataDefinition]
public sealed partial class DHActiveDisease
{
    /// <summary>0…число стадий; целая часть — номер стадии. Ноль и ниже — выздоровел.</summary>
    [DataField]
    public float Progress = 0.01f;

    /// <summary>Иммунный ответ 0…1.</summary>
    [DataField]
    public float Immunity;

    [DataField]
    public TimeSpan NextSymptoms;
}

/// <summary>
/// Может болеть и заражать. Сетевое только «есть симптомы» — для значка на медицинском HUD.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHDiseaseCarrierComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<DHDiseasePrototype>, DHActiveDisease> Diseases = new();

    /// <summary>Болезни, к которым есть иммунитет (переболел, вакцина).</summary>
    [DataField]
    public HashSet<ProtoId<DHDiseasePrototype>> Immune = new();

    [DataField, AutoNetworkedField]
    public bool Symptomatic;

    [DataField]
    public TimeSpan NextUpdate;

    [DataField]
    public TimeSpan NextSpread;
}

/// <summary>
/// Биозащита одежды: доля заражений, которую она останавливает (0…1). Защита надетых вещей складывается
/// (1 − Π(1 − p)), максимум 95%.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHBioProtectionComponent : Component
{
    [DataField]
    public float Airborne;

    [DataField]
    public float Contact;
}
