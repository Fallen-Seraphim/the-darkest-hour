using Content.Shared.CartridgeLoader;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.City;

// «Восстание», телефоны и терминалы (решение автора 2026-11-05): переносной терминал — телефон с приложениями
// (заметки, сообщения, банк LA-V, звонки); стационарный терминал — то же без звонков. Приложения — картриджи
// загрузчика программ (как у КПК), встроенные и несъёмные.

/// <summary>Телефон или терминал: номер и владелец.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHPhoneComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Number = string.Empty;

    [DataField, AutoNetworkedField]
    public string OwnerName = string.Empty;

    /// <summary>Стационарный терминал: без звонков, имя — по месту.</summary>
    [DataField, AutoNetworkedField]
    public bool Stationary;
}

[Serializable, NetSerializable]
public enum DHPhoneUiKey : byte
{
    Key,
}

/// <summary>Абонент в списке контактов.</summary>
[Serializable, NetSerializable]
public sealed class DHPhoneContact(string number, string name, int unread, bool busy)
{
    public readonly string Number = number;
    public readonly string Name = name;
    public readonly int Unread = unread;
    public readonly bool Busy = busy;
}

// ---------------------------------------------------------------- сообщения

[Serializable, NetSerializable]
public sealed class DHSmsEntry(bool outgoing, string text, string time)
{
    public readonly bool Outgoing = outgoing;
    public readonly string Text = text;
    public readonly string Time = time;
}

/// <summary>Приложение «Сообщения»: переписки этого телефона (по номеру собеседника).</summary>
[RegisterComponent]
public sealed partial class DHMessengerCartridgeComponent : Component
{
    [ViewVariables]
    public Dictionary<string, List<DHSmsEntry>> Chats = new();

    [ViewVariables]
    public Dictionary<string, int> Unread = new();

    /// <summary>Открытая переписка.</summary>
    [ViewVariables]
    public string? Open;

    [DataField]
    public int MaxLength = 300;
}

[Serializable, NetSerializable]
public sealed class DHMessengerUiState(string myNumber, List<DHPhoneContact> contacts, string? open, string? openName,
    List<DHSmsEntry> messages) : BoundUserInterfaceState
{
    public readonly string MyNumber = myNumber;
    public readonly List<DHPhoneContact> Contacts = contacts;
    public readonly string? Open = open;
    public readonly string? OpenName = openName;
    public readonly List<DHSmsEntry> Messages = messages;
}

[Serializable, NetSerializable]
public enum DHMessengerAction : byte
{
    Open,
    Close,
    Send,
}

[Serializable, NetSerializable]
public sealed class DHMessengerUiMessageEvent(DHMessengerAction action, string number, string text)
    : CartridgeMessageEvent
{
    public readonly DHMessengerAction Action = action;
    public readonly string Number = number;
    public readonly string Text = text;
}

// ---------------------------------------------------------------- банк LA-V

/// <summary>Приложение «Банк LA-V»: вход по логину и паролю счёта, баланс, история, переводы.</summary>
[RegisterComponent]
public sealed partial class DHBankAppCartridgeComponent : Component
{
    [ViewVariables]
    public string? LoggedIn;

    [ViewVariables]
    public string? Status;
}

[Serializable, NetSerializable]
public sealed class DHBankAppUiState(bool loggedIn, string number, string holder, int balance, List<string> log,
    string? status) : BoundUserInterfaceState
{
    public readonly bool LoggedIn = loggedIn;
    public readonly string Number = number;
    public readonly string Holder = holder;
    public readonly int Balance = balance;
    public readonly List<string> Log = log;
    public readonly string? Status = status;
}

[Serializable, NetSerializable]
public enum DHBankAppAction : byte
{
    Login,
    Logout,
    Transfer,
}

[Serializable, NetSerializable]
public sealed class DHBankAppUiMessageEvent(DHBankAppAction action, string first, string second, int amount)
    : CartridgeMessageEvent
{
    public readonly DHBankAppAction Action = action;

    /// <summary>Логин или номер счёта получателя.</summary>
    public readonly string First = first;

    /// <summary>Пароль.</summary>
    public readonly string Second = second;

    public readonly int Amount = amount;
}

// ---------------------------------------------------------------- звонки

/// <summary>Приложение «Телефон»: звонки на другие телефоны.</summary>
[RegisterComponent]
public sealed partial class DHCallCartridgeComponent : Component;

[Serializable, NetSerializable]
public enum DHCallStatus : byte
{
    Idle,
    Calling,
    Ringing,
    InCall,
}

[Serializable, NetSerializable]
public sealed class DHCallUiState(DHCallStatus status, string? other, List<DHPhoneContact> contacts)
    : BoundUserInterfaceState
{
    public readonly DHCallStatus Status = status;
    public readonly string? Other = other;
    public readonly List<DHPhoneContact> Contacts = contacts;
}

[Serializable, NetSerializable]
public enum DHCallAction : byte
{
    Call,
    Answer,
    HangUp,
}

[Serializable, NetSerializable]
public sealed class DHCallUiMessageEvent(DHCallAction action, string number) : CartridgeMessageEvent
{
    public readonly DHCallAction Action = action;
    public readonly string Number = number;
}
