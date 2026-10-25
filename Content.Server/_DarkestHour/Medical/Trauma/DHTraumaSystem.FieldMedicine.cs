using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server._DarkestHour.Medical.Trauma;

/// <summary>
/// Полевая медицина: жгуты и турникеты на конечности (останавливают кровотечение из них, больше 10 минут — отмирание),
/// полевые повязки (окклюзионная — на грудь).
/// </summary>
public sealed partial class DHTraumaSystem
{
    private static readonly string[] Limbs = { "ArmLeft", "ArmRight", "LegLeft", "LegRight" };

    /// <summary>Сколько жгут держится без вреда.</summary>
    private static readonly TimeSpan TourniquetSafe = TimeSpan.FromMinutes(10);

    private void InitializeFieldMedicine()
    {
        SubscribeLocalEvent<DHTourniquetComponent, AfterInteractEvent>(OnTourniquetAfterInteract);
        SubscribeLocalEvent<DHTraumaComponent, DHTourniquetDoAfterEvent>(OnTourniquetDoAfter);
        SubscribeLocalEvent<DHFieldDressingComponent, AfterInteractEvent>(OnDressingAfterInteract);
        SubscribeLocalEvent<DHTraumaComponent, DHFieldDressingDoAfterEvent>(OnDressingDoAfter);
        SubscribeLocalEvent<DHTraumaComponent, GetVerbsEvent<AlternativeVerb>>(OnGetTourniquetVerbs);
    }

    #region Жгут

    /// <summary>Кисть и стопа пережаты жгутом на руке/ноге; сама рука/нога — своим жгутом.</summary>
    private static bool Compressed(DHTraumaComponent comp, string category)
    {
        if (comp.Parts.TryGetValue(category, out var part) && part.Tourniquet != null)
            return true;

        return DHTraumaConstants.ParentOf.TryGetValue(category, out var parent) &&
               parent is not "Torso" &&
               comp.Parts.TryGetValue(parent, out var parentPart) && parentPart.Tourniquet != null;
    }

    /// <summary>Конечность для жгута: выбранная зона (кисть/стопа — их рука/нога), иначе самая кровоточащая.</summary>
    private string? FindTourniquetTarget(EntityUid user, DHTraumaComponent trauma)
    {
        if (TryComp<DHTargetingComponent>(user, out var targeting))
        {
            var chosen = targeting.Target.Id;
            if (chosen is "HandLeft" or "HandRight" or "FootLeft" or "FootRight")
                chosen = DHTraumaConstants.ParentOf[chosen];

            if (Array.IndexOf(Limbs, chosen) >= 0 &&
                (!trauma.Parts.TryGetValue(chosen, out var part) || part.Tourniquet == null))
            {
                return chosen;
            }
        }

        string? best = null;
        var worst = -1f;
        foreach (var limb in Limbs)
        {
            if (trauma.Parts.TryGetValue(limb, out var part) && part.Tourniquet != null)
                continue;

            var bleeding = part?.InternalBleeding ?? 0f;
            var brute = part?.Brute ?? 0f;
            var score = bleeding * 100f + brute;
            if (score > worst)
            {
                worst = score;
                best = limb;
            }
        }

        return best;
    }

    private void OnTourniquetAfterInteract(Entity<DHTourniquetComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target ||
            !TryComp<DHTraumaComponent>(target, out var trauma))
        {
            return;
        }

        args.Handled = true;
        if (FindTourniquetTarget(args.User, trauma) == null)
        {
            _popup.PopupEntity(Loc.GetString("dh-tourniquet-no-limb"), target, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new DHTourniquetDoAfterEvent(),
            target, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnTourniquetDoAfter(Entity<DHTraumaComponent> ent, ref DHTourniquetDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } item ||
            !TryComp<DHTourniquetComponent>(item, out var tourniquet))
        {
            return;
        }

        args.Handled = true;
        if (FindTourniquetTarget(args.User, ent.Comp) is not { } limb)
            return;

        var part = GetPart(ent.Comp, limb);
        part.Tourniquet = MetaData(item).EntityPrototype?.ID;
        part.TourniquetSince = _timing.CurTime;
        ent.Comp.PainSpike += 15f;

        _bloodstream.TryModifyBleedAmount(ent.Owner, -tourniquet.BleedReduction);
        _popup.PopupEntity(Loc.GetString("dh-tourniquet-applied", ("user", args.User), ("target", ent.Owner),
            ("part", PartName(limb))), ent, PopupType.Small);
        QueueDel(item);
        UpdateSummary(ent);
    }

