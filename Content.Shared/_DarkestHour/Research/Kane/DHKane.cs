using Content.Shared.Examine;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Research.Kane;

// Кристаллы Кейна (этап 4): запасают энергию, ёмкость растёт с плотностью. Осколок (руда) или синтетическая затравка
// растёт в кристаллизаторе — чем дольше, тем плотнее (с убывающей отдачей). Литограф: кристалл → плата Кейна →
// микроплата Кейна, плотность немного теряется на каждом шаге. Микроплата + корпус ячейки — ячейка Кейна, её ёмкость
// зависит от плотности. Дальше — большие батареи, энергооружие, топливные кассеты.

/// <summary>
/// Плотность кристалла Кейна (0…100). Есть у осколков, кристаллов, плат и микроплат.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHKaneDensityComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Density;

    /// <summary>Случайная плотность при появлении (осколки из руды); 0 — не задавать.</summary>
    [DataField]
    public float RandomMin;

    [DataField]
    public float RandomMax;
}

/// <summary>
/// Затравка для кристаллизатора (осколок или синтетическая затравка): в кристаллизаторе превращается в кристалл.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHKaneSeedComponent : Component
{
    [DataField]
    public EntProtoId Crystal = "DHKaneCrystal";
}

/// <summary>
/// Кристаллизатор: выращивает кристалл Кейна из затравки, пока есть питание.
/// Плотность = затравка + (максимум − затравка) × (1 − e^(−t/τ)).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHKaneCrystallizerComponent : Component
{
    [DataField]
    public string SlotId = "crystal";

    [DataField]
    public float MaxDensity = 100f;

    /// <summary>Время роста до ~63% от остатка до максимума.</summary>
    [DataField]
    public float GrowthTime = 300f;

    [ViewVariables]
    public float SeedDensity;

    [ViewVariables]
    public float Grown;

    [ViewVariables]
    public bool PendingSwap;
}

/// <summary>
/// Литограф: кристалл → плата → микроплата (по <see cref="DHKaneProductComponent"/>).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHKaneLithographComponent : Component
{
    [DataField]
    public string SlotId = "input";

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    [ViewVariables]
    public TimeSpan? End;
}

/// <summary>
/// Что делает из предмета литограф и какая доля плотности сохраняется.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHKaneProductComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Next;

    [DataField]
    public float Retain = 0.95f;
}

/// <summary>
/// Корпус ячейки Кейна: применить микроплату — получится заряженная ячейка, ёмкость по плотности.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHKaneCasingComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Result;

    [DataField]
    public float BaseCharge = 1500f;

    [DataField]
    public float ChargePerDensity = 80f;
}

/// <summary>
/// Нужна ли для корпуса именно микроплата (а не кристалл или плата).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHKaneMicroboardComponent : Component;

[Serializable, NetSerializable]
public enum DHKaneMachineVisuals : byte
{
    Running,
}

/// <summary>
/// Осмотр: плотность кристалла.
/// </summary>
public sealed partial class DHKaneExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHKaneDensityComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<DHKaneDensityComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("dh-kane-examine-density", ("density", (int) MathF.Round(ent.Comp.Density))));
    }
}
