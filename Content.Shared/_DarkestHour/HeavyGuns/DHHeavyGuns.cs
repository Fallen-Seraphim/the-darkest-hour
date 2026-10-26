using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.HeavyGuns;

// Тяжёлые орудия «Штурма»: корабельная артиллерия (бьёт с орбиты по координатам на планете), ПКО (бьёт с планеты по
// кораблям на орбите) и ПВО (перехватывает летящие снаряды). Орудие собирается из частей, как ускоритель частиц:
// казённик + ствол того же калибра на соседнем тайле по направлению казённика; наводят с консоли, сидя на стуле.
// Снаряды — тяжёлые предметы в две руки, заряжаются в казённик. Боеголовки: фугасная, бронебойная, зажигательная,
// кассетная.

[Serializable, NetSerializable]
public enum DHGunRole : byte
{
    /// <summary>Корабельная артиллерия: с орбиты по поверхности планеты.</summary>
    Artillery,

    /// <summary>ПКО: с поверхности планеты по кораблям на орбите.</summary>
    AntiOrbital,
}

[Serializable, NetSerializable]
public enum DHWarhead : byte
{
    HighExplosive,
    ArmorPiercing,
    Incendiary,
    Cluster,

    /// <summary>Осветительная мина: над точкой падения долго горит яркий огонь (ночью планеты темно).</summary>
    Flare,
}

/// <summary>Казённик тяжёлого орудия: сердце сборки, хранит заряженный снаряд.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHHeavyGunBreechComponent : Component
{
    /// <summary>Калибр, мм. Ствол и снаряды — того же калибра.</summary>
    [DataField(required: true)]
    public int Caliber;

    [DataField]
    public DHGunRole Role = DHGunRole.Artillery;

    /// <summary>Перезарядка после выстрела.</summary>
    [DataField]
    public TimeSpan ReloadTime = TimeSpan.FromSeconds(60);

    /// <summary>Заряжание снаряда в казённик.</summary>
    [DataField]
    public TimeSpan LoadTime = TimeSpan.FromSeconds(4);

    /// <summary>ПКО: шанс попадания по кораблю (до перехвата ПВО).</summary>
    [DataField]
    public float HitChance = 0.5f;

    /// <summary>Артиллерия: разброс точки падения, м.</summary>
    [DataField]
    public float Spread = 3f;

    /// <summary>Время полёта снаряда.</summary>
    [DataField]
    public TimeSpan FlightTime = TimeSpan.FromSeconds(12);

    /// <summary>Урон прочности авианосца при попадании (ПКО), до множителя боеголовки.</summary>
    [DataField]
    public float HullDamage = 10f;

    [DataField]
    public string Container = "breech";

    [ViewVariables]
    public TimeSpan NextShot;
}

/// <summary>Ствол тяжёлого орудия. Ставится на соседний тайл перед казёнником, повёрнутым так же.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHHeavyGunBarrelComponent : Component
{
    [DataField(required: true)]
    public int Caliber;
}

/// <summary>Снаряд тяжёлого орудия.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHHeavyShellComponent : Component
{
    [DataField(required: true)]
    public int Caliber;

    [DataField]
    public DHWarhead Warhead = DHWarhead.HighExplosive;
}

/// <summary>Консоль наведения одного тяжёлого орудия. Работает, только если оператор сидит (пристёгнут).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHHeavyGunConsoleComponent : Component
{
    [DataField]
    public DHGunRole Role = DHGunRole.Artillery;

    /// <summary>ПКО: наибольшая высота корабля над поверхностью тела, по которому можно стрелять, м.</summary>
    [DataField]
    public float OrbitalRange = 20000f;

    /// <summary>Артиллерия: наибольшая высота корабля над телом, с которой можно стрелять по поверхности, м.</summary>
    [DataField]
    public float ArtilleryRange = 20000f;

    /// <summary>Артиллерия: половина стороны зоны, куда можно навести, м.</summary>
    [DataField]
    public float TargetZone = 1500f;

    /// <summary>
    /// Под каждую пушку своя консоль: консоль сама привязывается к ближайшему казённику своей роли на том же гриде
    /// в пределах этого радиуса, если к нему ещё не привязана другая консоль.
    /// </summary>
    [DataField]
    public float LinkRange = 5f;

    [ViewVariables]
    public EntityUid? Gun;

    [ViewVariables]
    public string? SelectedBody;
}

[Serializable, NetSerializable]
public enum DHInterceptorKind : byte
{
    Missile,
    Autocannon,
}

