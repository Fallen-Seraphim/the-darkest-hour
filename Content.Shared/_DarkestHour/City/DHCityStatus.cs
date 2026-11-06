using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.City;

// «Восстание», обстановка в городе и связь района (решение автора 2026-11-06). Со старта — «Обычная ситуация»;
// Военная Полиция со своего компьютера связи вводит комендантский час или ЧС и возвращает обычную ситуацию.
// Компьютер связи района (управа) и ВП — объявления на весь город. Обстановка видна на телефонах и терминалах
// рядом со временем.

[Serializable, NetSerializable]
public enum DHCityStatus : byte
{
    Normal,
    Curfew,
    Emergency,
}

public static class DHCityStatuses
{
    public static string Name(DHCityStatus status)
    {
        return status switch
        {
            DHCityStatus.Curfew => "dh-city-status-curfew",
            DHCityStatus.Emergency => "dh-city-status-emergency",
            _ => "dh-city-status-normal",
        };
    }

    public static Color TextColor(DHCityStatus status)
    {
        return status switch
        {
            DHCityStatus.Curfew => Robust.Shared.Maths.Color.FromHex("#E8B04A"),
            DHCityStatus.Emergency => Robust.Shared.Maths.Color.FromHex("#E04A4A"),
            _ => Robust.Shared.Maths.Color.FromHex("#8FD66B"),
        };
    }
}

/// <summary>Сервер → клиенты: текущая обстановка в городе (при смене и при входе в игру).</summary>
[Serializable, NetSerializable]
public sealed class DHCityStatusEvent(DHCityStatus status) : EntityEventArgs
{
    public readonly DHCityStatus Status = status;
}

/// <summary>Компьютер связи: объявления на весь город, у ВП — ещё и обстановка.</summary>
[RegisterComponent]
public sealed partial class DHCityCommsComponent : Component
{
    /// <summary>Подпись объявлений.</summary>
    [DataField(required: true)]
    public LocId Sender;

    [DataField]
    public Color Color = Robust.Shared.Maths.Color.FromHex("#8A8A8A");

    /// <summary>Может менять обстановку в городе (компьютер ВП).</summary>
    [DataField]
    public bool CanSetStatus;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    [ViewVariables]
    public TimeSpan NextAnnounce;
}

[Serializable, NetSerializable]
public enum DHCityCommsUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHCityCommsState(string sender, DHCityStatus status, bool canSetStatus, int cooldown)
    : BoundUserInterfaceState
{
    public readonly string Sender = sender;
    public readonly DHCityStatus Status = status;
    public readonly bool CanSetStatus = canSetStatus;

    /// <summary>Секунд до следующего объявления, 0 — можно.</summary>
    public readonly int Cooldown = cooldown;
}

[Serializable, NetSerializable]
public sealed class DHCityCommsAnnounceMessage(string text) : BoundUserInterfaceMessage
{
    public readonly string Text = text;
}

[Serializable, NetSerializable]
public sealed class DHCityCommsSetStatusMessage(DHCityStatus status) : BoundUserInterfaceMessage
{
    public readonly DHCityStatus Status = status;
}
