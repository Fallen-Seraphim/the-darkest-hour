using System.Numerics;
using Content.Shared._DarkestHour.Overwatch;
using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Aviation;

/// <summary>
/// Атмосферный штурмовик Империи (решение автора 2026-11-12). Корпус — сущность на палубе авианосца (как у CMU:
/// корпус + кресло пилота отдельной сущностью). Жизненный цикл: в ангаре под палубой (лифт опущен) → на лифте в начале
/// ВПП (обслуживание, заправка, ЛТЦ) → пилот пристёгивается, цепляется к катапульте и взлетает → в слое атмосферы над
/// полем боя (WASD — направление, скорость из интерфейса полёта, топливо горит, ПВО бьёт без ЛТЦ) → автопилот посадки
/// возвращает на палубу и закатывает на лифт. Бреющий заход и вооружение — следующие этапы.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHAircraftComponent : Component
{
    [DataField, AutoNetworkedField]
    public DHAircraftState State = DHAircraftState.Stowed;

    // ---------------------------------------------------------------- топливо

    [DataField]
    public string FuelSolution = "fuel";

    [DataField]
    public ProtoId<ReagentPrototype> FuelReagent = "WeldingFuel";

    /// <summary>Сколько топлива нужно, чтобы катапульта выпустила самолёт.</summary>
    [DataField]
    public float TakeoffFuel = 60f;

    /// <summary>Расход в атмосфере на минимальной скорости, ед./с; на максимальной — вдвое больше.</summary>
    [DataField]
    public float FuelPerSecond = 0.5f;

    // ---------------------------------------------------------------- полёт

    /// <summary>Заданная скорость 0..1 (между <see cref="MinSpeed"/> и <see cref="MaxSpeed"/>).</summary>
    [DataField, AutoNetworkedField]
    public float Speed = 0.5f;

    /// <summary>Клеток в секунду.</summary>
    [DataField]
    public float MinSpeed = 6f;

    [DataField]
    public float MaxSpeed = 16f;

    /// <summary>Поворот, градусов в секунду.</summary>
    [DataField]
    public float TurnRate = 70f;

    /// <summary>Разгон катапультой по ВПП.</summary>
    [DataField]
    public float LaunchTime = 3.5f;

    /// <summary>Пробег после посадки до лифта.</summary>
    [DataField]
    public float LandingRollTime = 5f;

    /// <summary>Курс в мире (угол носа), задаётся WASD.</summary>
    [ViewVariables, AutoNetworkedField]
    public Angle Heading;

    // ---------------------------------------------------------------- прочность и ЛТЦ

    [DataField, AutoNetworkedField]
    public float Integrity = 100f;

    [DataField]
    public float MaxIntegrity = 100f;

    /// <summary>Ремонт сваркой на палубе за одно применение.</summary>
    [DataField]
    public float RepairAmount = 20f;

    [DataField, AutoNetworkedField]
    public int Flares = 8;

    [DataField]
    public int MaxFlares = 8;

    /// <summary>Сколько после отстрела ЛТЦ сбивают наведение ПВО.</summary>
    [DataField]
    public TimeSpan FlareDuration = TimeSpan.FromSeconds(6);

    [ViewVariables]
    public TimeSpan FlaresUntil;

    [DataField]
    public SoundSpecifier FlareSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/flaregun.ogg");

    [DataField]
    public SoundSpecifier LaunchSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_begin.ogg");

    [DataField]
    public SoundSpecifier HookSound = new SoundPathSpecifier("/Audio/Machines/door_lock_on.ogg");

    [DataField]
    public SoundSpecifier WarningSound = new SoundPathSpecifier("/Audio/Effects/alert.ogg");

    // ---------------------------------------------------------------- кресло и связи

    [DataField]
    public EntProtoId SeatPrototype = "DHAttackAircraftSeat";

    /// <summary>Где кресло пилота относительно центра корпуса (нос — вверх).</summary>
    [DataField]
    public Vector2 SeatOffset = new(0f, 0.9f);

    [ViewVariables]
    public EntityUid? Seat;

    /// <summary>Лифт, на котором самолёт базируется (туда же возвращает автопилот).</summary>
    [ViewVariables]
    public EntityUid? Lift;

    /// <summary>Сцеплен с катапультой.</summary>
    [ViewVariables, AutoNetworkedField]
    public bool Hooked;

    /// <summary>WASD управляет самолётом (иначе — пилот просто сидит).</summary>
    [ViewVariables, AutoNetworkedField]
    public bool Control;

    /// <summary>Прогресс разгона или пробега, 0..1.</summary>
    [ViewVariables]
    public float Progress;

    [ViewVariables]
    public Vector2 PathStart;

    [ViewVariables]
    public Vector2 PathEnd;

    [ViewVariables]
    public TimeSpan NextWarning;
}

