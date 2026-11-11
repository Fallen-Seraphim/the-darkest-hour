// ReSharper disable CheckNamespace

using Content.Shared.FixedPoint;

namespace Content.Shared.Chemistry.Reagent;

public sealed partial class ReagentPrototype
{
    /// <summary>
    /// DarkestHour: порог передозировки (единиц в крови). Выше него у пациента горит значок «Передозировка»
    /// (DHTraumaSystem). Сами последствия передозировки — в эффектах метаболизма реагента.
    /// </summary>
    [DataField("dhOverdose")]
    public FixedPoint2? DHOverdose;
}
