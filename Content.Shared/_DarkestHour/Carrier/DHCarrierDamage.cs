using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Carrier;

// Живучесть авианосца (план осады п. 19, решения автора 2026-11-19). Попадание ПКО (и ПВО) бьёт не только по
// общей прочности: в отсеке, куда попали, появляется пробоина (заваривается сваркой с листом стали), часто — пожар.
// Пока в отсеке есть пробоина, он повреждён: его машины и консоли обесточены, плюс особое последствие отсека.
// Незалатанные пробоины и непотушенные пожары понемногу съедают прочность корабля. Код — DHCarrierDamageSystem.

[Serializable, NetSerializable]
public enum DHCarrierCompartmentKind : byte
{
    /// <summary>Только пробоины и прочность, без особых последствий.</summary>
    None,

    /// <summary>Ангар и палуба: челноки не вылетают, штурмовик не взлетает, грузовой лифт стоит.</summary>
    Hangar,

    /// <summary>Орудийные палубы ОБ: консоли орудий обесточены.</summary>
    Guns,

    /// <summary>Мостик и штаб: консоли мостика обесточены.</summary>
    Bridge,

    /// <summary>Энергетика и реактор: по кораблю гаснет свет (аварийный остаётся), утечки радиации.</summary>
    Engineering,
}

/// <summary>Разметка отсеков корабля (генерирует скрипт сборки карты).</summary>
[Prototype("dhCarrierLayout")]
public sealed partial class DHCarrierLayoutPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<DHCarrierCompartment> Compartments = new();
}

[DataDefinition]
public sealed partial class DHCarrierCompartment
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public DHCarrierCompartmentKind Kind = DHCarrierCompartmentKind.None;

    /// <summary>Клетки грида отсека прямоугольниками (left, bottom, right, top).</summary>
    [DataField(required: true)]
    public List<Box2> Boxes = new();
}

/// <summary>На гриде авианосца: по какой разметке делятся отсеки.</summary>
[RegisterComponent]
public sealed partial class DHCarrierCompartmentsComponent : Component
{
    [DataField(required: true)]
    public ProtoId<DHCarrierLayoutPrototype> Layout;
}

/// <summary>Пробоина в отсеке авианосца: заваривается сваркой с листом стали в руках.</summary>
[RegisterComponent]
public sealed partial class DHCarrierBreachComponent : Component
{
    [ViewVariables]
    public EntityUid Grid;

    [ViewVariables]
    public string Compartment = string.Empty;

    [DataField]
    public float WeldTime = 10f;

    [DataField]
    public float WeldFuel = 5f;

    /// <summary>Доля воздуха клетки, что уходит наружу за секунду.</summary>
    [DataField]
    public float VentFraction = 0.2f;
}

[Serializable, NetSerializable]
public sealed partial class DHCarrierBreachWeldDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Схема повреждений: отсеки, пробоины, пожары и прочность корабля, на котором стоит консоль.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHCarrierDamageConsoleComponent : Component;

[Serializable, NetSerializable]
public enum DHCarrierDamageUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHCarrierCompartmentRow(string name, DHCarrierCompartmentKind kind, int breaches, int fires)
{
    public readonly string Name = name;
    public readonly DHCarrierCompartmentKind Kind = kind;
    public readonly int Breaches = breaches;
    public readonly int Fires = fires;
}

[Serializable, NetSerializable]
public sealed class DHCarrierDamageState(float hull, float maxHull, List<DHCarrierCompartmentRow> rows)
    : BoundUserInterfaceState
{
    public readonly float Hull = hull;
    public readonly float MaxHull = maxHull;
    public readonly List<DHCarrierCompartmentRow> Rows = rows;
}
