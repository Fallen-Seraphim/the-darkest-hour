using Content.Shared._DarkestHour.Atmos.Fire;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Atmos.Fire;

/// <summary>
/// Реагент гасит огонь DarkestHour на тайле (вода из огнетушителя, пролитая вода).
/// Горючие реагенты (есть dhFlammableReagent) огонь не гасят.
/// </summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class DHExtinguishTileFireReaction : ITileReaction
{
    /// <summary>Множитель «силы тушения» на единицу реагента.</summary>
    [DataField]
    public float Multiplier = 1f;

    public FixedPoint2 TileReact(TileRef tile,
        ReagentPrototype reagent,
        FixedPoint2 reactVolume,
        IEntityManager entityManager,
        List<ReagentData>? data)
    {
        if (reactVolume <= FixedPoint2.Zero || tile.Tile.IsEmpty)
            return FixedPoint2.Zero;

        if (IoCManager.Resolve<IPrototypeManager>().HasIndex<DHFlammableReagentPrototype>(reagent.ID))
            return FixedPoint2.Zero;

        entityManager.System<DHFireSystem>()
            .ExtinguishTile(tile.GridUid, tile.GridIndices, reactVolume.Float() * Multiplier);

        // Реагент не тратится — остальные реакции (охлаждение, лужа) идут как обычно.
        return FixedPoint2.Zero;
    }
}
