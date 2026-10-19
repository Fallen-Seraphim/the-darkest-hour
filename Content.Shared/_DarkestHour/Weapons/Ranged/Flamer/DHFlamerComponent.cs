using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Weapons.Ranged.Flamer;

/// <summary>
/// Огнемёт: берёт горючее из бака в слоте и выпускает струю огня по тайлам.
/// Стреляет через ванильный GunSystem как хитскан (сущность-выстрел <see cref="ShotPrototype"/>).
/// По образцу RMCFlamerAmmoProvider из RMC-14 (MIT).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHFlamerComponent : Component
{
    /// <summary>Слот бака.</summary>
    [DataField, AutoNetworkedField]
    public string ContainerId = "gun_magazine";

    /// <summary>Единиц горючего на один тайл струи.</summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 CostPer = FixedPoint2.New(1);

    /// <summary>Задержка между тайлами струи — огонь «бежит» вперёд.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DelayPer = TimeSpan.FromSeconds(0.05);

    /// <summary>Предельная дальность самого огнемёта (дальность реагента может быть меньше).</summary>
    [DataField, AutoNetworkedField]
    public int MaxRange = 6;

    /// <summary>Доля горючих реагентов в баке, ниже которой смесь не горит.</summary>
    [DataField, AutoNetworkedField]
    public float MinFlammableFraction = 0.5f;

    [DataField, AutoNetworkedField]
    public EntProtoId ShotPrototype = "DHFlamerShot";

    /// <summary>Запальник (пилотное пламя) зажжён.</summary>
    [DataField, AutoNetworkedField]
    public bool Ignited;

    [DataField]
    public SoundSpecifier? IgniteSound = new SoundPathSpecifier("/Audio/_RMC14/Weapons/Handling/flamer_ignition.ogg");

    [DataField]
    public SoundSpecifier? ExtinguishSound = new SoundPathSpecifier("/Audio/_RMC14/Weapons/Guns/Flamer/surv_flamer_extinguish.ogg");
}

/// <summary>Бак огнемёта.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHFlamerTankComponent : Component
{
    [DataField, AutoNetworkedField]
    public string SolutionId = "tank";
}

/// <summary>Метка сущности-выстрела огнемёта (живёт один кадр, обрабатывается на сервере).</summary>
[RegisterComponent]
public sealed partial class DHFlamerShotComponent : Component;

[Serializable, NetSerializable]
public enum DHFlamerVisuals : byte
{
    Ignited,
}
