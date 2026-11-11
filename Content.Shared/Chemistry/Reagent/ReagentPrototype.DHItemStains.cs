// Перенесено из RussianCM (CMU14, github.com/flex5hybrid/RussianCM, AGPL v3): ReagentPrototype.ItemStains.cs.
// ReSharper disable CheckNamespace

using Content.Shared._DarkestHour.Stains;

namespace Content.Shared.Chemistry.Reagent;

public sealed partial class ReagentPrototype
{
    /// <summary>DarkestHour: пятно на предмете или снаряжении при касании этим реагентом (кровь, масло).</summary>
    [DataField]
    public DHItemStainKind? ItemStain;

    /// <summary>DarkestHour: цвет пятна; по умолчанию — <see cref="SubstanceColor"/>.</summary>
    [DataField]
    public Color? ItemStainColor;

    /// <summary>DarkestHour: касание этим реагентом смывает пятна (вода, моющее средство).</summary>
    [DataField]
    public bool CleansItemStains;
}
