using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Research.Prototypes;

// Опытные образцы (этап 4): рецепты свежеизученной технологии сначала печатаются как опытные образцы — оружие
// клинит, инструменты дают сбой, машины на опытной плате иногда отключаются. Серийная печать — после пробного периода
// или после того, как испытанный образец (с наработкой или отказом) отсканирован в химико-материальном сканере.

/// <summary>Опытный образец, напечатанный по рецепту в пробный период.</summary>
[RegisterComponent]
public sealed partial class DHPrototypeItemComponent : Component
{
    [DataField]
    public ProtoId<LatheRecipePrototype>? Recipe;

    /// <summary>Шанс отказа на одно использование (выстрел, применение инструмента, проверка машины).</summary>
    [DataField]
    public float FailChance = 0.04f;

    /// <summary>Оружие заклинило — до передёргивания (использовать в руке) не стреляет.</summary>
    [DataField]
    public bool Jammed;

    /// <summary>Наработка: сколько раз образец использовали.</summary>
    [DataField]
    public int Uses;

    [DataField]
    public int Failures;

    /// <summary>Опытная плата в машине: следующая проверка на сбой.</summary>
    [ViewVariables]
    public TimeSpan NextMachineCheck;
}

/// <summary>
/// На сервере РнД: до какого времени рецепты изученных в раунде технологий печатаются как опытные образцы.
/// </summary>
[RegisterComponent]
public sealed partial class DHRecipeMaturityComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<LatheRecipePrototype>, TimeSpan> MatureAt = new();

    [DataField]
    public TimeSpan TrialPeriod = TimeSpan.FromMinutes(20);
}

/// <summary>
/// Лат напечатал предмет по рецепту. Поднимается на лате из ванильного LatheSystem.FinishProducing (правка DarkestHour).
/// </summary>
[ByRefEvent]
public readonly record struct DHLatheProducedEvent(ProtoId<LatheRecipePrototype> Recipe, EntityUid Result);
