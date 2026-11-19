using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Assault;

// Челноки и зоны посадки (решение автора 2026-10-25, по образцу морпехов RMC): авианосец не садится, на
// поверхность ходят два суборбитальных челнока на автопилоте — из ангара авианосца на ЗП и обратно.
// ЗП — готовые площадки у базы и переносные маяки, которые разворачивает командир десанта.

/// <summary>Зона посадки: челноки видят её в списке направлений, пока она стоит на поверхности.</summary>
[RegisterComponent]
public sealed partial class DHLandingZoneComponent : Component
{
    /// <summary>Имя в списке направлений; пусто — присвоится «ЗП-n» при появлении.</summary>
    [DataField]
    public string ZoneName = string.Empty;

    /// <summary>Временная (переносной маяк).</summary>
    [DataField]
    public bool Temporary;
}

/// <summary>Свёрнутый маяк ЗП: разворачивается в руке на поверхности планеты.</summary>
[RegisterComponent]
public sealed partial class DHLandingZoneBeaconComponent : Component
{
    [DataField]
    public string Zone = "DHLandingZoneTemporary";

    [DataField]
    public TimeSpan DeployTime = TimeSpan.FromSeconds(5);
}

[Serializable, NetSerializable]
public sealed partial class DHLandingZoneDeployDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

/// <summary>Консоль пилота челнока.</summary>
[RegisterComponent]
public sealed partial class DHDropshipConsoleComponent : Component;

[Serializable, NetSerializable]
public enum DHDropshipUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum DHDropshipState : byte
{
    /// <summary>В ангаре авианосца (пристыкован).</summary>
    Docked,

    /// <summary>Отсчёт до взлёта.</summary>
    Departing,

    /// <summary>В полёте.</summary>
    InFlight,

    /// <summary>Стоит на ЗП.</summary>
    Landed,
}

/// <summary>Фаза полёта челнока на автопилоте (решение автора 2026-11-19).</summary>
[Serializable, NetSerializable]
public enum DHDropshipFlight : byte
{
    None,

    /// <summary>Переход орбита — атмосфера над полем боя (или обратно).</summary>
    Transit,

    /// <summary>Полёт на уровне воздуха над полем боя: к цели, за город или висим.</summary>
    Cruise,

    /// <summary>Снижение по уровням над ЗП.</summary>
    Descend,

    /// <summary>Подъём по уровням до высоты полёта.</summary>
    Climb,

    /// <summary>Медленный полёт в слое атмосферы под управлением пилота — заход на выброску десанта.</summary>
    Atmosphere,
}

/// <summary>Направление полёта: ангар (Zone = null) или ЗП.</summary>
[Serializable, NetSerializable]
public sealed class DHDropshipDestination(NetEntity? zone, string name, bool available, string note)
{
    public readonly NetEntity? Zone = zone;
    public readonly string Name = name;
    public readonly bool Available = available;
    public readonly string Note = note;
}

[Serializable, NetSerializable]
public sealed class DHDropshipConsoleState(
    string shipName,
    DHDropshipState state,
    string location,
    string? destination,
    int secondsLeft,
    List<DHDropshipDestination> destinations,
    int flares = 0,
    int maxFlares = 0,
    bool flaresActive = false) : BoundUserInterfaceState
{
    /// <summary>ЛТЦ: осталось залпов, всего, сейчас прикрывают челнок.</summary>
    public readonly int Flares = flares;
    public readonly int MaxFlares = maxFlares;
    public readonly bool FlaresActive = flaresActive;

    public readonly string ShipName = shipName;
    public readonly DHDropshipState State = state;
    public readonly string Location = location;
    public readonly string? Destination = destination;
    public readonly int SecondsLeft = secondsLeft;
    public readonly List<DHDropshipDestination> Destinations = destinations;

    /// <summary>Что делает челнок в полёте (переход, полёт, снижение…).</summary>
    public string? Phase;

    /// <summary>Ручное управление включено.</summary>
    public bool Manual;

    /// <summary>Можно взять управление: челнок летит на уровне воздуха.</summary>
    public bool ManualAvailable;

    /// <summary>В полёте можно сменить цель.</summary>
    public bool Retargetable;

    /// <summary>Челнок в слое атмосферы (заход на выброску).</summary>
    public bool InAtmosphere;

    /// <summary>Можно подняться в слой атмосферы: челнок летит над полем боя.</summary>
    public bool AtmosphereAvailable;
}

/// <summary>Лететь: в ангар (Zone = null) или на ЗП.</summary>
[Serializable, NetSerializable]
public sealed class DHDropshipFlyMessage(NetEntity? zone) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Zone = zone;
}

/// <summary>Отстрелить ЛТЦ (в полёте): ПВО не наводится на челнок до конца захода.</summary>
[Serializable, NetSerializable]
public sealed class DHDropshipFlaresMessage : BoundUserInterfaceMessage;

/// <summary>Взять челнок на себя (WASD) или вернуть автопилоту.</summary>
[Serializable, NetSerializable]
public sealed class DHDropshipManualMessage(bool enable) : BoundUserInterfaceMessage
{
    public readonly bool Enable = enable;
}

/// <summary>Подняться в слой атмосферы для выброски десанта или вернуться на высоту полёта.</summary>
[Serializable, NetSerializable]
public sealed class DHDropshipAtmosphereMessage(bool enable) : BoundUserInterfaceMessage
{
    public readonly bool Enable = enable;
}

/// <summary>Отменить взлёт, пока идёт отсчёт.</summary>
[Serializable, NetSerializable]
public sealed class DHDropshipAbortMessage : BoundUserInterfaceMessage;