[Serializable, NetSerializable]
public enum DHAircraftState : byte
{
    /// <summary>В ангаре под палубой (лифт опущен).</summary>
    Stowed,

    /// <summary>На палубе, на лифте в начале ВПП.</summary>
    Parked,

    /// <summary>Катапульта разгоняет по ВПП.</summary>
    Launching,

    /// <summary>В слое атмосферы над полем боя.</summary>
    Airborne,

    /// <summary>Автопилот ведёт к авианосцу.</summary>
    Returning,

    /// <summary>Пробег по ВПП к лифту после посадки.</summary>
    Landing,
}

/// <summary>Кресло пилота; при пристёгивании пилот получает способности самолёта.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHAircraftSeatComponent : Component
{
    [ViewVariables]
    public EntityUid? Aircraft;

    [DataField]
    public List<EntProtoId> Actions = new()
    {
        "DHActionAircraftFlight",
        "DHActionAircraftControl",
        "DHActionAircraftFlares",
        "DHActionAircraftEject",
    };

    [ViewVariables]
    public List<EntityUid> ActionEntities = new();
}

/// <summary>Пилот, пристёгнутый в кресле самолёта.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHAircraftPilotComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid Aircraft;
}

/// <summary>
/// Лифт штурмовика в начале ВПП: поднимает самолёт из ангара на палубу и опускает обратно; он же — начало катапульты.
/// Катапульта разгоняет самолёт в сторону «вверх» лифта на <see cref="RunwayLength"/> клеток.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHAircraftLiftComponent : Component
{
    [DataField, AutoNetworkedField]
    public DHAircraftLiftMode Mode = DHAircraftLiftMode.Lowered;

    /// <summary>Самолёт, который лифт получает в начале раунда (в ангаре).</summary>
    [DataField]
    public EntProtoId? StartingAircraft = "DHAttackAircraft";

    [ViewVariables]
    public EntityUid? Aircraft;

    [DataField]
    public float RunwayLength = 50f;

    [DataField]
    public TimeSpan MoveTime = TimeSpan.FromSeconds(8);

    [ViewVariables]
    public TimeSpan MoveEnd;

    [DataField]
    public SoundSpecifier MoveSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/asrs_raising.ogg");
}

[Serializable, NetSerializable]
public enum DHAircraftLiftMode : byte
{
    Lowered,
    Raising,
    Raised,
    Lowering,
}

[Serializable, NetSerializable]
public enum DHAircraftLiftVisuals : byte
{
    Mode,
}

[Serializable, NetSerializable]
public enum DHAircraftLiftLayers : byte
{
    Base,
}

/// <summary>Кассета ЛТЦ: техник заряжает самолёт на палубе.</summary>
[RegisterComponent]
public sealed partial class DHAircraftFlareCassetteComponent : Component
{
    [DataField]
    public int Flares = 4;
}

[Serializable, NetSerializable]
public sealed partial class DHAircraftRepairDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class DHAircraftFlightActionEvent : InstantActionEvent;

public sealed partial class DHAircraftControlActionEvent : InstantActionEvent;

public sealed partial class DHAircraftFlaresActionEvent : InstantActionEvent;

public sealed partial class DHAircraftEjectActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum DHAircraftUiKey : byte
{
    Flight,
}

[Serializable, NetSerializable]
public sealed class DHAircraftBuiState : BoundUserInterfaceState
{
    public DHAircraftState State;
    public float Fuel;
    public float FuelMax;
    public float TakeoffFuel;
    public float Speed;
    public float Integrity;
    public float MaxIntegrity;
    public int Flares;
    public int MaxFlares;
    public bool FlaresActive;
    public bool Hooked;
    public bool Control;
    public bool CanLaunch;

    /// <summary>Почему нельзя взлететь (ключ локализации), если нельзя.</summary>
    public string? LaunchBlocker;

    /// <summary>Положение над полем боя (координаты поверхности); null — не в воздухе.</summary>
    public Vector2? Position;

    public float HeadingDegrees;

    /// <summary>Тактическая карта поверхности (как у пульта наблюдения).</summary>
    public DHOverwatchState? Map;
}

[Serializable, NetSerializable]
public sealed class DHAircraftHookMessage(bool hook) : BoundUserInterfaceMessage
{
    public readonly bool Hook = hook;
}

[Serializable, NetSerializable]
public sealed class DHAircraftLaunchMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DHAircraftReturnMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DHAircraftSpeedMessage(float speed) : BoundUserInterfaceMessage
{
    public readonly float Speed = speed;
}

[Serializable, NetSerializable]
public sealed class DHAircraftControlMessage(bool control) : BoundUserInterfaceMessage
{
    public readonly bool Control = control;
}
