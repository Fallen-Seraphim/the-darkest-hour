using System.Numerics;
using Content.Shared._DarkestHour.Assault;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Vehicles;

// Бронетехника «Штурма» (решение автора 2026-10-30): по БТР и танку на сторону. Своя компактная система поверх
// ванильного VehicleSystem (вождение) и станкового оружия DH (стрельба из башни). Спрайты и облик — RMC-14.
// Внутри машины — отсек (своя маленькая карта): люк, кресла водителя и стрелков, места десанта.

/// <summary>Боевая машина.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHVehicleComponent : Component
{
    /// <summary>Отсек машины.</summary>
    [DataField(required: true)]
    public ProtoId<DHVehicleInteriorPrototype> Interior;

    /// <summary>Оружие: id места (его же указывает кресло стрелка) → прототип орудия.</summary>
    [DataField]
    public Dictionary<string, EntProtoId> Weapons = new();

    /// <summary>Орудия, установленные на машину (id места → сущность; сервер).</summary>
    [ViewVariables]
    public Dictionary<string, EntityUid> WeaponEntities = new();

    /// <summary>Куда выходит экипаж через люк — смещение от центра машины в её системе координат (корма).</summary>
    [DataField]
    public Vector2 ExitOffset = new(0f, 2.4f);

    [DataField]
    public TimeSpan EnterTime = TimeSpan.FromSeconds(2);

    /// <summary>Отсек (сервер).</summary>
    [ViewVariables]
    public EntityUid? InteriorMap;

    [ViewVariables]
    public EntityUid? InteriorGrid;

    /// <summary>Клетка отсека у люка, куда попадает вошедший.</summary>
    [ViewVariables]
    public Vector2 InteriorEntry;

    /// <summary>Дымовые гранатомёты: залп ставит завесу вокруг машины.</summary>
    [DataField]
    public bool Smoke;

    [DataField]
    public TimeSpan SmokeCooldown = TimeSpan.FromSeconds(60);

    [DataField]
    public EntProtoId SmokeAction = "DHActionVehicleSmoke";

    /// <summary>Слои спрайта, которые поворачиваются вместе с башней.</summary>
    [DataField]
    public List<string> TurretLayers = new() { "turret", "primary" };

    /// <summary>Прочность корпуса (как порог разрушения): от неё считается «целостность» при осмотре.</summary>
    [DataField]
    public float MaxIntegrity = 1500f;

    /// <summary>Сторона машины: тактическая карта в отсеке показывает её бойцов.</summary>
    [DataField]
    public DHAssaultFaction Faction = DHAssaultFaction.Republic;

    /// <summary>Половина размеров корпуса (как у хитбокса): снаряды вылетают за его край, а не внутри него.</summary>
    [DataField]
    public Vector2 HullHalfSize = new(1.2f, 1.9f);

    /// <summary>Отдаление камеры у водителя и стрелков (во сколько раз дальше видно).</summary>
    [DataField]
    public float CrewZoom = 1.5f;

    /// <summary>Двигатель заведён (за рулём есть водитель; сервер).</summary>
    [ViewVariables]
    public bool EngineRunning;

    [ViewVariables]
    public TimeSpan NextEngineSound;

    /// <summary>Поворот башни относительно корпуса (0 — вдоль корпуса, против часовой — положительный).</summary>
    [DataField, AutoNetworkedField]
    public Angle TurretAngle;

    /// <summary>
    /// Ось вращения башни в спрайте, в тайлах от центра, для направлений спрайта корпуса: юг, север, восток, запад.
    /// </summary>
    [DataField]
    public List<Vector2> TurretPivots = new();

    /// <summary>
    /// Где ось башни нарисована в кадрах отдельных слоёв башни (ключ слоя → юг, север, восток, запад), если не там же,
    /// где в кадре корпуса (<see cref="TurretPivots"/>). В RMC / CMU кадры башни выровнены по её оси (ось — центр
    /// кадра, здесь 0,0), а ствол бывает нарисован по корпусу.
    /// </summary>
    [DataField]
    public Dictionary<string, List<Vector2>> TurretLayerPivots = new();

    /// <summary>
    /// Корпус — статичная дочерняя сущность с хитбоксом машины, о которую упираются люди (см. серверную систему).
    /// </summary>
    [DataField]
    public EntProtoId? Hull;

    [ViewVariables]
    public EntityUid? HullEntity;

    /// <summary>Остов, который остаётся на месте уничтоженной машины.</summary>
    [DataField]
    public EntProtoId? Wreck;

    /// <summary>Наезд: урон и сбивание с ног тем, кого машина задела на скорости.</summary>
    [DataField]
    public float RunoverSpeed = 2f;

    [DataField]
    public float RunoverDamage = 20f;

    /// <summary>Люки заперты изнутри: снаружи не войти.</summary>
    [DataField, AutoNetworkedField]
    public bool Locked;

    [DataField]
    public EntProtoId HornAction = "DHActionVehicleHorn";

    [DataField]
    public EntProtoId LightsAction = "DHActionVehicleLights";

    [DataField]
    public SoundSpecifier HornSound = new SoundPathSpecifier("/Audio/Effects/Vehicle/carhorn.ogg");
}

public enum DHVehicleSeatRole : byte
{
    Driver,
    Gunner,
    Passenger,
}

/// <summary>Кресло в отсеке (простое: управление — на пультах).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHVehicleSeatComponent : Component;

