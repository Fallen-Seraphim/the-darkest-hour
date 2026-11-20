using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Supply;

// Сброс грузов на маяк и лифты авианосца (план осады п. 4, решения автора 2026-11-19), по образцу RMC SupplyDrop и
// Elevators:
// - площадка сброса 3×3 в карго и консоль «Сброс грузов»: всё, что стоит на площадке, улетает к включённому маяку на
//   поверхности и через ~20 с опускается рядом с ним на парашютах; поддоны распаковываются в технику. Работает
//   всегда, после сброса — 2 минуты перезарядки;
// - лифт техники 5×5 у ВПП с компьютером: в трюме — поддоны и опущенная техника, поднять можно любое по выбору;
// - лифт штурмовика: отдельный компьютер с одной кнопкой «Поднять».

/// <summary>Площадка сброса: с неё консоль отправляет груз к маяку.</summary>
[RegisterComponent]
public sealed partial class DHSupplyDropPadComponent : Component
{
    /// <summary>Сколько клеток от центра — площадка (1 — квадрат 3×3).</summary>
    [DataField]
    public int Radius = 1;
}

/// <summary>Консоль «Сброс грузов».</summary>
[RegisterComponent]
public sealed partial class DHSupplyDropConsoleComponent : Component
{
    /// <summary>Как далеко на том же гриде искать площадку.</summary>
    [DataField]
    public float PadRange = 15f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(2);

    /// <summary>Сколько груз летит до маяка.</summary>
    [DataField]
    public TimeSpan FlightTime = TimeSpan.FromSeconds(20);

    [ViewVariables]
    public TimeSpan NextDrop;
}

[Serializable, NetSerializable]
public enum DHSupplyDropUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHSupplyDropBeaconRow
{
    public NetEntity Beacon;
    public string Name = string.Empty;
    public int X;
    public int Y;

    /// <summary>К маяку уже летит груз.</summary>
    public bool Incoming;
}

[Serializable, NetSerializable]
public sealed class DHSupplyDropState : BoundUserInterfaceState
{
    public bool HasPad;

    /// <summary>Что стоит на площадке.</summary>
    public List<string> Cargo = new();

    public List<DHSupplyDropBeaconRow> Beacons = new();

    /// <summary>Сколько секунд до готовности (0 — готово).</summary>
    public int Cooldown;

    /// <summary>Почему сброс сейчас невозможен (повреждён ангар и т. п.); пусто — возможен.</summary>
    public string Blocked = string.Empty;
}

[Serializable, NetSerializable]
public sealed class DHSupplyDropLaunchMessage : BoundUserInterfaceMessage
{
    public NetEntity Beacon;

    public DHSupplyDropLaunchMessage(NetEntity beacon)
    {
        Beacon = beacon;
    }
}

/// <summary>Дым включённого маяка: ставится маяком раз в несколько секунд, сам гаснет.</summary>
[RegisterComponent]
public sealed partial class DHDropBeaconSmokeComponent : Component;

/// <summary>Лифт техники 5×5: поднимает из трюма поддон или машину, опускает в трюм то, что стоит на платформе.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHVehicleLiftComponent : Component
{
    /// <summary>Платформа — квадрат со стороной 2·Radius+1 клеток.</summary>
    [DataField]
    public int Radius = 2;

    [DataField, AutoNetworkedField]
    public DHSupplyElevatorMode Mode = DHSupplyElevatorMode.Lowered;

    /// <summary>Что лежит в трюме в начале раунда.</summary>
    [DataField]
    public List<EntProtoId> Starting = new();

    /// <summary>Что сейчас в трюме (сервер; сущности вне карты).</summary>
    [ViewVariables]
    public List<EntityUid> Stored = new();

    /// <summary>Что поднимается сейчас.</summary>
    [ViewVariables]
    public EntityUid? Cargo;

    [DataField]
    public TimeSpan RaiseTime = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan LowerTime = TimeSpan.FromSeconds(15);

    [ViewVariables]
    public TimeSpan? DoneAt;

    [DataField]
    public SoundSpecifier RaisingSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/asrs_raising.ogg");

    [DataField]
    public SoundSpecifier LoweringSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/asrs_lowering.ogg");

    [DataField]
    public string PitFixture = "pit";
}

/// <summary>Компьютер лифта техники.</summary>
[RegisterComponent]
public sealed partial class DHVehicleLiftConsoleComponent : Component
{
    [DataField]
    public float LiftRange = 20f;
}

[Serializable, NetSerializable]
public enum DHVehicleLiftUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHVehicleLiftRow
{
    public NetEntity Entity;
    public string Name = string.Empty;
}

[Serializable, NetSerializable]
public sealed class DHVehicleLiftState : BoundUserInterfaceState
{
    public bool HasLift;
    public DHSupplyElevatorMode Mode;

    /// <summary>Трюм.</summary>
    public List<DHVehicleLiftRow> Stored = new();

    /// <summary>Что стоит на поднятой платформе.</summary>
    public List<string> OnPlatform = new();

    /// <summary>Почему лифт сейчас не работает; пусто — работает.</summary>
    public string Blocked = string.Empty;
}

[Serializable, NetSerializable]
public sealed class DHVehicleLiftRaiseMessage : BoundUserInterfaceMessage
{
    public NetEntity Entity;

    public DHVehicleLiftRaiseMessage(NetEntity entity)
    {
        Entity = entity;
    }
}

[Serializable, NetSerializable]
public sealed class DHVehicleLiftLowerMessage : BoundUserInterfaceMessage;

/// <summary>Компьютер лифта штурмовика: одна кнопка — «Поднять».</summary>
[RegisterComponent]
public sealed partial class DHAircraftLiftConsoleComponent : Component
{
    [DataField]
    public float LiftRange = 15f;
}
