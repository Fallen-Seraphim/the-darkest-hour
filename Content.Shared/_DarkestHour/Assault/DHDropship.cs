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
    List<DHDropshipDestination> destinations) : BoundUserInterfaceState
{
    public readonly string ShipName = shipName;
    public readonly DHDropshipState State = state;
    public readonly string Location = location;
    public readonly string? Destination = destination;
    public readonly int SecondsLeft = secondsLeft;
    public readonly List<DHDropshipDestination> Destinations = destinations;
}

/// <summary>Лететь: в ангар (Zone = null) или на ЗП.</summary>
[Serializable, NetSerializable]
public sealed class DHDropshipFlyMessage(NetEntity? zone) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Zone = zone;
}

/// <summary>Отменить взлёт, пока идёт отсчёт.</summary>
[Serializable, NetSerializable]
public sealed class DHDropshipAbortMessage : BoundUserInterfaceMessage;
