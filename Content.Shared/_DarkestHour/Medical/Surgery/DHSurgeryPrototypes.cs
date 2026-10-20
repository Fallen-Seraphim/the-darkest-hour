using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Tools;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Medical.Surgery;

// Хирургия The Darkest Hour. Своя реализация на новом теле апстрима (органы-сущности), по образцу Shitmed (Goob Station):
// операция — цепочка шагов, каждый шаг требует своего инструмента; состояние части тела (разрез, зажимы, ранорасширитель,
// вскрытая кость) хранится на органе-части тела.

/// <summary>
/// Состояние вскрытой части тела.
/// </summary>
[Flags, Serializable, NetSerializable]
public enum DHPartState : byte
{
    None = 0,
    /// <summary>Кожа разрезана.</summary>
    Incised = 1 << 0,
    /// <summary>Сосуды пережаты.</summary>
    Clamped = 1 << 1,
    /// <summary>Края раны разведены.</summary>
    Retracted = 1 << 2,
    /// <summary>Кость (рёбра, череп) вскрыта.</summary>
    BoneOpen = 1 << 3,
}

public static class DHPartStates
{
    public static DHPartState Combine(List<DHPartState> states)
    {
        var result = DHPartState.None;
        foreach (var state in states)
        {
            result |= state;
        }

        return result;
    }
}

/// <summary>
/// Вид хирургического инструмента.
/// </summary>
[Serializable, NetSerializable]
public enum DHSurgicalToolKind : byte
{
    Scalpel,
    Hemostat,
    Retractor,
    Saw,
    Drill,
    Cautery,
    BoneGel,
    BoneSetter,
}

/// <summary>
/// Особое действие шага помимо смены состояния части тела.
/// </summary>
[Serializable, NetSerializable]
public enum DHSurgeryEffect : byte
{
    None,
    /// <summary>Извлечь орган <see cref="DHSurgeryPrototype.Organ"/> в руку хирурга.</summary>
    RemoveOrgan,
    /// <summary>Вставить орган из руки хирурга.</summary>
    InsertOrgan,
    /// <summary>Отделить часть тела вместе с её дочерними частями.</summary>
    Amputate,
    /// <summary>Приживить отделённую часть тела из руки хирурга.</summary>
    AttachLimb,
}

/// <summary>
/// Подходящий для шага инструмент: свой хирургический вид или подручный инструмент с качеством (нож, лом, сварка).
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class DHSurgeryToolEntry
{
    [DataField]
    public DHSurgicalToolKind? Kind;

    /// <summary>Подручный инструмент: качество из ToolComponent.</summary>
    [DataField]
    public ProtoId<ToolQualityPrototype>? Quality;

    /// <summary>Множитель скорости для подручного инструмента (у хирургических — из DHSurgicalTool).</summary>
    [DataField]
    public float Speed = 1f;

    /// <summary>Шанс успеха для подручного инструмента.</summary>
    [DataField]
    public float Success = 1f;
}

/// <summary>
/// Шаг операции.
/// </summary>
[Prototype("dhSurgeryStep")]
public sealed partial class DHSurgeryStepPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>Подсказка, чем делать шаг.</summary>
    [DataField]
    public LocId? ToolHint;

    /// <summary>Чем можно сделать шаг. Пусто — шаг делается предметом-органом/частью тела в руке (вставка/приживление).</summary>
    [DataField]
    public List<DHSurgeryToolEntry> Tools = new();

    /// <summary>Базовая длительность на операционном столе хирургическим инструментом.</summary>
    [DataField]
    public float Duration = 3f;

    /// <summary>Насколько больно без наркоза (0…1): шанс крика и того, что пациент дёрнется и шаг сорвётся.</summary>
    [DataField]
    public float Pain;

    /// <summary>Шаг вскрывает ткани: без маски и перчаток хирурга — риск раневой инфекции.</summary>
    [DataField]
    public bool Invasive;

    /// <summary>Изменение кровотечения пациента после шага (минус — останавливает).</summary>
    [DataField]
    public float Bleed;

    [DataField("add")]
    public List<DHPartState> AddList = new();

    [DataField("remove")]
    public List<DHPartState> RemoveList = new();

    /// <summary>Шаг пропускается, если у части тела нет всех этих состояний.</summary>
    [DataField("onlyIf")]
    public List<DHPartState> OnlyIfList = new();

    public DHPartState Add => DHPartStates.Combine(AddList);
    public DHPartState Remove => DHPartStates.Combine(RemoveList);
    public DHPartState OnlyIf => DHPartStates.Combine(OnlyIfList);

    [DataField]
    public DHSurgeryEffect Effect = DHSurgeryEffect.None;

    /// <summary>Урон пациенту, если шаг сорвался.</summary>
    [DataField]
    public DamageSpecifier? FailDamage;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>
/// Операция: какие части тела, при каком состоянии, из каких шагов.
/// </summary>
[Prototype("dhSurgery")]
public sealed partial class DHSurgeryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    /// <summary>Порядок в списке операций.</summary>
    [DataField]
    public int Order;

    [DataField(required: true)]
    public List<ProtoId<OrganCategoryPrototype>> Parts = new();

    /// <summary>false — операция для отсутствующей части тела (приживление).</summary>
    [DataField]
    public bool PartPresent = true;

    /// <summary>Для отсутствующей части: к какой части тела приживлять (рука → торс, кисть → рука).</summary>
    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, ProtoId<OrganCategoryPrototype>> AttachTo = new();

    [DataField("requires")]
    public List<DHPartState> RequiresList = new();

    [DataField("forbids")]
    public List<DHPartState> ForbidsList = new();

    public DHPartState Requires => DHPartStates.Combine(RequiresList);
    public DHPartState Forbids => DHPartStates.Combine(ForbidsList);

    /// <summary>Орган, с которым работает операция (извлечь/вставить).</summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype>? Organ;

    /// <summary>true — орган должен быть в теле, false — должен отсутствовать.</summary>
    [DataField]
    public bool OrganPresent = true;

    [DataField(required: true)]
    public List<ProtoId<DHSurgeryStepPrototype>> Steps = new();
}
