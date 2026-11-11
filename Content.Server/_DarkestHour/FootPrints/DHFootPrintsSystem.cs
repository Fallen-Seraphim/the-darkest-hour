// Перенесено из RussianCM (CMU14, github.com/flex5hybrid/RussianCM, AGPL v3) — FootPrintsSystem и
// PuddleFootPrintsSystem (изначально Einstein Engines). Переписано под TDH: без сорняков ксено, обувь — свой след,
// пятно на обуви — DHItemStainSystem.
using System.Numerics;
using Content.Server.Decals;
using Content.Shared._DarkestHour.FootPrints;
using Content.Shared._DarkestHour.Stains;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Decals;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.FootPrints;

/// <summary>
/// Кровавые следы (решение автора 2026-11-11, как в CMU): наступил в лужу — несколько шагов оставляешь бледнеющие
/// отпечатки её цвета (в обуви — след подошвы, босиком — ступни); раненого в крите или мёртвого, которого тащат, —
/// размазанный след волочения. Обувь, наступившая в кровь, пачкается.
/// </summary>
public sealed partial class DHFootPrintsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private DHItemStainSystem _stains = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<ReagentPrototype> WaterReagent = "Water";
    private static readonly Vector2 DecalCenterOffset = new(-0.5f, -0.5f);
    private static readonly Angle DraggingRotationOffset = Angle.FromDegrees(-90f);
    private static readonly Angle StepRotationOffset = Angle.FromDegrees(180f);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHFootPrintsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DHFootPrintsComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<DHPuddleFootPrintsComponent, StartCollideEvent>(OnPuddleCollide);
        SubscribeLocalEvent<DHPuddleFootPrintsComponent, EndCollideEvent>(OnPuddleEndCollide);
    }

    private void OnStartup(Entity<DHFootPrintsComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.StepSize = Math.Max(0f, ent.Comp.StepSize + _random.NextFloat(-0.05f, 0.05f));
    }

    private void OnMove(Entity<DHFootPrintsComponent> ent, ref MoveEvent args)
    {
        var comp = ent.Comp;
        if (comp.PrintsColor.A <= 0f)
            return;

        var transform = args.Component;
        var state = TryComp<MobStateComponent>(ent, out var mobState) ? mobState.CurrentState : MobState.Alive;
        var dragging = state is MobState.Critical or MobState.Dead;
        var stepDelta = transform.LocalPosition - comp.StepPos;
        var stepSize = dragging ? comp.DragSize : comp.StepSize;
        if (stepDelta.LengthSquared() <= stepSize * stepSize)
            return;

        if (transform.GridUid is not { } gridUid || transform.ParentUid != gridUid)
        {
            comp.StepPos = transform.LocalPosition;
            return;
        }

        comp.RightStep = !comp.RightStep;

        if (dragging)
        {
            if (comp.DraggingDecals.Count > 0)
            {
                var at = new EntityCoordinates(gridUid, transform.LocalPosition);
                _decals.TryAddDecal(_random.Pick(comp.DraggingDecals), at.Offset(DecalCenterOffset), out _,
                    comp.PrintsColor, stepDelta.ToAngle() + DraggingRotationOffset, cleanable: true);
            }
        }
        else
        {
            var offset = comp.RightStep
                ? new Angle(StepRotationOffset + transform.LocalRotation).RotateVec(comp.OffsetPrint)
                : new Angle(transform.LocalRotation).RotateVec(comp.OffsetPrint);
            var at = new EntityCoordinates(gridUid, transform.LocalPosition + offset);
            var shod = _inventory.TryGetSlotEntity(ent, DHItemStainSystem.ShoesSlot, out _);
            var decal = shod ? comp.ShoesDecal : comp.RightStep ? comp.RightBareDecal : comp.LeftBareDecal;
            _decals.TryAddDecal(decal, at.Offset(DecalCenterOffset), out _, comp.PrintsColor,
                transform.LocalRotation + StepRotationOffset, cleanable: true);
        }

        var alpha = Math.Max(0f, comp.PrintsColor.A - comp.ColorReduceAlpha);
        comp.PrintsColor = comp.PrintsColor.WithAlpha(alpha);
        if (alpha <= 0f)
            comp.ColorQuantity = 0f;
        comp.StepPos = transform.LocalPosition;
    }

    private void OnPuddleCollide(Entity<DHPuddleFootPrintsComponent> ent, ref StartCollideEvent args)
    {
        if (!args.OtherFixture.Hard || args.OtherBody.BodyStatus == BodyStatus.InAir ||
            _gravity.IsWeightless(args.OtherEntity))
        {
            return;
        }

        if (!TryComp<AppearanceComponent>(ent, out var appearance) ||
            !TryComp<PuddleComponent>(ent, out var puddle) ||
            !TryComp<DHFootPrintsComponent>(args.OtherEntity, out var walker) ||
            !_solutions.ResolveSolution(ent.Owner, puddle.SolutionName, ref puddle.Solution, out var solution) ||
            solution.Contents.Count == 0)
        {
            return;
        }

        UpdateShoeStain(args.OtherEntity, solution);

        var total = 0f;
        var water = 0f;
        foreach (var reagent in solution.Contents)
        {
            var quantity = (float) reagent.Quantity;
            total += quantity;
            if (reagent.Reagent.Prototype == WaterReagent)
                water += quantity;
        }

        if (total <= 0f || water > total * ent.Comp.OffPercent / 100f || !ent.Comp.ActivatedEntities.Add(args.OtherEntity))
            return;

        if (_appearance.TryGetData(ent, PuddleVisuals.SolutionColor, out var color, appearance) &&
            _appearance.TryGetData(ent, PuddleVisuals.CurrentVolume, out var volume, appearance))
        {
            var col = (Color) color;
            walker.PrintsColor = walker.ColorQuantity == 0f
                ? col
                : Color.InterpolateBetween(walker.PrintsColor, col, walker.ColorInterpolationFactor);
            walker.ColorQuantity += (float) volume * ent.Comp.SizeRatio;
        }

        if (puddle.Solution is { } soln)
            _solutions.RemoveEachReagent(soln, 1);
    }

    /// <summary>Сошёл с лужи — снова наступив в неё, опять испачкается.</summary>
    private void OnPuddleEndCollide(Entity<DHPuddleFootPrintsComponent> ent, ref EndCollideEvent args)
    {
        ent.Comp.ActivatedEntities.Remove(args.OtherEntity);
    }

    /// <summary>Обувь, наступившая в лужу: самый объёмный реагент пачкает её (кровь) или отмывает (вода).</summary>
    private void UpdateShoeStain(EntityUid wearer, Solution solution)
    {
        if (!_inventory.TryGetSlotEntity(wearer, DHItemStainSystem.ShoesSlot, out var shoes))
            return;

        ReagentPrototype? dominant = null;
        var largest = 0f;
        foreach (var quantity in solution.Contents)
        {
            if ((float) quantity.Quantity <= largest || !_proto.TryIndex<ReagentPrototype>(quantity.Reagent.Prototype, out var candidate))
                continue;

            dominant = candidate;
            largest = (float) quantity.Quantity;
        }

        if (dominant == null)
            return;

        if (dominant.CleansItemStains)
            _stains.TryClean(shoes.Value);
        else if (dominant.ItemStain is { } kind)
            _stains.TryStain(shoes.Value, kind, dominant.ItemStainColor ?? dominant.SubstanceColor);
    }
}