/// <summary>
/// ПВО: перехватывает тяжёлые снаряды противника — на своём гриде (корабль) или в радиусе на поверхности (планета);
/// с поверхности ещё и бьёт по авианосцу, висящему в атмосфере над ней. Каждая попытка тратит заряд.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHInterceptorComponent : Component
{
    [DataField]
    public DHInterceptorKind Kind = DHInterceptorKind.Missile;

    [DataField]
    public int Ammo = 4;

    [DataField]
    public int MaxAmmo = 4;

    /// <summary>Шанс перехвата одной попыткой.</summary>
    [DataField]
    public float Chance = 0.35f;

    /// <summary>На поверхности: радиус прикрытия, м.</summary>
    [DataField]
    public float Radius = 120f;

    /// <summary>Между попытками.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(6);

    /// <summary>
    /// На поверхности: дальность огня по авианосцу, висящему в атмосфере над этим местом (по горизонтали), м.
    /// 0 — не стреляет по воздушным целям.
    /// </summary>
    [DataField]
    public float AirRange;

    /// <summary>Шанс попадания по авианосцу в атмосфере одним выстрелом.</summary>
    [DataField]
    public float AirChance = 0.5f;

    /// <summary>Урон прочности авианосца при попадании.</summary>
    [DataField]
    public float AirDamage = 2f;

    [ViewVariables]
    public TimeSpan NextReady;
}

/// <summary>Боекомплект ПВО: ракета или ящик снарядов к автопушке.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHInterceptorAmmoComponent : Component
{
    [DataField]
    public DHInterceptorKind Kind = DHInterceptorKind.Missile;

    [DataField]
    public int Amount = 1;
}

[Serializable, NetSerializable]
public enum DHHeavyGunVisuals : byte
{
    Assembled,
    Loaded,
}

[Serializable, NetSerializable]
public enum DHHeavyGunUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHGunEntry(NetEntity gun, string name, int caliber, string status, bool ready)
{
    public readonly NetEntity Gun = gun;
    public readonly string Name = name;
    public readonly int Caliber = caliber;
    public readonly string Status = status;
    public readonly bool Ready = ready;
}

[Serializable, NetSerializable]
public sealed class DHGunTargetEntry(NetEntity target, string name, float altitude)
{
    public readonly NetEntity Target = target;
    public readonly string Name = name;
    public readonly float Altitude = altitude;
}

[Serializable, NetSerializable]
public sealed class DHGunBodyEntry(string id, string name, float altitude)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly float Altitude = altitude;
}

[Serializable, NetSerializable]
public sealed class DHGunDefenseEntry(string name, int ammo, int maxAmmo)
{
    public readonly string Name = name;
    public readonly int Ammo = ammo;
    public readonly int MaxAmmo = maxAmmo;
}

[Serializable, NetSerializable]
public sealed class DHHeavyGunConsoleState(
    DHGunRole role,
    bool seated,
    List<DHGunEntry> guns,
    List<DHGunTargetEntry> targets,
    List<DHGunBodyEntry> bodies,
    string? selectedBody,
    float targetZone,
    List<DHGunDefenseEntry> defenses,
    List<DHLaserMarkEntry> marks,
    string status) : BoundUserInterfaceState
{
    public readonly DHGunRole Role = role;
    public readonly bool Seated = seated;
    public readonly List<DHGunEntry> Guns = guns;
    public readonly List<DHGunTargetEntry> Targets = targets;
    public readonly List<DHGunBodyEntry> Bodies = bodies;
    public readonly string? SelectedBody = selectedBody;
    public readonly float TargetZone = targetZone;
    public readonly List<DHGunDefenseEntry> Defenses = defenses;

    /// <summary>Метки ЛЦУ своей стороны на поверхностях, по которым может бить артиллерия.</summary>
    public readonly List<DHLaserMarkEntry> Marks = marks;

    public readonly string Status = status;
}

/// <summary>Залп выбранными орудиями: ПКО — по кораблю, артиллерия — по точке поверхности тела.</summary>
[Serializable, NetSerializable]
public sealed class DHHeavyGunFireMessage(List<NetEntity> guns, NetEntity? target, string? body, Vector2 position)
    : BoundUserInterfaceMessage
{
    public readonly List<NetEntity> Guns = guns;
    public readonly NetEntity? Target = target;
    public readonly string? Body = body;
    public readonly Vector2 Position = position;
}

[Serializable, NetSerializable]
public sealed class DHHeavyGunSelectBodyMessage(string body) : BoundUserInterfaceMessage
{
    public readonly string Body = body;
}

[Serializable, NetSerializable]
public sealed partial class DHHeavyGunLoadDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHInterceptorLoadDoAfterEvent : SimpleDoAfterEvent;
