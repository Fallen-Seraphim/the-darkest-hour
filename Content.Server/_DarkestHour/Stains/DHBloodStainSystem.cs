// Перенесено из RussianCM (CMU14, github.com/flex5hybrid/RussianCM, AGPL v3): CMUItemStainDamageSystem,
// CMUItemStainReactionSystem, CMUItemStainCleaningSystem. Переписано под TDH: часть тела — из травм (DHPartHitEvent).
using Content.Server._DarkestHour.Medical.Trauma;
using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared._DarkestHour.Stains;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Stains;

/// <summary>
/// Кровь на снаряжении (решение автора 2026-11-11, как в CMU):
/// <list type="bullet">
/// <item>удар (физический урон) с шансом 25 % + 2 % за единицу урона пачкает кровью оружие ближнего боя атакующего,
/// а с шансом 1/3 — снаряжение на поражённой части тела (голова — шлем и маска, торс/руки/ноги — бронежилет или форма,
/// кисть — перчатки, стопа — обувь);</item>
/// <item>касание реагентом с пятном (кровь) пачкает предмет или открытое снаряжение; моющий реагент (вода, мыло,
/// моющее средство) — отмывает;</item>
/// <item>чистка тряпкой/мылом (криминалистическая очистка) отмывает пятна.</item>
/// </list>
/// </summary>
public sealed partial class DHBloodStainSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DHItemStainSystem _stains = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float MeleeRange = 1.8f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHTraumaComponent, DHPartHitEvent>(OnPartHit);
        SubscribeLocalEvent<ItemComponent, ReactionEntityEvent>(OnItemReaction);
        SubscribeLocalEvent<InventoryComponent, ReactionEntityEvent>(OnWearerReaction);
        SubscribeLocalEvent<CleansForensicsComponent, CleanForensicsDoAfterEvent>(OnCleanDoAfter);
    }

    private void OnPartHit(Entity<DHTraumaComponent> ent, ref DHPartHitEvent args)
    {
        if (args.Brute <= 0f || !TryGetBloodColor(ent, out var color))
            return;

        if (!_random.Prob(Math.Clamp((25f + 2f * args.Brute) / 100f, 0f, 1f)))
            return;

        // Оружие ближнего боя в руке атакующего рядом.
        if (args.Origin is { } attacker && attacker != ent.Owner && Exists(attacker) &&
            _transform.InRange(Transform(attacker).Coordinates, Transform(ent).Coordinates, MeleeRange) &&
            _hands.GetActiveItem(attacker) is { } weapon)
        {
            _stains.TryStain(weapon, DHItemStainKind.Blood, color);
        }

        if (!_random.Prob(0.33f))
            return;

        switch (args.Part)
        {
            case "Head":
                _stains.TryStainSlot(ent, DHItemStainSystem.HeadSlot, DHItemStainKind.Blood, color);
                _stains.TryStainSlot(ent, DHItemStainSystem.MaskSlot, DHItemStainKind.Blood, color);
                break;
            case "HandLeft":
            case "HandRight":
                _stains.TryStainSlot(ent, DHItemStainSystem.GlovesSlot, DHItemStainKind.Blood, color);
                break;
            case "FootLeft":
            case "FootRight":
                _stains.TryStainSlot(ent, DHItemStainSystem.ShoesSlot, DHItemStainKind.Blood, color);
                break;
            default:
                _stains.TryStainOuterBody(ent, DHItemStainKind.Blood, color);
                break;
        }
    }

    private bool TryGetBloodColor(EntityUid uid, out Color color)
    {
        color = default;
        if (!TryComp<BloodstreamComponent>(uid, out var blood) || blood.BloodReferenceSolution.Contents.Count == 0)
            return false;

        var reagent = blood.BloodReferenceSolution.Contents[0].Reagent.Prototype;
        if (!_proto.TryIndex<ReagentPrototype>(reagent, out var proto))
            return false;

        color = proto.ItemStainColor ?? proto.SubstanceColor;
        return true;
    }

    private void OnItemReaction(Entity<ItemComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method != ReactionMethod.Touch)
            return;

        if (args.Reagent.CleansItemStains)
            _stains.TryClean(ent);
        else if (args.Reagent.ItemStain is { } kind)
            _stains.TryStain(ent, kind, args.Reagent.ItemStainColor ?? args.Reagent.SubstanceColor);
    }

    private void OnWearerReaction(Entity<InventoryComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method != ReactionMethod.Touch)
            return;

        if (args.Reagent.CleansItemStains)
            _stains.CleanExposedEquipment(ent);
        else if (args.Reagent.ItemStain is { } kind)
            _stains.StainExposedEquipment(ent, kind, args.Reagent.ItemStainColor ?? args.Reagent.SubstanceColor);
    }

    private void OnCleanDoAfter(Entity<CleansForensicsComponent> ent, ref CleanForensicsDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } target)
            return;

        _stains.TryClean(target);
        if (HasComp<InventoryComponent>(target))
            _stains.CleanExposedEquipment(target);
    }
}
