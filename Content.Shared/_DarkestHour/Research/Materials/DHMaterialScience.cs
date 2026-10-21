using Content.Shared.Materials;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Research.Materials;

// Материаловедение (этап 4, по задумке автора из полного плана): на лабораторном столе материаловед собирает
// кристаллическую решётку — тип решётки, атомы в узлах и электроны на внешней оболочке. Стол сверяет сборку с
// рецептом материала и даёт подсказки; удачная сборка расходует исходные материалы и даёт образец. Образец идёт в
// химико-материальный сканер: тот печатает технодиск с рецептами (например, сплавы для сплавной печи) и начисляет
// очки исследований. Число электронов меняется каждый раунд, чтобы ответы не уходили на вики.

[Serializable, NetSerializable]
public enum DHLatticeType : byte
{
    Cubic,
    Hexagonal,
    Tetragonal,
    Amorphous,
    /// <summary>Четырёхмерная решётка хронита.</summary>
    Tesseract,
}

[Serializable, NetSerializable]
public enum DHElement : byte
{
    None,
    Fe,
    C,
    H,
    Al,
    Ti,
    Cu,
    Ni,
    Si,
    U,
    /// <summary>Хронит.</summary>
    Chr,
}

/// <summary>
/// Рецепт материала: что собрать на лабораторном столе и что получится.
/// </summary>
[Prototype("dhMaterialRecipe")]
public sealed partial class DHMaterialRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public DHLatticeType Lattice;

    /// <summary>Атомы в узлах решётки (порядок не важен, повторы — несколько узлов одного элемента).</summary>
    [DataField(required: true)]
    public List<DHElement> Elements = new();

    /// <summary>Сколько исходных материалов уходит на удачную сборку (в единицах материала, 100 = лист).</summary>
    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, int> Consumes = new();

    /// <summary>Диапазон электронов на внешней оболочке; точное число выпадает в начале раунда для каждого элемента.</summary>
    [DataField]
    public int MinElectrons = 1;

    [DataField]
    public int MaxElectrons = 8;

    /// <summary>Технодиск, который печатает сканер по образцу.</summary>
    [DataField]
    public EntProtoId? Disk;

    /// <summary>Очки исследований за первый образец за раунд.</summary>
    [DataField]
    public int ResearchPoints = 5000;

    /// <summary>Порядок в списке стола.</summary>
    [DataField]
    public int Order;
}

/// <summary>
/// Материаловедческий лабораторный стол: сборка решётки. Исходные материалы вставляются в стол (MaterialStorage).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHMaterialLabComponent : Component
{
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(4);

    [DataField]
    public EntProtoId SamplePrototype = "DHMaterialSample";

    [ViewVariables]
    public TimeSpan NextAttempt;

    [ViewVariables]
    public string? LastResult;

    [ViewVariables]
    public bool LastSuccess;
}

/// <summary>
/// Образец материала с лабораторного стола.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHMaterialSampleComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<DHMaterialRecipePrototype>? Recipe;
}

/// <summary>
/// Химико-материальный сканер: образец → технодиск с рецептами и очки исследований.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHMaterialScannerComponent : Component
{
    [DataField]
    public string SlotId = "sample";

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    [DataField]
    public EntProtoId PaperPrototype = "Paper";

    [ViewVariables]
    public TimeSpan? ScanEnd;
}

[Serializable, NetSerializable]
public enum DHMaterialScannerVisuals : byte
{
    Running,
}

[Serializable, NetSerializable]
public enum DHMaterialLabUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHMaterialRecipeEntry(string id, string name, int nodes, string consumes, bool canAfford)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly int Nodes = nodes;
    public readonly string Consumes = consumes;
    public readonly bool CanAfford = canAfford;
}

[Serializable, NetSerializable]
public sealed class DHMaterialLabState(List<DHMaterialRecipeEntry> recipes, string materials, string? lastResult, bool lastSuccess)
    : BoundUserInterfaceState
{
    public readonly List<DHMaterialRecipeEntry> Recipes = recipes;
    public readonly string Materials = materials;
    public readonly string? LastResult = lastResult;
    public readonly bool LastSuccess = lastSuccess;
}

/// <summary>Попытка собрать решётку материала.</summary>
[Serializable, NetSerializable]
public sealed class DHMaterialLabAttemptMessage(string recipe, DHLatticeType lattice, List<DHElement> elements, List<int> electrons)
    : BoundUserInterfaceMessage
{
    public readonly string Recipe = recipe;
    public readonly DHLatticeType Lattice = lattice;
    public readonly List<DHElement> Elements = elements;
    public readonly List<int> Electrons = electrons;
}