    private void OnGetTourniquetVerbs(Entity<DHTraumaComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        foreach (var limb in Limbs)
        {
            if (!ent.Comp.Parts.TryGetValue(limb, out var part) || part.Tourniquet == null)
                continue;

            var category = limb;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dh-tourniquet-remove-verb", ("part", PartName(category))),
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
                Act = () => RemoveTourniquet(ent, category, user),
            });
        }
    }

    private void RemoveTourniquet(Entity<DHTraumaComponent> ent, string limb, EntityUid user)
    {
        if (!ent.Comp.Parts.TryGetValue(limb, out var part) || part.Tourniquet is not { } proto)
            return;

        part.Tourniquet = null;
        var item = Spawn(proto, _transform.GetMapCoordinates(ent));
        _hands.PickupOrDrop(user, item);
        _popup.PopupEntity(Loc.GetString("dh-tourniquet-removed", ("user", user), ("target", ent.Owner),
            ("part", PartName(limb))), ent, PopupType.Small);
        UpdateSummary(ent);
    }

    /// <summary>Жгут дольше 10 минут: конечность отмирает — боль и урон части раз в 30 секунд.</summary>
    private void TickTourniquets(Entity<DHTraumaComponent> ent, TimeSpan now)
    {
        foreach (var limb in Limbs)
        {
            if (!ent.Comp.Parts.TryGetValue(limb, out var part) || part.Tourniquet == null)
                continue;

            var held = now - part.TourniquetSince;
            if (held < TourniquetSafe || (int) held.TotalSeconds % 30 != 0)
                continue;

            part.Brute += 4f;
            ent.Comp.PainSpike += 10f;
            _popup.PopupEntity(Loc.GetString("dh-tourniquet-necrosis", ("part", PartName(limb))), ent, ent,
                PopupType.SmallCaution);
        }
    }

    private float TourniquetPain(DHTraumaComponent comp, TimeSpan now)
    {
        var pain = 0f;
        foreach (var limb in Limbs)
        {
            if (comp.Parts.TryGetValue(limb, out var part) && part.Tourniquet != null)
                pain += now - part.TourniquetSince >= TourniquetSafe ? 25f : 10f;
        }

        return pain;
    }

    private void AddTourniquetFindings(DHPartTrauma part, List<string> findings)
    {
        if (part.Tourniquet == null)
            return;

        var minutes = (int) (_timing.CurTime - part.TourniquetSince).TotalMinutes;
        findings.Add(Loc.GetString(minutes >= TourniquetSafe.TotalMinutes
            ? "dh-tourniquet-desc-necrosis"
            : "dh-tourniquet-desc", ("minutes", minutes)));
    }

    private void ExamineTourniquets(Entity<DHTraumaComponent> ent, ExaminedEvent args)
    {
        foreach (var limb in Limbs)
        {
            if (!ent.Comp.Parts.TryGetValue(limb, out var part) || part.Tourniquet == null)
                continue;

            var minutes = (int) (_timing.CurTime - part.TourniquetSince).TotalMinutes;
            args.PushMarkup(Loc.GetString("dh-tourniquet-examine", ("target", ent.Owner), ("part", PartName(limb)),
                ("minutes", minutes)));
        }
    }

    #endregion

    #region Полевая повязка

    private void OnDressingAfterInteract(Entity<DHFieldDressingComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<DHTraumaComponent>(target))
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay,
            new DHFieldDressingDoAfterEvent(), target, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnDressingDoAfter(Entity<DHTraumaComponent> ent, ref DHFieldDressingDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } item ||
            !TryComp<DHFieldDressingComponent>(item, out var dressing))
        {
            return;
        }

        args.Handled = true;
        if (ent.Comp.Parts.TryGetValue(dressing.Part, out var part) && part.InternalBleeding > 0f)
            part.InternalBleeding *= dressing.InternalBleedingFactor;

        _bloodstream.TryModifyBleedAmount(ent.Owner, -dressing.BleedReduction);
        if (!dressing.Heal.Empty)
            _damageable.TryChangeDamage(ent.Owner, dressing.Heal, true, origin: args.User);

        _popup.PopupEntity(Loc.GetString("dh-field-dressing-applied", ("user", args.User), ("target", ent.Owner),
            ("item", item), ("part", PartName(dressing.Part))), ent, PopupType.Small);
        QueueDel(item);
        UpdateSummary(ent);
    }

    #endregion
}
