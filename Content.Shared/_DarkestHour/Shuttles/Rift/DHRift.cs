using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Shuttles.Rift;

// Разрывные прыжки (этап 3) — замена блюспейса. Движок перемещения — ванильный FTL; поверх него: прыжок в другую
// звёздную систему требует разрывного генератора и накопителя (катушка / конденсатор / суперконденсатор) с зарядом
// хронита, долго заряжается, после прыжка остывает; при прыжке — хронитовая вспышка (молнии, радиация, хронит),
// накопитель изнашивается, изношенный может дать сбой (промах по точке выхода). В пределах своей системы (станция,
// планеты, экспедиции) корабли летают как раньше, без разрыва.

/// <summary>
/// Звёздная система: при старте раунда для каждой создаётся своя карта (пустой космос), куда можно прыгнуть только
/// через разрыв.
/// </summary>
[Prototype("dhStarSystem")]
public sealed partial class DHStarSystemPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Домашняя система станции: своей карты не получает, привязывается к карте станции (см. DHCelestialSystem).
    /// </summary>
    [DataField]
    public bool Home;

    /// <summary>Небесные тела системы: видны на сканере масс и навигационных консолях, опасны вблизи.</summary>
    [DataField]
    public List<DHCelestialBody> Bodies = new();
}

/// <summary>
/// Карта принадлежит звёздной системе. Карты без компонента — домашняя система станции (планеты, экспедиции).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHStarSystemMapComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<DHStarSystemPrototype>? System;

    /// <summary>Точка карты, от которой отсчитываются координаты тел системы.</summary>
    [DataField, AutoNetworkedField]
    public Vector2 Origin;
}

/// <summary>
/// Разрывной генератор: без него (и без накопителя) корабль не уходит в другую систему. Нужно питание.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHRiftGeneratorComponent : Component
{
    /// <summary>Зарядка разрыва перед прыжком (вместо обычных секунд FTL).</summary>
    [DataField]
    public TimeSpan ChargeTime = TimeSpan.FromSeconds(30);

    /// <summary>Остывание после прыжка.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(5);

    [DataField]
    public TimeSpan NextJump;

    /// <summary>Радиус молний хронитовой вспышки вокруг генератора.</summary>
    [DataField]
    public float FlashRange = 7f;
}

/// <summary>
/// Накопитель разрыва (катушка, конденсатор, суперконденсатор): одно устройство разного типоразмера. Определяет,
/// какую массу корабля можно перебросить; тратит хронит на прыжок и изнашивается.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHRiftAccumulatorComponent : Component
{
    /// <summary>Максимальная масса корабля (физическая масса грида).</summary>
    [DataField]
    public float MaxMass = 3000f;

    [DataField, AutoNetworkedField]
    public float Chronite;

    [DataField]
    public float ChroniteMax = 100f;

    [DataField]
    public float ChronitePerJump = 25f;

    /// <summary>Износ 0…1. Растёт с каждым прыжком; выше половины — риск сбоя и вспышка сильнее.</summary>
    [DataField, AutoNetworkedField]
    public float Wear;

    [DataField]
    public float WearPerJump = 0.1f;

    /// <summary>Сила хронитовой вспышки (больше хронита — сильнее).</summary>
    [DataField]
    public float FlashPower = 1f;
}

/// <summary>
/// Хронитовая ячейка: вставить в накопитель — пополняет заряд хронита.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHChroniteCellComponent : Component
{
    [DataField]
    public float Amount = 25f;
}

/// <summary>
/// Корабль готовится к разрывному прыжку (проверка пройдена, идёт зарядка FTL).
/// </summary>
[RegisterComponent]
public sealed partial class DHRiftJumpPendingComponent : Component
{
    [ViewVariables]
    public EntityUid Generator;

    [ViewVariables]
    public EntityUid Accumulator;
}

/// <summary>
/// Консоль шаттла запрашивает FTL на карту <see cref="TargetMap"/>. Отмена — прыжок не начнётся.
/// Поднимается из ванильного ShuttleConsoleSystem.ConsoleFTL (правка с пометкой DarkestHour).
/// </summary>
[ByRefEvent]
public record struct DHConsoleFTLTargetEvent(EntityUid Shuttle, MapId TargetMap, EntityUid Console, bool Cancelled = false);

[Serializable, NetSerializable]
public sealed partial class DHRiftRepairDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Корабль идёт сквозь разрыв (своя карта «разрывного пространства» на время прыжка): клиенту — тень по краям
/// экрана и вспышки молний для всех на борту.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHRiftTransitComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Since;

    /// <summary>Следующий разряд молний вокруг корпуса (только сервер).</summary>
    [ViewVariables]
    public TimeSpan NextThunder;
}

/// <summary>Разряд молний у корпуса: вспышка экрана у тех, кто на борту.</summary>
[Serializable, NetSerializable]
public sealed class DHRiftThunderEvent : EntityEventArgs
{
    public float Strength;

    public DHRiftThunderEvent(float strength)
    {
        Strength = strength;
    }
}
