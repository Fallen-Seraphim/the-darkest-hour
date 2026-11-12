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
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
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
    public SoundSpecifier FlareSound = new SoundPathSpecifier("/Audio/_DarkestHour/Aviation/countermeasures.ogg");

    [DataField]
    public SoundSpecifier LaunchSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_begin.ogg");

    [DataField]
    public SoundSpecifier HookSound = new SoundPathSpecifier("/Audio/Machines/door_lock_on.ogg");

    [DataField]
    public SoundSpecifier WarningSound = new SoundPathSpecifier("/Audio/_DarkestHour/Aviation/manpad-lock.wav");

    [DataField]
    public SoundSpecifier HitSound = new SoundPathSpecifier("/Audio/_DarkestHour/Aviation/airframe-hit.ogg");

    [DataField]
    public SoundSpecifier EjectSound = new SoundPathSpecifier("/Audio/_DarkestHour/Aviation/ejection.ogg");

    // ---------------------------------------------------------------- бреющий заход (этап 2)

    /// <summary>Сколько топлива съедает заход: снижение с атмосферы, проход над полем и набор высоты.</summary>
    [DataField]
    public float PassFuel = 40f;

    /// <summary>Скорость на бреющем, клеток в секунду (курс задан линией захода).</summary>
    [DataField]
    public float PassSpeed = 12f;

    /// <summary>Самый короткий заход, клеток.</summary>
    [DataField]
    public float MinPassLength = 30f;

    /// <summary>Линия захода в координатах поверхности: начало и конец (выбираются на тактической карте).</summary>
    [ViewVariables, AutoNetworkedField]
    public Vector2? PassStart;

    [ViewVariables, AutoNetworkedField]
    public Vector2? PassEnd;

    /// <summary>Слой атмосферы, из которого начат заход (туда самолёт возвращается в конце).</summary>
    [ViewVariables]
    public EntityUid? PassSky;

    /// <summary>Высота бреющего: от неё зависят точность пушки и бомб и опасность ПВО.</summary>
    [DataField, AutoNetworkedField]
    public DHAircraftAltitude Altitude = DHAircraftAltitude.Medium;

    [DataField]
    public SoundSpecifier PassSound = new SoundPathSpecifier("/Audio/_DarkestHour/Aviation/jet-pass.ogg");

    [DataField]
    public SoundSpecifier PassHighSound = new SoundPathSpecifier("/Audio/_DarkestHour/Aviation/jet-pass-high.ogg");

    // ---------------------------------------------------------------- пушка 30 мм (подвесные контейнеры)

    /// <summary>Снаряды в каждом узле подвески (имеет смысл только для пушечных контейнеров).</summary>
    [DataField, AutoNetworkedField]
    public List<int> HardpointAmmo = new() { 0, 0, 0, 0 };

    /// <summary>Ёмкость одного пушечного контейнера.</summary>
    [DataField]
    public int PodCapacity = 300;

    /// <summary>Сколько снарядов во всех пушечных контейнерах.</summary>
    public int CannonAmmo
    {
        get
        {
            var total = 0;
            for (var i = 0; i < Hardpoints.Count && i < HardpointAmmo.Count; i++)
            {
                if (Hardpoints[i] == DHAircraftMunition.CannonPod)
                    total += HardpointAmmo[i];
            }

            return total;
        }
    }

    public int CannonPods => Hardpoints.FindAll(h => h == DHAircraftMunition.CannonPod).Count;

    /// <summary>Снарядов в очереди одного контейнера (одна очередь — одно попадание по курсу).</summary>
    [DataField]
    public int RoundsPerBurst = 6;

    [DataField]
    public TimeSpan CannonInterval = TimeSpan.FromSeconds(0.2);

    /// <summary>Куда ложатся очереди: столько клеток впереди самолёта по курсу.</summary>
    [DataField]
    public float CannonLead = 7f;

    /// <summary>Пушка включена: стреляет по курсу, пока самолёт на бреющем.</summary>
    [ViewVariables, AutoNetworkedField]
    public bool CannonOn;

    [ViewVariables]
    public TimeSpan NextBurst;

    [DataField]
    public SoundSpecifier CannonSound = new SoundPathSpecifier("/Audio/_DarkestHour/Aviation/gau-ground.ogg");

    [DataField]
    public SoundSpecifier CannonImpactSound = new SoundPathSpecifier("/Audio/_DarkestHour/Aviation/gau-impact.ogg");

    [ViewVariables]
    public TimeSpan NextCannonSound;

    // ---------------------------------------------------------------- подвески

    /// <summary>Четыре узла подвески; на каждом — один боеприпас.</summary>
    [DataField, AutoNetworkedField]
    public List<DHAircraftMunition> Hardpoints = new() { DHAircraftMunition.None, DHAircraftMunition.None, DHAircraftMunition.None, DHAircraftMunition.None };

    [DataField]
    public TimeSpan LoadTime = TimeSpan.FromSeconds(3);

    /// <summary>Выбранный узел подвески (на дисплее управления вооружением).</summary>
    [ViewVariables, AutoNetworkedField]
    public int SelectedHardpoint;

    /// <summary>Что спавнить, когда боеприпас снимают с узла на палубе.</summary>
    [DataField]
    public Dictionary<DHAircraftMunition, EntProtoId> MunitionPrototypes = new()
    {
        { DHAircraftMunition.BombHE, "DHAircraftBombHE" },
        { DHAircraftMunition.BombCluster, "DHAircraftBombCluster" },
        { DHAircraftMunition.BombNapalm, "DHAircraftBombNapalm" },
        { DHAircraftMunition.BombMagnesium, "DHAircraftBombMagnesium" },
        { DHAircraftMunition.GuidedBomb, "DHAircraftGuidedBomb" },
        { DHAircraftMunition.MissileAT, "DHAircraftMissileAT" },
        { DHAircraftMunition.MissileHE, "DHAircraftMissileHE" },
        { DHAircraftMunition.CannonPod, "DHAircraftCannonPod" },
    };

    // ---------------------------------------------------------------- спецотсеки (РЛС, ЛТЦ)

    /// <summary>Два отсека спецсистем в фюзеляже: РЛС и блок ЛТЦ ставятся и снимаются на палубе.</summary>
    [DataField, AutoNetworkedField]
    public List<DHAircraftModule> Modules = new() { DHAircraftModule.Radar, DHAircraftModule.FlareDispenser };

    [DataField]
    public Dictionary<DHAircraftModule, EntProtoId> ModulePrototypes = new()
    {
        { DHAircraftModule.Radar, "DHAircraftModuleRadar" },
        { DHAircraftModule.FlareDispenser, "DHAircraftModuleFlares" },
    };

    // ---------------------------------------------------------------- удары из атмосферы (этап 3)

    [ViewVariables, AutoNetworkedField]
    public DHAircraftMapMode MapMode = DHAircraftMapMode.Pass;

    /// <summary>Точка цели для пуска из атмосферы (координаты поверхности).</summary>
    [ViewVariables, AutoNetworkedField]
    public Vector2? TargetPoint;

    /// <summary>Подпись выбранной цели (сигнатура, метка или координаты).</summary>
    [ViewVariables, AutoNetworkedField]
    public string? TargetLabel;

    /// <summary>Захваченная техника (ракета наводится на неё до попадания).</summary>
    [ViewVariables]
    public EntityUid? TargetEntity;

    /// <summary>Цель — метка ЛЦУ (ракеты идут по ней).</summary>
    [ViewVariables]
    public bool TargetIsMark;

    /// <summary>Цель — скопление пехоты без метки: по ней ракету не навести.</summary>
    [ViewVariables]
    public bool TargetIsInfantry;

    /// <summary>Дальность пуска из атмосферы (по горизонтали), клеток.</summary>
    [DataField]
    public float LaunchRange = 160f;

    /// <summary>РЛС: как часто обновляются сигнатуры и насколько неточно.</summary>
    [DataField]
    public TimeSpan ScanInterval = TimeSpan.FromSeconds(12);

    [DataField]
    public float ScanJitter = 3f;

    [ViewVariables]
    public TimeSpan NextScan;

    /// <summary>Последний снимок РЛС: сигнатуры и их источники (на сервере).</summary>
    [ViewVariables]
    public List<DHAircraftSignature> Signatures = new();

    [ViewVariables]
    public Dictionary<int, EntityUid> SignatureSources = new();

    [DataField]
    public SoundSpecifier MissileSound = new SoundPathSpecifier("/Audio/_DarkestHour/Aviation/missile-release.ogg");

    // ---------------------------------------------------------------- кресло и связи

    [DataField]
    public EntProtoId CanopyPrototype = "DHAttackAircraftCanopy";

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Canopy;

    [DataField]
    public EntProtoId SeatPrototype = "DHAttackAircraftSeat";

    /// <summary>Где кресло пилота относительно центра корпуса (нос — вверх).</summary>
    [DataField]
    public Vector2 SeatOffset = new(0f, 0.9f);

    [ViewVariables, AutoNetworkedField]
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

    /// <summary>Бреющий заход над полем боя по линии захода.</summary>
    Pass,
}

