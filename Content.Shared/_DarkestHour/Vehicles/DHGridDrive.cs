using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Vehicles;

/// <summary>
/// Вождение техники, как в RMC-14 / CMU (GridVehicleMover, решение автора 2026-11-12): W/S — газ и тормоз (задний ход),
/// A/D — поворот корпуса с разгоном и торможением вращения; машина едет туда, куда смотрит, под любым углом. Танк
/// крутится и на месте. На малой скорости корпус, почти выровненный по стороне света, сам довернёт до неё, а ровно
/// стоящая машина держит полосу — так проще заехать в ворота. Кого задела — сбивает и отталкивает, баррикады и лёгкие
/// постройки ломает, о стены и другую технику упирается. Колёса или гусеницы — модуль
/// (<see cref="DHVehicleHardpointsComponent"/>): без них машина стоит. Числа — из прототипов RMC.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHGridDriveComponent : Component
{
    /// <summary>Наибольшая скорость вперёд, тайлов в секунду.</summary>
    [DataField]
    public float MaxSpeed = 3f;

    /// <summary>Разгон вперёд, тайлов/с².</summary>
    [DataField]
    public float Acceleration = 2f;

    /// <summary>Торможение (газ отпущен или нажат в обратную сторону), тайлов/с².</summary>
    [DataField]
    public float Deceleration = 8f;

    [DataField]
    public float MaxReverseSpeed = 2f;

    [DataField]
    public float ReverseAcceleration = 2f;

    /// <summary>Наибольшая скорость поворота корпуса, градусов в секунду.</summary>
    [DataField]
    public float MaxRotationSpeedDegrees = 45f;

    [DataField]
    public float RotationAccelerationDegrees = 90f;

    /// <summary>Торможение вращения, когда A/D отпущены.</summary>
    [DataField]
    public float RotationDecelerationDegrees = 180f;

    /// <summary>Поворот на месте (гусеницы). Колёсная машина рулит только на ходу.</summary>
    [DataField]
    public bool TurnInPlace;

    /// <summary>Доводка до стороны света на малой скорости: в пределах стольких градусов.</summary>
    [DataField]
    public float AlignmentAssistDegrees = 8f;

    /// <summary>Доводка работает не быстрее этой скорости, тайлов/с.</summary>
    [DataField]
    public float AlignmentAssistMaxSpeed = 1.5f;

    /// <summary>Насколько можно сдвинуть корпус вбок (и вдоль), чтобы найти место для поворота или проехать в проём.</summary>
    [DataField]
    public float NudgeLimit = 0.45f;

    /// <summary>Урон постройке, в которую машина упёрлась (не чаще раза в <see cref="SmashCooldown"/>).</summary>
    [DataField]
    public float SmashDamage = 300f;

    [DataField]
    public TimeSpan SmashCooldown = TimeSpan.FromSeconds(0.35);

    // --- состояние (сервер) ---

    /// <summary>Скорость со знаком (минус — задний ход), тайлов/с.</summary>
    [ViewVariables]
    public float CurrentSpeed;

    /// <summary>Скорость вращения корпуса со знаком (плюс — против часовой, влево), градусов/с.</summary>
    [ViewVariables]
    public float AngularVelocityDegrees;

    /// <summary>Едет или поворачивает (звук двигателя).</summary>
    [ViewVariables]
    public bool IsMoving;
}
