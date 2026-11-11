using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Chemistry;

/// <summary>
/// Эффект реакции: часть продукта улетает ядовитым облаком (SO₂, NO₂, хлороводород…). Облако — обычный дым
/// с этим реагентом: вдыхают все рядом без маски. Размер облака растёт с количеством прореагировавшего.
/// Этап 4 переработки медицины и химии (claude/chemistry-rework.md).
/// </summary>
/// <inheritdoc cref="EntityEffect"/>
public sealed partial class DHToxicFumes : EntityEffectBase<DHToxicFumes>
{
    /// <summary>Реагент в облаке.</summary>
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    /// <summary>Сколько единиц реагента уходит в облако на единицу масштаба реакции.</summary>
    [DataField]
    public float Ratio = 0.5f;

    /// <summary>Облако появляется, только если прореагировало не меньше этого.</summary>
    [DataField]
    public float MinReacted = 2f;

    [DataField]
    public float Duration = 8f;

    /// <summary>Сколько единиц масштаба на одну клетку облака.</summary>
    [DataField]
    public float OverflowThreshold = 5f;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/smoke.ogg");

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        prototype.TryIndex(Reagent, out var reagent)
            ? Loc.GetString("dh-entity-effect-guidebook-toxic-fumes", ("reagent", reagent.LocalizedName))
            : string.Empty;

    public override LogImpact? Impact => LogImpact.Medium;
}
