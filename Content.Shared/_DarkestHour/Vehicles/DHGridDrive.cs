using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Vehicles;

/// <summary>
/// Движение техники по клеткам, как в CM-SS13 / RMC (решение автора 2026-11-11): машина едет шагами по тайлам,
/// разгоняется на прямой, поворачивает на месте на 90°, сдаёт назад медленнее. Кого задела — сбивает и отталкивает,
/// баррикады и лёгкие постройки ломает, о стены и другую технику упирается. Колёса или гусеницы — модуль
/// (<see cref="DHVehicleHardpointsComponent"/>): без них машина стоит.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHGridDriveComponent : Component
{
    /// <summary>Наибольшая скорость, клеток в секунду.</summary>
    [DataField]
    public float TilesPerSecond = 3f;

    /// <summary>Скорость с места (доля наибольшей); до наибольшей — за <see cref="RampSteps"/> шагов.</summary>
    [DataField]
    public float StartFraction = 0.4f;

    [DataField]
    public int RampSteps = 4;

    /// <summary>Задний ход (доля наибольшей).</summary>
    [DataField]
    public float ReverseFraction = 0.5f;

    /// <summary>Поворот на 90° на месте.</summary>
    [DataField]
    public TimeSpan TurnTime = TimeSpan.FromSeconds(0.8);

    /// <summary>Урон постройке, в которую машина въехала (за шаг).</summary>
    [DataField]
    public float SmashDamage = 300f;

    // --- состояние (сервер) ---

    [ViewVariables]
    public bool Moving;

    [ViewVariables]
    public Vector2 From;

    [ViewVariables]
    public Vector2 To;

    [ViewVariables]
    public float Progress;

    [ViewVariables]
    public float StepDuration;

    [ViewVariables]
    public int Streak;

    [ViewVariables]
    public TimeSpan BusyUntil;

    [ViewVariables]
    public TimeSpan LastStep;
}
