// Перенесено из RussianCM (CMU14, github.com/flex5hybrid/RussianCM, AGPL v3) — система следов FootPrints
// (изначально Einstein Engines): FootPrintsComponent, PuddleFootPrintsComponent.
using System.Numerics;
using Content.Shared.Decals;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.FootPrints;

/// <summary>
/// Оставляет следы, наступив в лужу (кровь, грязь): отпечатки шагов, а раненый в крите или мёртвый, которого тащат, —
/// размазанный след волочения. Решение автора 2026-11-11: кровавые следы, как в CMU.
/// </summary>
[RegisterComponent]
public sealed partial class DHFootPrintsComponent : Component
{
    [DataField]
    public List<ProtoId<DecalPrototype>> DraggingDecals = new()
    {
        "FootprintDragging1",
        "FootprintDragging2",
        "FootprintDragging3",
        "FootprintDragging4",
        "FootprintDragging5",
    };

    [DataField]
    public ProtoId<DecalPrototype> ShoesDecal = "FootprintShoes";

    [DataField]
    public ProtoId<DecalPrototype> LeftBareDecal = "FootprintBareLeft";

    [DataField]
    public ProtoId<DecalPrototype> RightBareDecal = "FootprintBareRight";

    /// <summary>Текущий цвет следов; альфа 0 — следов нет.</summary>
    [ViewVariables]
    public Color PrintsColor = Color.FromHex("#00000000");

    [DataField]
    public float StepSize = 0.7f;

    [DataField]
    public float DragSize = 0.5f;

    /// <summary>Насколько бледнеет каждый следующий отпечаток.</summary>
    [DataField]
    public float ColorReduceAlpha = 0.1f;

    [DataField]
    public Vector2 OffsetPrint = new(0.1f, 0f);

    [ViewVariables]
    public float ColorQuantity;

    public bool RightStep = true;

    public Vector2 StepPos = Vector2.Zero;

    public float ColorInterpolationFactor = 0.2f;
}

/// <summary>Лужа, в которой пачкаются ноги (и обувь — пятном).</summary>
[RegisterComponent]
public sealed partial class DHPuddleFootPrintsComponent : Component
{
    [DataField]
    public float SizeRatio = 0.2f;

    /// <summary>Если воды в луже больше этого процента — следов нет (просто мокро).</summary>
    [DataField]
    public float OffPercent = 80f;

    public HashSet<EntityUid> ActivatedEntities = new();
}
