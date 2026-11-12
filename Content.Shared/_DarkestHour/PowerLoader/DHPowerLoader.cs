using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.PowerLoader;

// Грузовой экзоскелет-погрузчик (авиация, этап 4; решение автора 2026-11-12), по образцу LU-50 из RMC-14, но свой:
// пристегнулся — едешь (WASD через реле), обе руки заняты клешнями. Клик клешнёй по тяжёлому грузу (авиабоеприпас,
// модуль, снаряд, ракета-перехватчик, ящик) — захват; клик с грузом по штурмовику, казённику или ПВО — зарядить туда
// (как будто вручную, той же логикой), клик по полу — поставить.

/// <summary>Погрузчик: держит один груз в клешнях.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHPowerLoaderComponent : Component
{
    [DataField]
    public string Container = "dh-power-loader-clamp";

    /// <summary>Как далеко от погрузчика клешни берут и ставят груз, клеток.</summary>
    [DataField]
    public float Reach = 1.8f;

    /// <summary>Дальше этого не достать даже до крупной цели (центр штурмовика), клеток.</summary>
    [DataField]
    public float BigReach = 4f;

    [DataField]
    public TimeSpan GrabTime = TimeSpan.FromSeconds(1.5);

    [DataField]
    public SoundSpecifier GrabSound = new SoundPathSpecifier("/Audio/Mecha/sound_mecha_hydraulic.ogg");
}

/// <summary>Клешня погрузчика — виртуальный предмет в руке пилота.</summary>
[RegisterComponent]
public sealed partial class DHPowerLoaderClampComponent : Component
{
    [ViewVariables]
    public EntityUid Loader;
}

/// <summary>Груз, который клешни погрузчика могут взять (ящики берутся и без этой метки).</summary>
[RegisterComponent]
public sealed partial class DHPowerLoaderGrabbableComponent : Component;

[Serializable, NetSerializable]
public sealed partial class DHPowerLoaderGrabDoAfterEvent : SimpleDoAfterEvent;
