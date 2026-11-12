using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Aviation;

// ПЗРК Республики (авиация, этап 4; решение автора 2026-11-12): бьёт только по штурмовику на бреющем — в атмосфере
// его не достать. Взять в обе руки, кликнуть в сторону неба: захват ближайшего штурмовика на заходе в радиусе
// (пилот слышит предупреждение), через несколько секунд — пуск. Попадание зависит от высоты захода; свежие ЛТЦ
// почти всегда уводят ракету. После пуска — перезарядка ракетой из ящика.

/// <summary>Переносной зенитный ракетный комплекс: одна ракета в трубе.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHManpadsComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Loaded = true;

    /// <summary>Дальность захвата по горизонтали, клеток.</summary>
    [DataField]
    public float Range = 30f;

    /// <summary>Сколько держать цель до пуска.</summary>
    [DataField]
    public TimeSpan LockTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan ReloadTime = TimeSpan.FromSeconds(4);

    /// <summary>Скорость ракеты, клеток в секунду (время полёта — по расстоянию).</summary>
    [DataField]
    public float MissileSpeed = 35f;

    /// <summary>Шанс попадания на средней высоте (низкая — выше, высокая — ниже).</summary>
    [DataField]
    public float HitChance = 0.6f;

    /// <summary>Шанс, что свежие ЛТЦ уведут ракету.</summary>
    [DataField]
    public float FlareDecoyChance = 0.85f;

    /// <summary>Урон прочности штурмовика (из 100).</summary>
    [DataField]
    public float Damage = 35f;

    [DataField]
    public SoundSpecifier LaunchSound = new SoundPathSpecifier("/Audio/_DarkestHour/Aviation/rocket-release.ogg");

    [DataField]
    public SoundSpecifier LockSound = new SoundPathSpecifier("/Audio/_DarkestHour/Aviation/manpad-lock.wav");

    [DataField]
    public SoundSpecifier ReloadSound = new SoundPathSpecifier("/Audio/Weapons/Guns/MagIn/batrifle_magin.ogg");
}

/// <summary>Ракета к ПЗРК.</summary>
[RegisterComponent]
public sealed partial class DHManpadsMissileComponent : Component;

[Serializable, NetSerializable]
public sealed partial class DHManpadsLockDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHManpadsReloadDoAfterEvent : SimpleDoAfterEvent;
