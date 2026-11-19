using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Battlefield;

// Разрушаемость поля боя «Осады» (план осады п. 7, решения автора 2026-11-19): объекты POI выходят из строя и
// чинятся, мосты рушатся в воду и наводятся понтонами, остаются воронки, трава горит, крыши ломаются по материалу,
// после обрушения — завалы (под ними можно оказаться). Код — Content.Server/_DarkestHour/Battlefield.

/// <summary>
/// Объект POI (турбина, энергоблок, орудие ПКО, шахта ПВО): от урона <see cref="BreakDamage"/> выходит из строя и
/// не работает, пока его не починят сваркой с материалом в руке — независимо от того, чья точка.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHPoiObjectComponent : Component
{
    [DataField]
    public float BreakDamage = 300f;

    [DataField, AutoNetworkedField]
    public bool Broken;

    /// <summary>
    /// Зона, которую объект выключает целиком, пока сломан (энергоблоки АЭС — сеть по карте). Пусто — ломается только
    /// он сам.
    /// </summary>
    [DataField]
    public string? Point;

    /// <summary>При первой поломке вокруг остаётся радиационное заражение до конца раунда (энергоблок АЭС).</summary>
    [DataField]
    public EntProtoId? Contamination;

    [ViewVariables]
    public bool Contaminated;

    [DataField]
    public float RepairTime = 30f;

    [DataField]
    public float RepairFuel = 10f;

    /// <summary>Тип стака материала для ремонта (листы стали, пластали).</summary>
    [DataField]
    public string RepairStack = "Steel";

    [DataField]
    public int RepairAmount = 5;
}

[Serializable, NetSerializable]
public sealed partial class DHPoiRepairDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Клетка моста. Клетки одного пролёта (<see cref="Section"/>) рушатся вместе, когда хоть одна из них получит
/// <see cref="BreakDamage"/>: настил становится глубокой водой. Наводится понтонной секцией.
/// </summary>
[RegisterComponent]
public sealed partial class DHBridgeTileComponent : Component
{
    [DataField(required: true)]
    public string Section = string.Empty;

    [DataField]
    public float BreakDamage = 250f;

    /// <summary>Каким был настил (что вернуть при наведении).</summary>
    [DataField]
    public string Floor = "FloorReinforced";

    [ViewVariables]
    public bool Broken;
}

/// <summary>Понтонная секция: наводит обрушенный пролёт моста рядом.</summary>
[RegisterComponent]
public sealed partial class DHPontoonKitComponent : Component
{
    [DataField]
    public float Range = 3f;

    [DataField]
    public float DeployTime = 30f;
}

[Serializable, NetSerializable]
public sealed partial class DHPontoonDeployDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Воронка: замедляет технику (пехоту — контактами), даёт укрытие как окоп.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHCraterComponent : Component
{
    /// <summary>Множитель скорости машины, у которой воронка под центром.</summary>
    [DataField]
    public float VehicleSpeed = 0.5f;
}

/// <summary>Высокая трава, которая горит: загорается от огня рядом и сгорает (укрытия больше нет).</summary>
[RegisterComponent]
public sealed partial class DHFlammableGrassComponent : Component
{
    /// <summary>Шанс за секунду загореться от соседнего огня.</summary>
    [DataField]
    public float CatchChance = 0.35f;
}

/// <summary>Завал после обрушения крыши: препятствие и укрытие, разбирается лопатой. Под ним может оказаться человек.</summary>
[RegisterComponent]
public sealed partial class DHRubbleComponent : Component
{
    [DataField]
    public float ClearTime = 6f;
}

/// <summary>Боец под завалом: не может двигаться, пока завал не разберут (или сам не выберется — долго).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHBuriedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Rubble;

    /// <summary>Когда выберется сам.</summary>
    [ViewVariables]
    public TimeSpan FreeAt;
}

[Serializable, NetSerializable]
public sealed partial class DHRubbleClearDoAfterEvent : SimpleDoAfterEvent;
