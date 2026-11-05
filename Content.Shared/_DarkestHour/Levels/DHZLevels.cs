using System.Numerics;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Levels;

// Z-уровни: у SS14 одна карта — один слой, поэтому этаж — отдельная карта с теми же координатами, что у земли.
// Карты одного места (земля, этажи, подвалы) — одна «стопка» (Stack). Уровень 0 — земля, +1..+8 — этажи и крыши
// (крыша 7-этажного дома — +8), −1..−3 — подземелье (канализация, бункеры). Переходы — лестничные пролёты,
// лестницы, люки и лифты: ведут в ту же точку XY на соседнем уровне.

/// <summary>Карта — уровень стопки Z-уровней.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHZLevelComponent : Component
{
    public const int MinLevel = -3;
    public const int MaxLevel = 8;

    /// <summary>Номер стопки: карты с одним номером лежат друг над другом.</summary>
    [DataField, AutoNetworkedField]
    public int Stack;

    [DataField, AutoNetworkedField]
    public int Level;
}

/// <summary>
/// Лестничный пролёт: шагнул на ступени по направлению пролёта (куда он повёрнут) — оказался на соседнем уровне,
/// на клетку дальше. Нижний пролёт смотрит вверх по лестнице (Up), верхний проём — вниз.
/// </summary>
[RegisterComponent]
public sealed partial class DHZStairsComponent : Component
{
    [DataField]
    public bool Up = true;

    /// <summary>На сколько клеток дальше пролёта выходишь.</summary>
    [DataField]
    public float Exit = 1f;
}

/// <summary>
/// Боец, который умеет смотреть на соседние уровни (кнопки «Посмотреть вверх/вниз»): у лестницы, люка, проёма,
/// с края крыши или под открытым небом. С прицелом x8 — на 3 уровня, x16 — на все.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHZViewerComponent : Component
{
    [DataField]
    public EntProtoId LookUpAction = "DHActionLookUp";

    [DataField]
    public EntProtoId LookDownAction = "DHActionLookDown";

    [DataField]
    public EntityUid? LookUpActionEntity;

    [DataField]
    public EntityUid? LookDownActionEntity;

    /// <summary>Камера на другом уровне, через которую сейчас смотрит боец.</summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Camera;

    /// <summary>На какой уровень смотрит (если смотрит).</summary>
    [ViewVariables, AutoNetworkedField]
    public int ViewLevel;

    /// <summary>Откуда смотрит: отошёл дальше — взгляд сбрасывается.</summary>
    [ViewVariables]
    public Vector2 Anchor;
}

/// <summary>Прицел (обвес или встроенный): на сколько уровней видно с края, когда оружие в двух руках.</summary>
[RegisterComponent]
public sealed partial class DHZSightComponent : Component
{
    [DataField]
    public int Depth = 1;
}

/// <summary>Камера взгляда на другой уровень (служебная сущность).</summary>
[RegisterComponent]
public sealed partial class DHZCameraComponent : Component
{
    [ViewVariables]
    public EntityUid Viewer;
}

/// <summary>Призрак (и админ-призрак): кнопки «на уровень выше/ниже» — сразу в ту же точку на другом Z-уровне.</summary>
[RegisterComponent]
public sealed partial class DHZGhostMoverComponent : Component
{
    [DataField]
    public EntProtoId UpAction = "DHActionGhostLevelUp";

    [DataField]
    public EntProtoId DownAction = "DHActionGhostLevelDown";

    [DataField]
    public EntityUid? UpActionEntity;

    [DataField]
    public EntityUid? DownActionEntity;
}

public sealed partial class DHGhostLevelUpActionEvent : InstantActionEvent;

public sealed partial class DHGhostLevelDownActionEvent : InstantActionEvent;

public sealed partial class DHLookUpActionEvent : InstantActionEvent;

public sealed partial class DHLookDownActionEvent : InstantActionEvent;

/// <summary>
/// Выстрел по точке на другом уровне той же стопки (стрелок смотрит туда — с крыши вниз или из проёма вверх):
/// пуля вылетает уже на уровне цели, над/под стрелком.
/// </summary>
[ByRefEvent]
public record struct DHCrossLevelShotEvent(Robust.Shared.Map.EntityCoordinates Origin,
    Robust.Shared.Map.EntityCoordinates Target);
