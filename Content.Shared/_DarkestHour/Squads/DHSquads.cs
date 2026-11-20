using Content.Shared._DarkestHour.Assault;
using Content.Shared.Alert;
using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Squads;

// Отряды «Штурма» (решение автора 2026-11-05): у Республики — Альфа, Браво, Чарли, Дельта, у Империи — Тау, Эпсилон,
// Омега. Бойцов распределяет командование через консоль отрядов; у каждого отряда своя рация — при назначении в
// гарнитуре меняется отрядный ключ (нет — добавляется).

/// <summary>Отряд стороны.</summary>
[Prototype("dhSquad")]
public sealed partial class DHSquadPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public DHAssaultFaction Faction;

    /// <summary>Канал рации отряда.</summary>
    [DataField(required: true)]
    public ProtoId<RadioChannelPrototype> Channel;

    /// <summary>Ключ шифрования отряда, который вставляется в гарнитуру.</summary>
    [DataField(required: true)]
    public EntProtoId Key;

    [DataField]
    public Color Color = Color.White;

    /// <summary>Порядок в списке.</summary>
    [DataField]
    public int Order;

    /// <summary>Алерт трекера командира отряда (стрелка цвета отряда).</summary>
    [DataField]
    public ProtoId<AlertPrototype>? TrackerAlert;

    /// <summary>Значок отряда в худе (у командира — свой, с латунной рамкой).</summary>
    [DataField]
    public ProtoId<Content.Shared.StatusIcon.StatusIconPrototype>? Icon;

    [DataField]
    public ProtoId<Content.Shared.StatusIcon.StatusIconPrototype>? LeaderIcon;
}

/// <summary>
/// Кто и как распределяется по отрядам стороны на старте раунда (решение автора 2026-11-19, как в RMC): только пехота.
/// Лидеры (командиры отрядов Империи, офицеры Республики) — по одному на отряд и сразу командиры; бойцы «вразнос»
/// (медики, пулемётчики, спецы) расходятся по отрядам поровну; остальные — в любимый отряд, если он не больше самого
/// маленького более чем на <see cref="MaxImbalance"/>.
/// </summary>
[Prototype("dhSquadRoster")]
public sealed partial class DHSquadRosterPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public DHAssaultFaction Faction;

    /// <summary>Должности лидеров отрядов.</summary>
    [DataField]
    public List<ProtoId<Content.Shared.Roles.JobPrototype>> Leaders = new();

    /// <summary>Должности, которые расходятся по отрядам поровну.</summary>
    [DataField]
    public List<ProtoId<Content.Shared.Roles.JobPrototype>> Spread = new();

    /// <summary>Остальная пехота отрядов.</summary>
    [DataField]
    public List<ProtoId<Content.Shared.Roles.JobPrototype>> Members = new();

    /// <summary>Насколько любимый отряд может быть больше самого маленького.</summary>
    [DataField]
    public int MaxImbalance = 2;
}

/// <summary>Боец состоит в отряде. Сетевой: клиент рисует значок отряда в худе и нашивку цвета отряда.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class DHSquadMemberComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<DHSquadPrototype> Squad;

    /// <summary>Командир отряда: на него указывает трекер у остальных бойцов отряда.</summary>
    [DataField, AutoNetworkedField]
    public bool Leader;
}

/// <summary>Консоль отрядов: командование стороны распределяет бойцов по отрядам.</summary>
[RegisterComponent]
public sealed partial class DHSquadConsoleComponent : Component
{
    [DataField(required: true)]
    public DHAssaultFaction Faction;
}

[Serializable, NetSerializable]
public enum DHSquadConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHSquadInfo(string id, string name, Color color)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly Color Color = color;
}

[Serializable, NetSerializable]
public sealed class DHSquadFighterEntry(NetEntity entity, string name, string job, string? squad, bool alive,
    bool leader)
{
    public readonly bool Leader = leader;
    public readonly NetEntity Entity = entity;
    public readonly string Name = name;
    public readonly string Job = job;
    public readonly string? Squad = squad;
    public readonly bool Alive = alive;
}

[Serializable, NetSerializable]
public sealed class DHSquadConsoleState(List<DHSquadInfo> squads, List<DHSquadFighterEntry> fighters)
    : BoundUserInterfaceState
{
    public readonly List<DHSquadInfo> Squads = squads;
    public readonly List<DHSquadFighterEntry> Fighters = fighters;
}

/// <summary>Назначить бойца в отряд (null — вывести из отряда).</summary>
[Serializable, NetSerializable]
public sealed class DHSquadAssignMessage(NetEntity fighter, string? squad) : BoundUserInterfaceMessage
{
    public readonly NetEntity Fighter = fighter;
    public readonly string? Squad = squad;
}

/// <summary>Назначить бойца командиром его отряда (прежний командир отряда снимается).</summary>
[Serializable, NetSerializable]
public sealed class DHSquadSetLeaderMessage(NetEntity fighter) : BoundUserInterfaceMessage
{
    public readonly NetEntity Fighter = fighter;
}
