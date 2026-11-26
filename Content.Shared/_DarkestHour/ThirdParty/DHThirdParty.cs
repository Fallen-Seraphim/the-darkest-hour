using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.ThirdParty;

// События третьей стороны (план осады пп. 11 и 28): набор из призраков «по интересу», как в RuCM. Сервер рассылает
// открытые наборы всем; в окне «Роли призраков» у события одна кнопка «Интересоваться» на всё событие. Набралось
// достаточно — событие запускается, роли раздаются записавшимся случайно.

/// <summary>Одна роль события (для описания: кто войдёт в событие).</summary>
[Serializable, NetSerializable]
public sealed class DHThirdPartyRoleInfo
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
}

[Serializable, NetSerializable]
public sealed class DHThirdPartyEventInfo
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public List<DHThirdPartyRoleInfo> Roles = new();

    /// <summary>Сколько записалось.</summary>
    public int Interested;

    /// <summary>Сколько нужно, чтобы событие началось.</summary>
    public int Required;

    /// <summary>Сколько всего мест.</summary>
    public int Slots;

    /// <summary>Этот игрок записан.</summary>
    public bool Mine;

    /// <summary>Сколько секунд раунда осталось до закрытия набора.</summary>
    public int SecondsLeft;
}

/// <summary>Сервер → клиент: открытые наборы (пустой список — наборов нет).</summary>
[Serializable, NetSerializable]
public sealed class DHThirdPartyStateEvent : EntityEventArgs
{
    public List<DHThirdPartyEventInfo> Events = new();

    /// <summary>Только что открылся новый набор — показать окно наблюдателям.</summary>
    public bool Opened;
}

/// <summary>Клиент → сервер: интересоваться событием или отказаться.</summary>
[Serializable, NetSerializable]
public sealed class DHThirdPartyInterestEvent : EntityEventArgs
{
    public string EventId = string.Empty;
    public bool Interested;
}

/// <summary>Способность «Задание»: напомнить цель, сроки и правила кодекса.</summary>
public sealed partial class DHZGAStatusActionEvent : InstantActionEvent;

/// <summary>Летальная инъекция десантника (имплант на основе базового): не попасть в плен живым.</summary>
public sealed partial class DHZGALethalActionEvent : InstantActionEvent;
