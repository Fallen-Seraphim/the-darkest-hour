using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Levels;

// Z-уровни: у SS14 одна карта — один слой, поэтому этаж — отдельная карта с теми же координатами, что у земли.
// Карты одного места (земля, этажи, подвалы) — одна «стопка» (Stack). Уровень 0 — земля, +1..+8 — этажи и крыши
// (крыша 7-этажного дома — +8), −1..−3 — подземелье (канализация, бункеры). Переходы — универсальные лестницы,
// люки и лифты: ведут в ту же точку XY на соседнем уровне.

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
/// Уровень воздуха над полем боя (решение автора 2026-11-13): пустая карта стопки, на которой штурмовик идёт
/// бреющим на своей высоте. Отсюда видно всю стопку до земли, а здания сверху закрыты крышами.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHZAirLevelComponent : Component;

/// <summary>
/// Универсальная лестница (решение автора 2026-11-27, вместо лестничных маршей и проёмов; спрайты RMC-14 через RuCM):
/// ставится на каждом уровне в одной точке. Лестница ведёт туда, где в той же точке на соседнем уровне стоит другая
/// такая же: вверх, вниз или в обе стороны. Клик — подняться (если выше ничего — спуститься), в альтернативных
/// действиях — подняться, спуститься, посмотреть вверх или вниз. Спрайт выбирается сам по соединениям.
/// </summary>
[RegisterComponent]
public sealed partial class DHZLadderComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);
}

[Serializable, NetSerializable]
public enum DHZLadderVisuals : byte
{
    Connections,
}

[Serializable, NetSerializable]
public enum DHZLadderConnections : byte
{
    None,
    Up,
    Down,
    Both,
}

[Serializable, NetSerializable]
public sealed partial class DHZLadderDoAfterEvent : DoAfterEvent
{
    /// <summary>+1 — подняться, −1 — спуститься.</summary>
    [DataField]
    public int Step;

    private DHZLadderDoAfterEvent()
    {
    }

    public DHZLadderDoAfterEvent(int step)
    {
        Step = step;
    }

    public override DoAfterEvent Clone() => this;
}

/// <summary>
/// Боец, который умеет смотреть на соседние уровни (кнопки «Посмотреть вверх/вниз», как в Monolith — переключатель,
/// решение автора 2026-11-27): вверх — под открытым небом или у лестницы и шахты лифта, вниз — у края, лестницы,
/// люка или шахты. Взгляд идёт следом за бойцом, пока условие держится; повторное нажатие — перестать смотреть
/// (с прицелом x8 в двух руках — сначала глубже, на 3 уровня, x16 — на все).
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
