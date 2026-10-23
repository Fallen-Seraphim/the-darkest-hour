using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Investigation;

// Следствие (этап 4): вскрытие (сканер вскрытия → протокол), память камер и история мониторинга экипажа
// (архив наблюдения на настольном компьютере).

/// <summary>Сканер вскрытия: применить к мёртвому телу — протокол вскрытия.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHAutopsyScannerComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(8);

    [DataField]
    public EntProtoId PaperPrototype = "Paper";
}

[Serializable, NetSerializable]
public sealed partial class DHAutopsyDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Компьютер архива наблюдения: журнал камер и датчиков экипажа станции.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHArchiveConsoleComponent : Component;

[Serializable, NetSerializable]
public enum DHArchiveUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum DHArchiveKind : byte
{
    /// <summary>Камера видела человека или событие.</summary>
    Camera,

    /// <summary>Датчики костюма: критическое состояние, смерть.</summary>
    Sensors,
}

[Serializable, NetSerializable]
public sealed class DHArchiveEntry(TimeSpan time, DHArchiveKind kind, string source, string text)
{
    public readonly TimeSpan Time = time;
    public readonly DHArchiveKind Kind = kind;
    public readonly string Source = source;
    public readonly string Text = text;
}

[Serializable, NetSerializable]
public sealed class DHArchiveBoundUserInterfaceState(List<DHArchiveEntry> entries) : BoundUserInterfaceState
{
    public readonly List<DHArchiveEntry> Entries = entries;
}

[Serializable, NetSerializable]
public sealed class DHArchiveRefreshMessage : BoundUserInterfaceMessage;
