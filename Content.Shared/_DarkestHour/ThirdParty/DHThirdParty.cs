using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.ThirdParty;

// События третьей стороны (план осады пп. 11 и 28): набор из призраков «по интересу», как в CMU. Сервер рассылает
// открытые наборы всем; наблюдатель записывается на роль, и когда на каждую роль есть хотя бы один — событие
// запускается, а записавшиеся становятся его участниками.

/// <summary>Одна роль набора: кто нужен и сколько уже записалось.</summary>
[Serializable, NetSerializable]
public sealed class DHThirdPartyRoleInfo
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public int Interested;

    /// <summary>Этот игрок записан на роль.</summary>
    public bool Mine;
}

[Serializable, NetSerializable]
public sealed class DHThirdPartyEventInfo
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public List<DHThirdPartyRoleInfo> Roles = new();

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

/// <summary>Клиент → сервер: записаться на роль или отказаться.</summary>
[Serializable, NetSerializable]
public sealed class DHThirdPartyInterestEvent : EntityEventArgs
{
    public string EventId = string.Empty;
    public string RoleId = string.Empty;
    public bool Interested;
}

/// <summary>Способность «Задание»: напомнить цель, сроки и правила кодекса.</summary>
public sealed partial class DHZGAStatusActionEvent : InstantActionEvent;
