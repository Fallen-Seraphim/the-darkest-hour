using Content.Shared.Shuttles.BUIStates;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Shuttles.Weapons;

// Корабельное и станционное орудие The Darkest Hour. По образцу Monolith FireControl/SpaceArtillery (AGPL), переписано
// компактно: без серверов наведения, хардпоинтов, радарных меток, щитов и тепловых сигнатур. Консоль наведения
// управляет всеми орудиями на своём гриде; станционная консоль может включить автоматическую противометеорную защиту.

/// <summary>
/// Орудие, управляемое с консоли наведения. Само оружие — обычный Gun апстрима (магазин или снаряды внутри);
/// башня поворачивается к цели. Нужно питание.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHShipGunComponent : Component
{
    /// <summary>Калибр для списка орудий на консоли.</summary>
    [DataField]
    public LocId Caliber = "dh-ship-gun-caliber-unknown";

    /// <summary>Может работать в режиме противометеорной защиты (малые калибры: 20 и 30 мм).</summary>
    [DataField]
    public bool PointDefense;

    /// <summary>Дальность автоматического огня по метеорам.</summary>
    [DataField]
    public float PointDefenseRange = 40f;

    /// <summary>Снаряды взрываются рядом с метеором (неконтактный взрыватель), а не только при попадании.</summary>
    [DataField]
    public float ProximityRange = 0.9f;

    /// <summary>Точка, по которой ведётся огонь (координаты карты).</summary>
    [ViewVariables]
    public MapCoordinates? FireTarget;

    /// <summary>До какого времени вести огонь по точке.</summary>
    [ViewVariables]
    public TimeSpan FireUntil;

    /// <summary>Сейчас стреляет по метеору (автоматика).</summary>
    [ViewVariables]
    public EntityUid? AutoTarget;
}

/// <summary>
/// Снаряд корабельного орудия: пролетает сквозь свой грид (орудие стоит внутри корпуса), снаряды противометеорных
/// орудий подрываются рядом с метеором.
/// </summary>
[RegisterComponent]
public sealed partial class DHShipProjectileComponent : Component
{
    [ViewVariables]
    public EntityUid? SourceGrid;

    /// <summary>Радиус неконтактного подрыва по метеорам; 0 — только прямое попадание.</summary>
    [ViewVariables]
    public float ProximityRange;
}

/// <summary>
/// Консоль наведения: радар, список орудий на гриде, огонь по клику, режим непрерывного огня. Станционная консоль
/// (<see cref="AutoDefense"/>) может включить автоматическую противометеорную защиту.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHGunneryConsoleComponent : Component
{
    [DataField]
    public bool AutoDefense;

    /// <summary>Сколько секунд ведётся огонь после одного клика по радару.</summary>
    [DataField]
    public TimeSpan Burst = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan NextUiUpdate;
}

/// <summary>
/// Автоматическая противометеорная защита грида включена (ставит консоль наведения со станционным режимом).
/// </summary>
[RegisterComponent]
public sealed partial class DHPointDefenseGridComponent : Component
{
    [ViewVariables]
    public bool Enabled;

    [ViewVariables]
    public TimeSpan NextScan;
}

[Serializable, NetSerializable]
public enum DHGunneryUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHGunneryGunEntry(NetEntity gun, string name, string caliber, int ammo, int capacity, bool powered, bool pointDefense, bool firing)
{
    public readonly NetEntity Gun = gun;
    public readonly string Name = name;
    public readonly string Caliber = caliber;
    public readonly int Ammo = ammo;
    public readonly int Capacity = capacity;
    public readonly bool Powered = powered;
    public readonly bool PointDefense = pointDefense;
    public readonly bool Firing = firing;
}

[Serializable, NetSerializable]
public sealed class DHGunneryBoundUserInterfaceState(NavInterfaceState nav, List<DHGunneryGunEntry> guns, bool autoDefenseAvailable, bool autoDefense)
    : BoundUserInterfaceState
{
    public readonly NavInterfaceState Nav = nav;
    public readonly List<DHGunneryGunEntry> Guns = guns;
    public readonly bool AutoDefenseAvailable = autoDefenseAvailable;
    public readonly bool AutoDefense = autoDefense;
}

/// <summary>Огонь выбранными орудиями по точке радара.</summary>
[Serializable, NetSerializable]
public sealed class DHGunneryFireMessage(NetCoordinates target, List<NetEntity> guns, bool continuous) : BoundUserInterfaceMessage
{
    public readonly NetCoordinates Target = target;
    public readonly List<NetEntity> Guns = guns;
    public readonly bool Continuous = continuous;
}

[Serializable, NetSerializable]
public sealed class DHGunneryCeaseFireMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DHGunneryAutoDefenseMessage(bool enabled) : BoundUserInterfaceMessage
{
    public readonly bool Enabled = enabled;
}
