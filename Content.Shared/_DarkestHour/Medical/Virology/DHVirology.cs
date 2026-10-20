using Content.Shared._DarkestHour.Medical.Disease;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Medical.Virology;

// Вирусология The Darkest Hour (по задумке автора): мазок → анализатор печатает строение → вирусолог в компьютере
// заполняет карточку случая (группа, путь передачи, симптомы, лечение). Верно — очки исследований станции, болезнь
// становится известной (значок на медицинском HUD) и печатается рецепт, который вирусолог несёт химикам.

/// <summary>
/// Стерильная палочка для мазка: с человека или из банки с культурой. Одноразовая.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHPathogenSwabComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Used;

    /// <summary>Возбудители в мазке.</summary>
    [DataField]
    public List<ProtoId<DHDiseasePrototype>> Samples = new();

    /// <summary>У кого/откуда взят мазок (для распечатки).</summary>
    [DataField, AutoNetworkedField]
    public string? Source;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);
}

/// <summary>
/// Ручной сканер тела: есть ли возбудитель и какие симптомы, без названия болезни.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHPathogenScannerComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);
}

/// <summary>
/// Анализатор: мазок в слот → через время печатает строение возбудителей. Мазок расходуется.
/// </summary>
[RegisterComponent]
public sealed partial class DHPathogenAnalyzerComponent : Component
{
    [DataField]
    public string SlotId = "swab";

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan? AnalysisEnd;

    [DataField]
    public EntProtoId PaperPrototype = "Paper";
}

[Serializable, NetSerializable]
public enum DHPathogenAnalyzerVisuals : byte
{
    Running,
}

/// <summary>
/// Банка/резервуар с культурой возбудителя (карго, экспедиции). Из неё берут мазок; разбитая заражает всех рядом.
/// </summary>
[RegisterComponent]
public sealed partial class DHCultureJarComponent : Component
{
    [DataField(required: true)]
    public ProtoId<DHDiseasePrototype> Disease;

    [DataField]
    public float BreakRange = 2.5f;

    [DataField]
    public float BreakChance = 0.7f;
}

/// <summary>
/// Компьютер вирусолога.
/// </summary>
[RegisterComponent]
public sealed partial class DHVirologyConsoleComponent : Component
{
    [DataField]
    public TimeSpan NextSubmit;

    /// <summary>Пауза после неверной карточки.</summary>
    [DataField]
    public TimeSpan WrongCooldown = TimeSpan.FromSeconds(30);

    [DataField]
    public string? LastResult;

    [DataField]
    public bool LastSuccess;

    [DataField]
    public EntProtoId PaperPrototype = "Paper";
}

[Serializable, NetSerializable]
public enum DHVirologyConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHVirologySymptomData(string id, string name)
{
    public readonly string Id = id;
    public readonly string Name = name;
}

[Serializable, NetSerializable]
public sealed class DHVirologyConsoleState : BoundUserInterfaceState
{
    public List<DHVirologySymptomData> Symptoms = new();
    public List<string> KnownDiseases = new();
    public string? LastResult;
    public bool LastSuccess;
    public float CooldownSeconds;
}

/// <summary>Карточка случая.</summary>
[Serializable, NetSerializable]
public sealed class DHVirologySubmitMessage(
    DHPathogenGroup group,
    List<DHSpreadVector> spread,
    List<string> symptoms,
    DHTreatmentKind treatment) : BoundUserInterfaceMessage
{
    public readonly DHPathogenGroup Group = group;
    public readonly List<DHSpreadVector> Spread = spread;
    public readonly List<string> Symptoms = symptoms;
    public readonly DHTreatmentKind Treatment = treatment;
}

[Serializable, NetSerializable]
public sealed partial class DHSwabDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHPathogenScanDoAfterEvent : SimpleDoAfterEvent;