/// <summary>Высота бреющего (вместо Z-уровней: +2, +3, +4 над землёй).</summary>
[Serializable, NetSerializable]
public enum DHAircraftAltitude : byte
{
    Low,
    Medium,
    High,
}

/// <summary>Боеприпас на узле подвески.</summary>
[Serializable, NetSerializable]
public enum DHAircraftMunition : byte
{
    None,

    /// <summary>Фугасная бомба: сброс на бреющем.</summary>
    BombHE,

    /// <summary>Кассетная бомба: россыпь малых разрывов вдоль курса — по пехоте на площади.</summary>
    BombCluster,

    /// <summary>Напалм: сплошная полоса огня вдоль курса — против пехоты и для запрета зоны.</summary>
    BombNapalm,

    /// <summary>Магниевая кассетная: химический огонь очагами, прожигает броню.</summary>
    BombMagnesium,

    /// <summary>Управляемая бомба: пуск из атмосферы по точке на карте; её может перехватить ПВО.</summary>
    GuidedBomb,

    /// <summary>Ракета воздух—земля бронебойная: из атмосферы по сигнатуре техники или метке.</summary>
    MissileAT,

    /// <summary>Ракета воздух—земля осколочно-фугасная: из атмосферы по сигнатуре или метке.</summary>
    MissileHE,

