using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.AntiTank;

// Противотанковое оружие пехоты (план осады п. 13, решения автора 2026-11-19):
//  - одноразовая труба: взять в обе руки и выстрелить, после выстрела бросается пустой тубус;
//  - ПТУР на треноге: разложить, сесть за него — ракета летит туда, куда наводчик держит курсор, пока цел провод
//    (~40 клеток) и наводчик сидит за ПТУР; встал или его ранили так, что выпал, — ракета идёт прямо.
// Обе при пуске бьют реактивной струёй назад и оставляют облачко дыма; пуск ПТУР засекается контрбатарейной
// разведкой противника, как выстрел миномёта. Всё — только через карго (ПТУР — после узла древа «Огонь»).

/// <summary>Реактивная струя назад при пуске: обжигает и роняет тех, кто стоит позади.</summary>
[RegisterComponent]
public sealed partial class DHBackblastComponent : Component
{
    [DataField]
    public float Range = 2.5f;

    /// <summary>Половина угла конуса позади, градусов.</summary>
    [DataField]
    public float HalfAngle = 35f;

    [DataField]
    public DamageSpecifier Damage = new();

    [DataField]
    public TimeSpan Knockdown = TimeSpan.FromSeconds(2);

    /// <summary>Облачко дыма на месте пуска.</summary>
    [DataField]
    public EntProtoId? Smoke = "DHAntiTankLaunchSmoke";
}

/// <summary>Одноразовая труба: после выстрела превращается в пустой тубус у ног стрелка.</summary>
[RegisterComponent]
public sealed partial class DHDisposableLauncherComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Spent;
}

/// <summary>Сложенный ПТУР: «использовать в руке» — разложить на земле перед собой.</summary>
[RegisterComponent]
public sealed partial class DHATGMFoldedComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Deployed;

    [DataField]
    public TimeSpan DeployTime = TimeSpan.FromSeconds(6);
}

/// <summary>Ракета к ПТУР (предмет).</summary>
[RegisterComponent]
public sealed partial class DHATGMMissileItemComponent : Component;

/// <summary>
/// Разложенный ПТУР на треноге. Наводчик садится за него (пристёгивается, как к креслу) и получает кнопку «Пуск»;
/// клиент наводчика шлёт положение курсора, ракета летит за ним.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHATGMComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Loaded = true;

    [DataField(required: true)]
    public EntProtoId Folded;

    /// <summary>Снаряд ракеты.</summary>
    [DataField(required: true)]
    public EntProtoId Missile;

    /// <summary>Предмет-ракета, который выпадает, если сложить заряженный ПТУР.</summary>
    [DataField(required: true)]
    public EntProtoId MissileItem;

    [DataField]
    public EntProtoId FireAction = "DHActionATGMFire";

    [DataField]
    public EntityUid? FireActionEntity;

    /// <summary>Сектор пуска: от направления треноги в обе стороны, градусов.</summary>
    [DataField]
    public float Arc = 60f;

    /// <summary>Длина провода: дальше ракета не управляется и летит прямо.</summary>
    [DataField]
    public float WireRange = 40f;

    [DataField]
    public float MissileSpeed = 9f;

    /// <summary>Как быстро ракета доворачивает за курсором, градусов в секунду.</summary>
    [DataField]
    public float TurnRate = 110f;

    [DataField]
    public TimeSpan ReloadTime = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan FoldTime = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier LaunchSound = new SoundPathSpecifier("/Audio/_DarkestHour/Aviation/rocket-release.ogg");

    [DataField]
    public SoundSpecifier ReloadSound = new SoundPathSpecifier("/Audio/Weapons/Guns/MagIn/batrifle_magin.ogg");

    /// <summary>Кто сейчас сидит за ПТУР.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Operator;

    /// <summary>Куда наводчик держит курсор (координаты карты), от клиента.</summary>
    [ViewVariables]
    public Vector2? Aim;
}

[Serializable, NetSerializable]
public enum DHATGMVisuals : byte
{
    Loaded,
}

/// <summary>Клиент наводчика → сервер: курсор (координаты на карте ПТУР).</summary>
[Serializable, NetSerializable]
public sealed class DHATGMAimEvent : EntityEventArgs
{
    public NetEntity Launcher;
    public Vector2 Position;

    public DHATGMAimEvent(NetEntity launcher, Vector2 position)
    {
        Launcher = launcher;
        Position = position;
    }
}

/// <summary>Кнопка «Пуск» у наводчика ПТУР.</summary>
public sealed partial class DHATGMFireActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class DHATGMDeployDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHATGMFoldDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHATGMReloadDoAfterEvent : SimpleDoAfterEvent;
