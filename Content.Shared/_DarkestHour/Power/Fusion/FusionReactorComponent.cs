namespace Content.Shared._DarkestHour.Power.Fusion;

/// <summary>
/// Компактный термоядерный реактор (упрощение по решению автора 2026-11-11): пока вставлена топливная кассета и реактор
/// включён, он сам отдаёт энергию в высоковольтную сеть — без газового контура, турбин и поля удержания. Мощность
/// задаётся оператором (0–100%) и плавно выходит на заданную; топливо горит пропорционально мощности.
/// </summary>
[RegisterComponent]
public sealed partial class FusionReactorComponent : Component
{
    /// <summary>Слот топливной кассеты.</summary>
    [DataField]
    public string FuelSlotId = "fuel_slot";

    /// <summary>Отдача в сеть на 100% мощности, Вт.</summary>
    [DataField]
    public float MaxPowerOutput = 500_000f;

    /// <summary>Как быстро отдача догоняет заданную мощность: доля от наибольшей в секунду.</summary>
    [DataField]
    public float RampRate = 0.1f;

    /// <summary>Расход топлива в секунду на 100% мощности (кассета на 100 — около получаса).</summary>
    [DataField]
    public float FuelBurnRate = 0.055f;

    /// <summary>Оператор включил реактор.</summary>
    [DataField]
    public bool Active;

    /// <summary>Заданная мощность, 0..1.</summary>
    [DataField]
    public float PowerSetting = 0.5f;

    /// <summary>Текущая отдача в сеть, Вт.</summary>
    [DataField]
    public float PowerOutput;

    [ViewVariables]
    public bool SlotLocked;

    [ViewVariables]
    public float Accumulator;
}