    /// <summary>Пушечный контейнер 30 мм: свои снаряды, бьёт по курсу на бреющем; контейнеров может быть несколько.</summary>
    CannonPod,
}

/// <summary>Спецсистема в отсеке фюзеляжа.</summary>
[Serializable, NetSerializable]
public enum DHAircraftModule : byte
{
    None,

    /// <summary>РЛС: сигнатуры техники и пехоты на поле боя.</summary>
    Radar,

    /// <summary>Блок отстрела ЛТЦ: без него завесу не поставить.</summary>
    FlareDispenser,
}

/// <summary>Модуль спецсистемы (предмет; ставится в отсек самолёта на палубе).</summary>
[RegisterComponent]
public sealed partial class DHAircraftModuleComponent : Component
{
    [DataField(required: true)]
    public DHAircraftModule Kind;

    /// <summary>Блок ЛТЦ: сколько патронов в нём (переносятся при снятии и установке).</summary>
    [DataField]
    public int Flares;
}

/// <summary>Что показывает правый клик по тактической карте: линия захода или цель для пуска из атмосферы.</summary>
[Serializable, NetSerializable]
public enum DHAircraftMapMode : byte
{
    Pass,
    Target,
}

[Serializable, NetSerializable]
public enum DHAircraftSignatureKind : byte
{
    Armor,
    Truck,
    Infantry,
}

