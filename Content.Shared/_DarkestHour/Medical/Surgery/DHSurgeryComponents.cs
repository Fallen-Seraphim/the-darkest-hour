using Content.Shared.Body;
using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Medical.Surgery;

/// <summary>
/// Хирургический инструмент.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHSurgicalToolComponent : Component
{
    [DataField(required: true)]
    public DHSurgicalToolKind Kind;

    /// <summary>Множитель скорости шага (больше — быстрее).</summary>
    [DataField]
    public float Speed = 1f;

    /// <summary>Шанс, что шаг выйдет с первого раза.</summary>
    [DataField]
    public float Success = 1f;

    /// <summary>Сам прижигает разрез (лазерный скальпель): разрез не кровит.</summary>
    [DataField]
    public bool Cauterizes;
}

/// <summary>
/// Простыня для операции: «использовать» на пациенте — открыть окно хирургии.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHSurgicalDrapesComponent : Component;

/// <summary>
/// Поверхность для операций. Пациент, пристёгнутый к ней, оперируется с этим множителем времени.
/// Прочие кровати и каталки — <see cref="DHSurgeryConstants.BedMultiplier"/>, пол — <see cref="DHSurgeryConstants.FloorMultiplier"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHSurgicalTableComponent : Component
{
    [DataField]
    public float TimeMultiplier = 1f;

    /// <summary>Дополнительный риск инфекции (стерильный стол — 0).</summary>
    [DataField]
    public float InfectionRisk;
}

public enum DHSterileSlot : byte
{
    Mask,
    Gloves,
}

/// <summary>
/// Стерильная одежда хирурга (маска или перчатки). Без неё вскрытие тканей грозит раневой инфекцией.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHSterileClothingComponent : Component
{
    [DataField(required: true)]
    public DHSterileSlot Slot;
}

/// <summary>
/// Пациент, на котором идёт или шла операция: состояние каждой части тела по категории.
/// </summary>
[RegisterComponent]
public sealed partial class DHSurgeryPatientComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, DHSurgeryPartProgress> Parts = new();
}

[DataDefinition]
public sealed partial class DHSurgeryPartProgress
{
    [DataField]
    public DHPartState State;

    /// <summary>Идущая операция на этой части.</summary>
    [DataField]
    public ProtoId<DHSurgeryPrototype>? Active;

    /// <summary>Сделанные шаги идущей операции (индексы).</summary>
    [DataField]
    public HashSet<int> Done = new();

    public bool IsEmpty => State == DHPartState.None && Active == null;
}

/// <summary>
/// Хирург: какую часть тела какого пациента он выбрал в окне хирургии. Нужен, чтобы шаг можно было сделать,
/// просто применив инструмент к пациенту.
/// </summary>
[RegisterComponent]
public sealed partial class DHSurgeonComponent : Component
{
    [DataField]
    public EntityUid? Patient;

    [DataField]
    public ProtoId<OrganCategoryPrototype>? Part;
}

/// <summary>
/// Жизненно важные органы: если органа нет в теле, пациент получает урон (нет сердца — нет кровообращения и т.д.).
/// Пока органов хватает, ничего не тикает.
/// </summary>
[RegisterComponent]
public sealed partial class DHVitalOrgansComponent : Component
{
    /// <summary>Урон каждые <see cref="Interval"/>, пока органа этой категории нет.</summary>
    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, DamageSpecifier> Missing = new();

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan NextUpdate;

    /// <summary>Каких органов сейчас нет (пересчитывается при вставке и извлечении).</summary>
    [ViewVariables]
    public HashSet<ProtoId<OrganCategoryPrototype>> Absent = new();
}

public static class DHSurgeryConstants
{
    public const float BedMultiplier = 1.5f;
    public const float FloorMultiplier = 2f;
    public const float FloorInfectionRisk = 0.1f;
    public const float NoMaskInfectionRisk = 0.15f;
    public const float NoGlovesInfectionRisk = 0.25f;

    /// <summary>Доля боли, переходящая в шанс сорвать шаг у пациента без наркоза.</summary>
    public const float PainFailFactor = 0.6f;

    /// <summary>Порядок частей тела в окне хирургии.</summary>
    public static readonly string[] PartOrder =
    {
        "Head", "Torso", "ArmLeft", "HandLeft", "ArmRight", "HandRight", "LegLeft", "FootLeft", "LegRight", "FootRight",
    };
}
