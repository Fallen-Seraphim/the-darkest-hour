using Content.Shared._DarkestHour.Assault;
using Content.Shared.Radio;
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
}

/// <summary>Боец состоит в отряде.</summary>
[RegisterComponent]
public sealed partial class DHSquadMemberComponent : Component
{
    [DataField]
    public ProtoId<DHSquadPrototype> Squad;
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
public sealed class DHSquadFighterEntry(NetEntity entity, string name, string job, string? squad, bool alive)
{
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
