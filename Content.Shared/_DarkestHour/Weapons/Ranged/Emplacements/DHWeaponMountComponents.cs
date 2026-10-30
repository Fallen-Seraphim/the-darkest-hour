using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Weapons.Ranged.Emplacements;

// Станковое оружие The Darkest Hour. Механика по образцу WeaponMount / MountableWeapon / Overheat из RMC-14 (MIT),
// упрощена: станок и пулемёт — один предмет, который разворачивают перед собой и за которым встают.

/// <summary>
/// Станок с пулемётом. В сложенном виде — тяжёлый предмет; развёрнутый — закреплён на тайле, за ним встаёт стрелок.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(DHWeaponMountSystem))]
public sealed partial class DHWeaponMountComponent : Component
{
    /// <summary>Пулемёт, который при появлении ставится на станок.</summary>
    [DataField(required: true)]
    public EntProtoId Weapon;

    [DataField]
    public string WeaponSlotId = "dh_mounted_weapon";

    [DataField, AutoNetworkedField]
    public EntityUid? MountedWeapon;

    /// <summary>Кто сейчас стоит за пулемётом.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? User;

    [DataField, AutoNetworkedField]
    public bool Deployed;

    /// <summary>Пулемётное гнездо: станок стоит в окопе или на мешках (или стрелок в окопе) — разброс меньше.</summary>
    [DataField, AutoNetworkedField]
    public bool Nested;

    /// <summary>Множитель разброса и отдачи в гнезде.</summary>
    [DataField]
    public float NestSpreadMultiplier = 0.6f;

    [DataField]
    public TimeSpan DeployDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan UndeployDelay = TimeSpan.FromSeconds(3);

    /// <summary>Насколько впереди станка (в тайлах) появляются пули.</summary>
    [DataField]
    public float MuzzleOffset = 0.75f;

    /// <summary>Разворачивать станок за прицелом стрелка, если цель вне сектора.</summary>
    [DataField]
    public bool RotateToAim = true;

    [DataField]
    public EntProtoId DismountAction = "DHActionDismountWeapon";

    [DataField, AutoNetworkedField]
    public EntityUid? DismountActionEntity;

    [DataField]
    public SoundSpecifier? DeploySound = new SoundPathSpecifier("/Audio/Items/ratchet.ogg");

    [DataField]
    public SoundSpecifier? UndeploySound = new SoundPathSpecifier("/Audio/Items/screwdriver.ogg");

    [DataField]
    public SoundSpecifier? RotateSound = new SoundPathSpecifier("/Audio/Items/ratchet.ogg");
}

/// <summary>
/// Пулемёт, который стреляет только со станка.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(DHWeaponMountSystem))]
public sealed partial class DHMountableWeaponComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? MountedTo;

    /// <summary>Сектор обстрела в градусах (по центру — направление станка).</summary>
    [DataField]
    public float ShootArc = 120f;

    /// <summary>Сколько рук должно быть свободно у стрелка.</summary>
    [DataField]
    public int RequiredFreeHands = 2;
}

/// <summary>
/// Стрелок за станковым оружием: его выстрелы идут из пулемёта.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(DHWeaponMountSystem))]
public sealed partial class DHWeaponControllerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Weapon;

    [DataField, AutoNetworkedField]
    public EntityUid? Mount;
}

/// <summary>
/// Перегрев ствола. Тепло остывает со временем без постоянного тика: текущее значение считается по времени
/// последнего изменения.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(DHOverheatSystem))]
public sealed partial class DHOverheatComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Heat;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan HeatUpdated;

    [DataField]
    public float MaxHeat = 40f;

    [DataField]
    public float HeatPerShot = 1f;

    /// <summary>Остывание в единицах тепла в секунду.</summary>
    [DataField]
    public float CooldownRate = 2f;

    /// <summary>Сколько секунд пулемёт не стреляет после перегрева.</summary>
    [DataField]
    public TimeSpan OverheatLockout = TimeSpan.FromSeconds(3);

    /// <summary>Во сколько раз падает тепло после перегрева.</summary>
    [DataField]
    public float OverheatHeatMultiplier = 0.4f;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan OverheatedUntil;

    /// <summary>Урон станку при перегреве.</summary>
    [DataField]
    public DamageSpecifier? Damage;

    [DataField]
    public SoundSpecifier? OverheatSound = new SoundPathSpecifier("/Audio/Effects/extinguish.ogg");
}

[Serializable, NetSerializable]
public enum DHWeaponMountVisuals : byte
{
    Deployed,
    Ammo,
}

[Serializable, NetSerializable]
public enum DHWeaponMountLayers : byte
{
    Folded,
    Deployed,
    Ammo,
    Heat,
}
