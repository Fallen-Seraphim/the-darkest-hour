using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Republic;

/// <summary>
/// Ключ панели тактической боеголовки: открывает и закрывает панель ввода.
/// </summary>
[RegisterComponent]
public sealed partial class DHWarheadPanelKeyComponent : Component;

/// <summary>
/// Ключ авторизации тактической боеголовки — пропуск на шнурке. Приложить к боеголовке с открытой панелью
/// (в руке или прямо с шеи) — откроется интерфейс подрыва.
/// </summary>
[RegisterComponent]
public sealed partial class DHWarheadAuthKeyComponent : Component;

/// <summary>
/// Лист с кодом ядерной аутентификации текущего раунда: при появлении код вписывается в текст.
/// </summary>
[RegisterComponent]
public sealed partial class DHWarheadCodePaperComponent : Component;

[Serializable, NetSerializable]
public enum DHWarheadUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum DHWarheadVisuals : byte
{
    Panel,
    State,
}

[Serializable, NetSerializable]
public enum DHWarheadVisualState : byte
{
    Idle,
    Authorized,
    Armed,
    Final,
}

[Serializable, NetSerializable]
public enum DHWarheadStatus : byte
{
    /// <summary>Ждёт код ядерной аутентификации.</summary>
    AwaitCode,

    /// <summary>Код принят, можно взводить.</summary>
    Ready,

    /// <summary>Взведена, идёт отсчёт.</summary>
    Armed,
}

[Serializable, NetSerializable]
public sealed class DHWarheadState(
    DHWarheadStatus status,
    int enteredDigits,
    int codeLength,
    int timer,
    int minTimer,
    int maxTimer,
    float remaining,
    bool deployed) : BoundUserInterfaceState
{
    public readonly DHWarheadStatus Status = status;
    public readonly int EnteredDigits = enteredDigits;
    public readonly int CodeLength = codeLength;
    public readonly int Timer = timer;
    public readonly int MinTimer = minTimer;
    public readonly int MaxTimer = maxTimer;
    public readonly float Remaining = remaining;
    public readonly bool Deployed = deployed;
}

[Serializable, NetSerializable]
public sealed class DHWarheadKeypadMessage(int digit) : BoundUserInterfaceMessage
{
    public readonly int Digit = digit;
}

[Serializable, NetSerializable]
public sealed class DHWarheadClearMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DHWarheadEnterMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DHWarheadAdjustTimerMessage(int delta) : BoundUserInterfaceMessage
{
    public readonly int Delta = delta;
}

[Serializable, NetSerializable]
public sealed class DHWarheadArmMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DHWarheadDisarmMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed partial class DHWarheadPanelDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHWarheadDisarmDoAfterEvent : SimpleDoAfterEvent;
