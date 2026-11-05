using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Levels;

// Уровни по образцу RMC-14 (у SS14 нет Z-уровней): подземелье и этажи — отдельные карты, переходы —
// лестницы/люки и лифты. С ключом Link — ведут к паре с тем же ключом (старый бункер); без ключа — в ту же точку
// XY на соседнем Z-уровне (DHZLevelComponent).

/// <summary>
/// Лестница или люк: подняться/спуститься к лестнице с тем же <see cref="Link"/> на другом уровне, а без ключа —
/// на уровень выше/ниже в той же точке.
/// </summary>
[RegisterComponent]
public sealed partial class DHLadderComponent : Component
{
    [DataField]
    public string Link = string.Empty;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);

    /// <summary>true — ведёт вниз (люк), false — наверх.</summary>
    [DataField]
    public bool Down;
}

/// <summary>
/// Платформа лифта: всё незакреплённое на ней (люди, ящики) переезжает на платформу-пару (по ключу Link), а без
/// ключа — на платформу выбранного этажа в той же шахте (платформы в той же точке XY на других уровнях).
/// </summary>
[RegisterComponent]
public sealed partial class DHLiftComponent : Component
{
    [DataField]
    public string Link = string.Empty;

    /// <summary>Половина стороны платформы, м (3x3 тайла — 1.45).</summary>
    [DataField]
    public float HalfSize = 1.45f;

    [DataField]
    public TimeSpan TravelTime = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(4);

    /// <summary>Рейс назначен: отправка в это время (только сервер).</summary>
    [ViewVariables]
    public TimeSpan? DepartAt;

    [ViewVariables]
    public TimeSpan NextUse;

    /// <summary>Куда едет назначенный рейс (только сервер).</summary>
    [ViewVariables]
    public EntityUid? Destination;
}

/// <summary>Пульт лифта: отправить груз со своей платформы или вызвать с другого уровня (этажи — в меню действий).</summary>
[RegisterComponent]
public sealed partial class DHLiftPanelComponent : Component
{
    [DataField]
    public string Link = string.Empty;
}

[Serializable, NetSerializable]
public sealed partial class DHLadderDoAfterEvent : SimpleDoAfterEvent;