/// <summary>
/// Пост экипажа в отсеке (пульт механика-водителя, прицел наводчика, пост командира). Управление берут, сев в кресло
/// рядом с пультом и нажав на него; встал с кресла — управление отпущено.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHVehicleStationComponent : Component
{
    [DataField]
    public DHVehicleSeatRole Role = DHVehicleSeatRole.Driver;

    /// <summary>Место оружия машины, которым управляет пост (наводчик, командир).</summary>
    [DataField]
    public string? Weapon;

    /// <summary>Пост управляет дымовыми гранатомётами (если они есть у машины).</summary>
    [DataField]
    public bool SmokeControl;

    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;

    /// <summary>Кто сейчас за пультом.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? User;

    /// <summary>Как далеко от пульта может стоять кресло того, кто за ним работает.</summary>
    [DataField]
    public float Range = 1.6f;

    [ViewVariables]
    public EntityUid? SmokeActionEntity;

    [ViewVariables]
    public EntityUid? HornActionEntity;

    [ViewVariables]
    public EntityUid? LightsActionEntity;

    /// <summary>Предел отдаления камеры работающего до того, как он сел за пульт (вернуть потом).</summary>
    [ViewVariables]
    public Vector2? PreviousMaxZoom;
}

/// <summary>Член экипажа за пультом.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHVehicleCrewComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Station;
}

/// <summary>Механизм заряжания в отсеке: в него закладывают боеприпасы для орудий машины.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHVehicleAmmoRackComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;
}

/// <summary>Ящик боеприпасов техники: пополняет орудие, которое стреляет <see cref="Ammo"/>.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHVehicleAmmoComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Ammo;

    /// <summary>Другие патроны орудий, в которые подходит этот ящик (например, ослабленный пулемёт танка).</summary>
    [DataField]
    public List<EntProtoId> AlsoFits = new();

    [DataField, AutoNetworkedField]
    public int Amount = 1;

    [DataField]
    public TimeSpan LoadTime = TimeSpan.FromSeconds(3);
}

[Serializable, NetSerializable]
public sealed partial class DHVehicleAmmoLoadDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Люк изнутри: выход из машины.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHVehicleHatchComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;
}

/// <summary>Орудие машины: стреляет от машины, стрелок сидит в отсеке.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHVehicleGunComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;

    /// <summary>Место орудия на машине (его указывает кресло стрелка).</summary>
    [DataField, AutoNetworkedField]
    public string Slot = string.Empty;

    /// <summary>На сколько метров от центра машины вылетает снаряд.</summary>
    [DataField]
    public float MuzzleOffset = 2f;

    /// <summary>Орудие в башне: башня доворачивается за прицелом.</summary>
    [DataField]
    public bool Turret = true;

    /// <summary>Модуль орудия разбит: не стреляет.</summary>
    [DataField, AutoNetworkedField]
    public bool Disabled;

    /// <summary>Сектор обстрела в градусах (360 — во все стороны). Бортовые порты бьют только в свой борт.</summary>
    [DataField]
    public float Arc = 360f;

    /// <summary>Середина сектора относительно корпуса, градусы (0 — вперёд, по ходу машины; 90 — влево).</summary>
    [DataField]
    public float ArcCenter;
}

/// <summary>Отсек: грид внутри машины.</summary>
[RegisterComponent]
public sealed partial class DHVehicleInteriorComponent : Component
{
    [ViewVariables]
    public EntityUid Vehicle;
}

[Serializable, NetSerializable]
public enum DHVehicleVisuals : byte
{
    Unused,
}

public sealed partial class DHVehicleSmokeActionEvent : InstantActionEvent;

/// <summary>Клиент стрелка: башня должна смотреть под этим углом к корпусу (за курсором, без выстрела).</summary>
[Serializable, NetSerializable]
public sealed class DHVehicleTurretAimEvent : EntityEventArgs
{
    public NetEntity Vehicle;
    public Angle Angle;

    public DHVehicleTurretAimEvent(NetEntity vehicle, Angle angle)
    {
        Vehicle = vehicle;
        Angle = angle;
    }
}

[Serializable, NetSerializable]
public sealed partial class DHVehicleEnterDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Планировка отсека: прямоугольная коробка со стенами по краю и сущностями внутри.</summary>
[Prototype("dhVehicleInterior")]
public sealed partial class DHVehicleInteriorPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Размер с учётом стен.</summary>
    [DataField(required: true)]
    public Vector2i Size;

    [DataField]
    public string Floor = "FloorSteel";

    [DataField]
    public EntProtoId Wall = "WallPlastitaniumIndestructible";

    /// <summary>Клетка, куда попадает вошедший (рядом с люком).</summary>
    [DataField(required: true)]
    public Vector2i Entry;

    [DataField]
    public List<DHVehicleInteriorEntity> Entities = new();

    [DataField]
    public Color Light = Color.FromHex("#9a9486");
}

[DataDefinition]
public sealed partial class DHVehicleInteriorEntity
{
    [DataField(required: true)]
    public EntProtoId Proto;

    [DataField(required: true)]
    public Vector2i Pos;

    /// <summary>Поворот в градусах.</summary>
    [DataField]
    public float Rot;

    /// <summary>Висит на стене (смотровой прибор, телефон): стена под ним остаётся.</summary>
    [DataField]
    public bool OnWall;
}
