using Content.Server._DarkestHour.Medical.Trauma;
using Content.Shared._DarkestHour.Clothing.ModularArmor;
using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Clothing.ModularArmor;

/// <summary>
/// Модульная броня на сервере: защита той части тела, куда пришёлся удар (часть выбирают травмы до брони), износ и
/// поломка модулей, броня шлема только для головы, автоаптечка.
/// </summary>
public sealed partial class DHModularArmorDamageSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DHModularArmorSystem _armor = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DHTraumaSystem _trauma = default!;

    private static readonly SoundSpecifier BreakSound = new SoundCollectionSpecifier("MetalBreak");
    private static readonly SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");

    private TimeSpan _nextAutoMed;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHModularArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnSuitDamageModify);
        SubscribeLocalEvent<DHPartArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnPartArmorDamageModify);
    }

    /// <summary>Часть тела этого удара: выбирается один раз (первым, кто спросит) и запоминается травмами.</summary>
    private string HitPart(EntityUid target, EntityUid? origin)
    {
        return _trauma.PickHitPart(target, origin);
    }

    private void OnSuitDamageModify(Entity<DHModularArmorComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        var target = args.Owner;
        var part = PeekOrPick(target, args.Args.Origin);
        if (DHModularArmor.SlotFor(part) is not { } slot ||
            _armor.GetModule(ent, slot) is not { } module ||
            !TryComp<DHArmorModuleComponent>(module, out var comp) ||
            comp.Broken)
        {
            return;
        }

        var before = args.Args.Damage.GetTotal().Float();
        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, comp.Modifiers);
        var absorbed = before - args.Args.Damage.GetTotal().Float();
        if (absorbed <= 0f)
            return;

        comp.Integrity = MathF.Max(0f, comp.Integrity - absorbed);
        Dirty(module, comp);

        if (comp.Broken)
        {
            _audio.PlayPvs(BreakSound, target);
            _popup.PopupEntity(Loc.GetString("dh-armor-module-broken", ("module", Name(module))), target, target,
                PopupType.LargeCaution);
        }
    }

    private void OnPartArmorDamageModify(Entity<DHPartArmorComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        var part = PeekOrPick(args.Owner, args.Args.Origin);
        if (ent.Comp.Parts.Contains(part))
            args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, ent.Comp.Modifiers);
    }

    /// <summary>
    /// Шлем и корпус спрашивают часть тела в одном и том же ударе: выбирается один раз за тик.
    /// </summary>
    private string PeekOrPick(EntityUid target, EntityUid? origin)
    {
        if (TryComp<DHTraumaComponent>(target, out var trauma) &&
            trauma.PendingHitPart != null && trauma.PendingHitTick == _timing.CurTick)
        {
            return trauma.PendingHitPart;
        }

        return HitPart(target, origin);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextAutoMed)
            return;

        _nextAutoMed = now + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<DHArmorSpecialComponent>();
        while (query.MoveNext(out var module, out var special))
        {
            if (special.Kind != DHArmorSpecialKind.AutoMed || now < special.NextUse)
                continue;

            // модуль в слоте корпуса, корпус надет
            if (!TryGetSuitOf(module, out var suit) || _armor.GetWearer(suit) is not { } wearer ||
                !TryComp<DamageableComponent>(wearer, out var damageable) ||
                _damageable.GetTotalDamage((wearer, damageable)).Float() < special.Threshold)
            {
                continue;
            }

            if (!_solutions.TryGetSolution(module, special.Solution, out var solutionEnt, out var solution) ||
                solution.Volume <= 0)
            {
                continue;
            }

            var dose = _solutions.SplitSolution(solutionEnt.Value, FixedPoint2.New(special.Dose));
            if (!_bloodstream.TryAddToBloodstream(wearer, dose))
                continue;

            special.NextUse = now + special.Cooldown;
            Dirty(module, special);
            _audio.PlayPvs(InjectSound, wearer);
            _popup.PopupEntity(Loc.GetString("dh-armor-automed-injected"), wearer, wearer, PopupType.Medium);
        }
    }

    private bool TryGetSuitOf(EntityUid module, out EntityUid suit)
    {
        suit = default;
        var parent = Transform(module).ParentUid;
        if (!HasComp<DHModularArmorComponent>(parent) || _armor.GetModule(parent, DHArmorSlot.Special) != module)
            return false;

        suit = parent;
        return true;
    }
}
