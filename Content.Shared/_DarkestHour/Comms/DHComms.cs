using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Comms;

// Связь «Штурма» (решение автора 2026-10-25, переделано 2026-11-05): каналы сторон и командные работают только там,
// где стоит живой ретранслятор стороны. Зона ретранслятора — его карта; поверхность планеты вместе с бункером и небом
// над ней — одна зона, отсек машины — зона, где стоит машина. Сообщение доходит, если зона отправителя и зона
// получателя обе покрыты. Разрушил вышку — связь пропала; Империя приносит на планету переносной ретранслятор. Глушилки — ванильные. Полевые телефоны — проводная связь между
// точками своей сети, работает и при глушении.

/// <summary>
/// Ретранслятор: пока цел, каналы <see cref="Channels"/> проходят между картами. Разбитый остаётся остовом с
/// <see cref="Active"/> = false — канал по-прежнему требует ретранслятора, но живого больше нет.
/// </summary>
[RegisterComponent]
public sealed partial class DHCommsRelayComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<RadioChannelPrototype>> Channels = new();

    [DataField]
    public bool Active = true;
}

/// <summary>Переносной ретранслятор в сложенном виде: развернуть в руке — встанет на месте.</summary>
[RegisterComponent]
public sealed partial class DHPortableRelayFoldedComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Deployed;

    [DataField]
    public TimeSpan DeployTime = TimeSpan.FromSeconds(6);
}

/// <summary>Развёрнутый переносной ретранслятор: сворачивается альтернативным действием.</summary>
[RegisterComponent]
public sealed partial class DHPortableRelayComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Folded;

    [DataField]
    public TimeSpan FoldTime = TimeSpan.FromSeconds(6);
}

[Serializable, NetSerializable]
public sealed partial class DHPortableRelayDeployDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHPortableRelayFoldDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum DHCommsRelayVisuals : byte
{
    /// <summary>bool: ретранслятор цел и работает.</summary>
    Active,
}

/// <summary>Полевой телефон: звонит только телефонам своей сети.</summary>
[RegisterComponent]
public sealed partial class DHFieldPhoneComponent : Component
{
    [DataField(required: true)]
    public string Network = string.Empty;

    /// <summary>Имя в справочнике; пусто — имя сущности.</summary>
    [DataField]
    public string PhoneName = string.Empty;

    /// <summary>
    /// Без <see cref="PhoneName"/> брать имя самой сущности, а не грида (общие телефоны: маппер ставит и
    /// переименовывает каждый — «rename» или VV → MetaData → Name, имя сохраняется в карте).
    /// </summary>
    [DataField]
    public bool UseEntityName;
}

[Serializable, NetSerializable]
public enum DHFieldPhoneUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum DHFieldPhoneStatus : byte
{
    Idle,
    Calling,
    Ringing,
    InCall,
}

[Serializable, NetSerializable]
public sealed class DHFieldPhoneEntry(NetEntity phone, string name, bool busy)
{
    public readonly NetEntity Phone = phone;
    public readonly string Name = name;
    public readonly bool Busy = busy;
}

[Serializable, NetSerializable]
public sealed class DHFieldPhoneState(string name, DHFieldPhoneStatus status, string? other, List<DHFieldPhoneEntry> book)
    : BoundUserInterfaceState
{
    public readonly string Name = name;
    public readonly DHFieldPhoneStatus Status = status;

    /// <summary>С кем говорим / кто звонит / кому звоним.</summary>
    public readonly string? Other = other;

    public readonly List<DHFieldPhoneEntry> Book = book;
}

[Serializable, NetSerializable]
public sealed class DHFieldPhoneCallMessage(NetEntity phone) : BoundUserInterfaceMessage
{
    public readonly NetEntity Phone = phone;
}

[Serializable, NetSerializable]
public sealed class DHFieldPhoneAnswerMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DHFieldPhoneHangUpMessage : BoundUserInterfaceMessage;

/// <summary>
/// Взламываемый ретранслятор (нейтральная антенна ЗП-1 «Восстания», решение автора 2026-11-05): пока не взломан,
/// его каналы молчат. Взлом — взломщиком антенн (он у главы отряда ОФСК), восстановление — мультитулом.
/// </summary>
[RegisterComponent]
public sealed partial class DHHackableRelayComponent : Component
{
    [DataField]
    public bool Hacked;

    [DataField]
    public TimeSpan HackTime = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan RestoreTime = TimeSpan.FromSeconds(12);
}

/// <summary>Взломщик антенн.</summary>
[RegisterComponent]
public sealed partial class DHRelayHackerComponent : Component;

[Serializable, NetSerializable]
public sealed partial class DHRelayHackDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHRelayRestoreDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
