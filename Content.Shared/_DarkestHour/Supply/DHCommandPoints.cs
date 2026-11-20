using Content.Shared._DarkestHour.Assault;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Supply;

// Очки командования и древо «Осады» (план осады п. 3, решения автора 2026-11-19). Очки капают за удержание POI (у
// каждой точки своя ценность) и за первый захват точки стороной. Снабжение тратит их на пополнение карго по
// разделам, командование — на узлы древа: узел открывает позиции каталога (со стартовым запасом) или улучшает
// снабжение. Код — DHCommandPointsSystem и DHSupplySystem.

/// <summary>Узел древа командования стороны.</summary>
[Prototype("dhSupplyNode")]
public sealed partial class DHSupplyNodePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public DHAssaultFaction Faction = DHAssaultFaction.Republic;

    /// <summary>Ветка (ключ локализации): снабжение, огонь, техника, медицина, разведка.</summary>
    [DataField(required: true)]
    public LocId Branch;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    /// <summary>Порядок внутри ветки (ступень).</summary>
    [DataField]
    public int Tier = 1;

    [DataField]
    public int Cost = 100;

    /// <summary>Какие узлы должны быть открыты раньше.</summary>
    [DataField]
    public List<ProtoId<DHSupplyNodePrototype>> Requires = new();

    /// <summary>Позиции каталога стороны (<see cref="DHSupplyEntry.Id"/>), закрытые до открытия узла.</summary>
    [DataField]
    public List<string> Unlocks = new();

    /// <summary>Склады: потолок запаса всех позиций растёт на эту долю (0.5 — в полтора раза).</summary>
    [DataField]
    public float MaxBonus;

    /// <summary>Лифт: время подъёма и спуска умножается на это (0.5 — вдвое быстрее). 1 — без изменений.</summary>
    [DataField]
    public float ElevatorTime = 1f;
}

/// <summary>Покупка пополнения раздела каталога за очки командования.</summary>
[DataDefinition]
public sealed partial class DHSupplyRestock
{
    /// <summary>Раздел каталога (<see cref="DHSupplyEntry.Category"/>).</summary>
    [DataField(required: true)]
    public LocId Category;

    [DataField(required: true)]
    public int Price;
}

/// <summary>Компьютер штаба: древо командования стороны (открывает только командование — по доступу ID).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHCommandTreeConsoleComponent : Component
{
    [DataField]
    public DHAssaultFaction Faction = DHAssaultFaction.Republic;
}

[Serializable, NetSerializable]
public enum DHCommandTreeUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum DHCommandTreeNodeState : byte
{
    Opened,
    Available,

    /// <summary>Сначала нужны другие узлы.</summary>
    Locked,
}

[Serializable, NetSerializable]
public sealed class DHCommandTreeNodeRow(
    string id,
    string branch,
    string name,
    string? description,
    int tier,
    int cost,
    DHCommandTreeNodeState state,
    List<string> requires,
    List<string> unlocks)
{
    public readonly string Id = id;
    public readonly string Branch = branch;
    public readonly string Name = name;
    public readonly string? Description = description;
    public readonly int Tier = tier;
    public readonly int Cost = cost;
    public readonly DHCommandTreeNodeState State = state;

    /// <summary>Названия узлов, которые нужны раньше (локализованные ключи).</summary>
    public readonly List<string> Requires = requires;

    /// <summary>Прототипы того, что откроется в каталоге.</summary>
    public readonly List<string> Unlocks = unlocks;
}

[Serializable, NetSerializable]
public sealed class DHCommandTreeState(int points, int income, List<DHCommandTreeNodeRow> nodes)
    : BoundUserInterfaceState
{
    public readonly int Points = points;

    /// <summary>Очков в минуту за удерживаемые точки.</summary>
    public readonly int Income = income;

    public readonly List<DHCommandTreeNodeRow> Nodes = nodes;
}

[Serializable, NetSerializable]
public sealed class DHCommandTreeUnlockMessage(string node) : BoundUserInterfaceMessage
{
    public readonly string Node = node;
}

/// <summary>Купить пополнение раздела карго за очки командования.</summary>
[Serializable, NetSerializable]
public sealed class DHSupplyRestockMessage(string category) : BoundUserInterfaceMessage
{
    public readonly string Category = category;
}

[Serializable, NetSerializable]
public sealed class DHSupplyRestockRow(string category, int price)
{
    public readonly string Category = category;
    public readonly int Price = price;
}
