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

// --- Пульт Z-лифта (решение автора 2026-11-05): окно со всеми этажами шахты вместо меню действий ---

[Serializable, NetSerializable]
public enum DHLiftPanelUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHLiftFloor(int level, string name, NetEntity lift, bool here, bool platformHere)
{
    public readonly int Level = level;
    public readonly string Name = name;
    public readonly NetEntity Lift = lift;

    /// <summary>Этаж пульта.</summary>
    public readonly bool Here = here;

    /// <summary>Платформа сейчас стоит на этом этаже (в шахте одна «живая» платформа — та, к которой вызывали).</summary>
    public readonly bool PlatformHere = platformHere;
}

[Serializable, NetSerializable]
public sealed class DHLiftPanelState(List<DHLiftFloor> floors, bool busy) : BoundUserInterfaceState
{
    public readonly List<DHLiftFloor> Floors = floors;
    public readonly bool Busy = busy;
}

/// <summary>Отправить груз с платформы этого этажа на выбранный.</summary>
[Serializable, NetSerializable]
public sealed class DHLiftSendMessage(NetEntity to) : BoundUserInterfaceMessage
{
    public readonly NetEntity To = to;
}

/// <summary>Вызвать груз с выбранного этажа на этаж пульта.</summary>
[Serializable, NetSerializable]
public sealed class DHLiftCallMessage(NetEntity from) : BoundUserInterfaceMessage
{
    public readonly NetEntity From = from;
}
