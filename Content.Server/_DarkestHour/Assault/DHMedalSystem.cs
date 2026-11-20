using System.Linq;
using Content.Shared._DarkestHour.Assault;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Assault;

/// <summary>
/// Наградной лист командира (план осады п. 6, решение автора 2026-11-20): выбрать бойца своей стороны (живого или
/// павшего), медаль своей стороны и формулировку. Награда объявляется стороне и попадает в итоги раунда; не больше
/// <see cref="DHMedalTabletComponent.MaxAwards"/> с одного листа.
/// </summary>
public sealed partial class DHMedalSystem : EntitySystem
{
    [Dependency] private DHAssaultRuleSystem _assault = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<DHMedalTabletComponent>(DHMedalTabletUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<DHMedalAwardMessage>(OnAward);
        });
    }

    private void OnOpened(Entity<DHMedalTabletComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent, args.Actor);
    }

    private void UpdateUi(Entity<DHMedalTabletComponent> ent, EntityUid user)
    {
        var state = new DHMedalTabletState
        {
            Remaining = Math.Max(0, ent.Comp.MaxAwards - ent.Comp.Awarded),
            MaxCitation = ent.Comp.MaxCitation,
            Medals = Medals(ent.Comp.Faction).Select(m => m.ID).ToList(),
        };

        var fighters = EntityQueryEnumerator<DHAssaultFighterComponent, MobStateComponent>();
        while (fighters.MoveNext(out var uid, out var fighter, out _))
        {
            // Только свои и только те, кем играет (или играл) человек; себя не награждают.
            if (fighter.Faction != ent.Comp.Faction || uid == user ||
                !_mind.TryGetMind(uid, out var mindId, out var mind) || mind.UserId == null)
            {
                continue;
            }

            state.Candidates.Add(new DHMedalCandidate
            {
                Entity = GetNetEntity(uid),
                Name = Name(uid),
                Job = _jobs.MindTryGetJobName(mindId),
                Dead = _mobState.IsDead(uid),
            });
        }

        state.Candidates.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        _ui.SetUiState(ent.Owner, DHMedalTabletUiKey.Key, state);
    }

    private IEnumerable<DHMedalPrototype> Medals(DHAssaultFaction faction)
    {
        return _proto.EnumeratePrototypes<DHMedalPrototype>()
            .Where(m => m.Faction == faction)
            .OrderBy(m => m.Order);
    }

    private void OnAward(Entity<DHMedalTabletComponent> ent, ref DHMedalAwardMessage args)
    {
        var user = args.Actor;
        if (ent.Comp.Awarded >= ent.Comp.MaxAwards)
        {
            _popup.PopupEntity(Loc.GetString("dh-medal-none-left"), user, user);
            return;
        }

        if (!TryComp<DHAssaultFighterComponent>(user, out var giverFighter) || giverFighter.Faction != ent.Comp.Faction)
        {
            _popup.PopupEntity(Loc.GetString("dh-medal-wrong-side"), user, user);
            return;
        }

        if (!TryGetEntity(args.Target, out var target) || target == user ||
            !TryComp<DHAssaultFighterComponent>(target, out var fighter) || fighter.Faction != ent.Comp.Faction ||
            !_proto.TryIndex<DHMedalPrototype>(args.Medal, out var medal) || medal.Faction != ent.Comp.Faction)
        {
            return;
        }

        var citation = args.Citation.Trim();
        if (citation.Length > ent.Comp.MaxCitation)
            citation = citation[..ent.Comp.MaxCitation];

        if (citation.Length == 0)
        {
            _popup.PopupEntity(Loc.GetString("dh-medal-no-citation"), user, user);
            return;
        }

        ent.Comp.Awarded++;
        Dirty(ent);

        var recipient = Name(target.Value);
        var giver = Name(user);
        _assault.RecordMedal(ent.Comp.Faction, medal.Name, recipient, giver, citation);
        _assault.AnnounceToFaction(ent.Comp.Faction, Loc.GetString("dh-medal-sender"),
            Loc.GetString("dh-medal-announce", ("giver", giver), ("recipient", recipient),
                ("medal", Loc.GetString(medal.Name)), ("citation", citation)));

        UpdateUi(ent, user);
    }
}
