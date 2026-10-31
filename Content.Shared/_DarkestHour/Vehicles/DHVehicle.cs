using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
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
}

public enum DHVehicleSeatRole : byte
{
    Driver,
    Gunner,
    Passenger,
}

/// <summary>Кресло в отсеке машины.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHVehicleSeatComponent : Component
{
    [DataField]
    public DHVehicleSeatRole Role = DHVehicleSeatRole.Passenger;

    /// <summary>Для стрелка — место оружия машины, которым он управляет.</summary>
    [DataField]
    public string? Weapon;

    /// <summary>Сидящий получает кнопку дымовой завесы (если она есть у машины).</summary>
    [DataField]
    public bool SmokeControl;

    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;

    [ViewVariables]
    public EntityUid? SmokeActionEntity;

    /// <summary>Предел отдаления камеры сидящего до того, как он сел (вернуть при вставании).</summary>
    [ViewVariables]
    public Vector2? PreviousMaxZoom;
}

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