/// <summary>Сигнатура с РЛС штурмовика: обнаружена с запаздыванием и неточно.</summary>
[Serializable, NetSerializable]
public sealed class DHAircraftSignature
{
    public int Id;
    public DHAircraftSignatureKind Kind;
    public Vector2 Position;
}

/// <summary>Боеприпас для подвески (предмет; заряжается в самолёт на палубе).</summary>
[RegisterComponent]
public sealed partial class DHAircraftMunitionComponent : Component
{
    [DataField(required: true)]
    public DHAircraftMunition Kind;

    /// <summary>Пушечный контейнер: снаряды в нём.</summary>
    [DataField]
    public int Rounds;
}

/// <summary>Короб снарядов 30 мм для пушечных контейнеров штурмовика.</summary>
[RegisterComponent]
public sealed partial class DHAircraftCannonAmmoComponent : Component
{
    [DataField]
    public int Rounds = 150;
}

[Serializable, NetSerializable]
public sealed partial class DHAircraftLoadDoAfterEvent : SimpleDoAfterEvent;

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
        "DHActionAircraftCannon",
        "DHActionAircraftDrop",
        "DHActionAircraftFire",
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

    /// <summary>Стартовый самолёт уже выдан (при инициализации карты или при первом подъёме).</summary>
    [ViewVariables]
    public bool StartingSpawned;

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

public sealed partial class DHAircraftCannonActionEvent : InstantActionEvent;

public sealed partial class DHAircraftDropActionEvent : InstantActionEvent;

public sealed partial class DHAircraftFireActionEvent : InstantActionEvent;

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

    public Vector2? PassStart;
    public Vector2? PassEnd;
    public bool CanPass;
    public string? PassBlocker;
    public DHAircraftAltitude Altitude;
    public int CannonAmmo;
    public int MaxCannonAmmo;
    public bool CannonOn;
    public List<DHAircraftMunition> Hardpoints = new();
    public List<int> HardpointAmmo = new();
    public List<DHAircraftModule> Modules = new();
    public int SelectedHardpoint;
    public DHAircraftMapMode MapMode;
    public Vector2? TargetPoint;
    public string? TargetLabel;
    public List<DHAircraftSignature> Signatures = new();
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

/// <summary>Точка линии захода, выбранная правым кликом на тактической карте.</summary>
[Serializable, NetSerializable]
public sealed class DHAircraftPassPointMessage(Vector2 point) : BoundUserInterfaceMessage
{
    public readonly Vector2 Point = point;
}

[Serializable, NetSerializable]
public sealed class DHAircraftClearPassMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DHAircraftStartPassMessage : BoundUserInterfaceMessage;

/// <summary>Сменить высоту бреющего: -1 ниже, +1 выше.</summary>
[Serializable, NetSerializable]
public sealed class DHAircraftAltitudeMessage(int delta) : BoundUserInterfaceMessage
{
    public readonly int Delta = delta;
}

[Serializable, NetSerializable]
public sealed class DHAircraftCannonMessage(bool on) : BoundUserInterfaceMessage
{
    public readonly bool On = on;
}

[Serializable, NetSerializable]
public sealed class DHAircraftDropMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DHAircraftSelectHardpointMessage(int slot) : BoundUserInterfaceMessage
{
    public readonly int Slot = slot;
}

[Serializable, NetSerializable]
public sealed class DHAircraftMapModeMessage(DHAircraftMapMode mode) : BoundUserInterfaceMessage
{
    public readonly DHAircraftMapMode Mode = mode;
}

/// <summary>Цель для пуска из атмосферы: правый клик по карте в режиме «Цель».</summary>
[Serializable, NetSerializable]
public sealed class DHAircraftTargetMessage(Vector2 point) : BoundUserInterfaceMessage
{
    public readonly Vector2 Point = point;
}

/// <summary>Пуск управляемого боеприпаса с выбранного узла по выбранной цели.</summary>
[Serializable, NetSerializable]
public sealed class DHAircraftFireMessage : BoundUserInterfaceMessage;
