using Content.Shared.Access.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Thief;
using Content.Shared.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Vendors;

/// <summary>
/// Вендомат снаряжения в духе морпехов RMC/CMU: разделы с вариантами экипировки, у каждого раздела — лимит выборов
/// на бойца (лимит общий для всех автоматов одной группы), ограничение по должностям и, при желании, конечный запас.
/// Доступ к самому автомату — обычный AccessReader (своя сторона), к разделам — по должности (медик, стрелок…).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHGearVendorComponent : Component
{
    /// <summary>Автоматы одной группы делят лимиты выборов: из второго такого же автомата повторно не взять.</summary>
    [DataField(required: true)]
    public string Group = string.Empty;

    /// <summary>Должности, которым автомат вообще отвечает. Пусто — всем, кого пустил AccessReader.</summary>
    [DataField]
    public List<ProtoId<JobPrototype>> Jobs = new();

    [DataField(required: true)]
    public List<DHGearVendorSection> Sections = new();

    [DataField]
    public SoundSpecifier VendSound = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");

    [DataField]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

    /// <summary>Остаток по позициям с конечным запасом: (раздел, позиция) → сколько осталось. Только на сервере.</summary>
    [ViewVariables]
    public Dictionary<(int Section, int Entry), int> Stock = new();
}

[DataDefinition]
public sealed partial class DHGearVendorSection
{
    /// <summary>Ключ лимита внутри группы; разделы с одинаковым Id в разных автоматах группы делят лимит.</summary>
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>Сколько позиций боец может взять из раздела. 0 — без ограничения.</summary>
    [DataField]
    public int Choices = 1;

    /// <summary>Должности, которым открыт раздел. Пусто — всем.</summary>
    [DataField]
    public List<ProtoId<JobPrototype>> Jobs = new();

    [DataField(required: true)]
    public List<DHGearVendorEntry> Entries = new();
}

[DataDefinition]
public sealed partial class DHGearVendorEntry
{
    /// <summary>Готовый комплект (тот же формат, что и у кейсов выбора снаряжения).</summary>
    [DataField]
    public ProtoId<ThiefBackpackSetPrototype>? Set;

    /// <summary>Одиночный предмет (если не задан Set).</summary>
    [DataField]
    public EntProtoId? Proto;

    /// <summary>Сколько штук Proto выдаётся за один выбор.</summary>
    [DataField]
    public int Count = 1;

    /// <summary>Запас позиции в автомате; -1 — бесконечно.</summary>
    [DataField]
    public int Stock = -1;

    /// <summary>Должности, которым доступна позиция. Пусто — всем, кому открыт раздел.</summary>
    [DataField]
    public List<ProtoId<JobPrototype>> Jobs = new();
}

/// <summary>Сколько уже взято бойцом из разделов (ключ «группа:раздел»). Висит на разуме, чтобы смена тела не сбрасывала.</summary>
[RegisterComponent]
public sealed partial class DHGearVendorUserComponent : Component
{
    [ViewVariables]
    public Dictionary<string, int> Taken = new();
}

[Serializable, NetSerializable]
public enum DHGearVendorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHGearVendorEntryState
{
    public bool Allowed;
    public int Stock;
}

[Serializable, NetSerializable]
public sealed class DHGearVendorSectionState
{
    public bool Allowed;
    public int Taken;
    public int Choices;
    public List<DHGearVendorEntryState> Entries = new();
}

/// <summary>Состояние автомата для конкретного бойца (лимиты у каждого свои).</summary>
[Serializable, NetSerializable]
public sealed class DHGearVendorUserState(List<DHGearVendorSectionState> sections)
{
    public readonly List<DHGearVendorSectionState> Sections = sections;
}

/// <summary>
/// Состояние окна: личные состояния всех, у кого оно открыто (каждый клиент берёт своё).
/// Именно состояние, а не личное сообщение: оно приходит вместе с открытием окна и не теряется,
/// если окно на клиенте ещё не успело создаться.
/// </summary>
[Serializable, NetSerializable]
public sealed class DHGearVendorBuiState(Dictionary<NetEntity, DHGearVendorUserState> users) : BoundUserInterfaceState
{
    public readonly Dictionary<NetEntity, DHGearVendorUserState> Users = users;
}

[Serializable, NetSerializable]
public sealed class DHGearVendorRefreshMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DHGearVendorVendMessage(int section, int entry) : BoundUserInterfaceMessage
{
    public readonly int Section = section;
    public readonly int Entry = entry;
}

/// <summary>Общая часть: не пускает к автомату чужую сторону (AccessReader) — предсказывается и на клиенте.</summary>
public abstract partial class SharedDHGearVendorSystem : EntitySystem
{
    [Dependency] protected AccessReaderSystem Access = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHGearVendorComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
    }

    private void OnOpenAttempt(EntityUid uid, DHGearVendorComponent comp, ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || Access.IsAllowed(args.User, uid))
            return;

        args.Cancel();
        if (!args.Silent)
            Popup.PopupClient(Loc.GetString("dh-gear-vendor-no-access"), uid, args.User);
    }
}
