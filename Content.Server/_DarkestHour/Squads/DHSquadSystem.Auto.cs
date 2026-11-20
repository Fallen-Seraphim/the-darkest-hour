using System.Linq;
using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Squads;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Squads;

/// <summary>
/// Распределение пехоты по отрядам на старте раунда (решение автора 2026-11-19, как в RMC): лидеры — по одному на
/// отряд и сразу командиры; медики, пулемётчики и спецы — поровну; стрелки — в любимый отряд из профиля, если он не
/// перегружен. Опоздавших не трогаем — их назначает командование через консоль.
/// </summary>
public sealed partial class DHSquadSystem
{
    [Dependency] private IRobustRandom _random = default!;

    private void InitializeAuto()
    {
        SubscribeLocalEvent<RulePlayerJobsAssignedEvent>(OnJobsAssigned);
    }

    private sealed class SquadSlot(DHSquadPrototype squad)
    {
        public readonly DHSquadPrototype Squad = squad;
        public int Size;
        public bool HasLeader;
        public readonly Dictionary<string, int> Jobs = new();
        public readonly List<EntityUid> Members = new();
    }

    private void OnJobsAssigned(RulePlayerJobsAssignedEvent ev)
    {
        foreach (var roster in _proto.EnumeratePrototypes<DHSquadRosterPrototype>())
        {
            Distribute(roster, ev.Players, ev.Profiles);
        }
    }

    private void Distribute(DHSquadRosterPrototype roster, ICommonSession[] players,
        IReadOnlyDictionary<NetUserId, HumanoidCharacterProfile> profiles)
    {
        var slots = Squads(roster.Faction).Select(s => new SquadSlot(s)).ToList();
        if (slots.Count == 0)
            return;

        var leaders = new List<(EntityUid Mob, string Job, string? Pref)>();
        var spread = new List<(EntityUid Mob, string Job, string? Pref)>();
        var members = new List<(EntityUid Mob, string Job, string? Pref)>();

        foreach (var player in players)
        {
            if (player.AttachedEntity is not { } mob ||
                !TryComp<DHAssaultFighterComponent>(mob, out var fighter) || fighter.Faction != roster.Faction ||
                HasComp<DHSquadMemberComponent>(mob) ||
                !_mind.TryGetMind(mob, out var mindId, out _) ||
                !_jobs.MindTryGetJobId(mindId, out var jobId) || jobId is not { } job)
            {
                continue;
            }

            var pref = profiles.TryGetValue(player.UserId, out var profile)
                ? roster.Faction == DHAssaultFaction.Republic ? profile.DHDetails.RepublicSquad : profile.DHDetails.EmpireSquad
                : null;

            var entry = (mob, job.Id, pref);
            if (roster.Leaders.Contains(job))
                leaders.Add(entry);
            else if (roster.Spread.Contains(job))
                spread.Add(entry);
            else if (roster.Members.Contains(job))
                members.Add(entry);
        }

        _random.Shuffle(leaders);
        _random.Shuffle(spread);
        _random.Shuffle(members);

        // Лидеры: любимый отряд, если там ещё нет командира; иначе первый отряд без командира. Лишние — бойцами.
        foreach (var (mob, job, pref) in leaders)
        {
            var slot = slots.FirstOrDefault(s => s.Squad.ID == pref && !s.HasLeader) ??
                       slots.FirstOrDefault(s => !s.HasLeader);
            if (slot == null)
            {
                members.Add((mob, job, pref));
                continue;
            }

            Place(slot, mob, job, leader: true);
        }

        // «Вразнос»: туда, где этой должности меньше всего (при равенстве — любимый, потом самый маленький отряд).
        foreach (var group in spread.GroupBy(e => e.Job))
        {
            foreach (var (mob, job, pref) in group)
            {
                var fewest = slots.Min(s => s.Jobs.GetValueOrDefault(job));
                var candidates = slots.Where(s => s.Jobs.GetValueOrDefault(job) == fewest).ToList();
                var slot = candidates.FirstOrDefault(s => s.Squad.ID == pref && Fits(slots, s, roster)) ??
                           candidates.MinBy(s => s.Size)!;
                Place(slot, mob, job, leader: false);
            }
        }

        // Остальные: любимый отряд, если он не больше самого маленького более чем на MaxImbalance.
        foreach (var (mob, job, pref) in members)
        {
            var slot = slots.FirstOrDefault(s => s.Squad.ID == pref && Fits(slots, s, roster)) ??
                       slots.MinBy(s => s.Size)!;
            Place(slot, mob, job, leader: false);
        }

        // Отряды без командира: бойцы знают, что его назначит командование.
        foreach (var slot in slots.Where(s => !s.HasLeader && s.Members.Count > 0))
        {
            var name = Loc.GetString(slot.Squad.Name);
            foreach (var member in slot.Members)
            {
                _popup.PopupEntity(Loc.GetString("dh-squad-no-leader", ("squad", name)), member, member,
                    PopupType.Medium);
            }
        }
    }

    private static bool Fits(List<SquadSlot> slots, SquadSlot slot, DHSquadRosterPrototype roster)
    {
        return slot.Size + 1 - slots.Min(s => s.Size) <= roster.MaxImbalance;
    }

    private void Place(SquadSlot slot, EntityUid mob, string job, bool leader)
    {
        slot.Size++;
        slot.Jobs[job] = slot.Jobs.GetValueOrDefault(job) + 1;
        slot.Members.Add(mob);
        Assign(mob, slot.Squad);

        // Сообщения «вы в отряде» / «вы командир отряда» выдают Assign и SetLeader.
        if (leader && TryComp<DHSquadMemberComponent>(mob, out var member))
        {
            slot.HasLeader = true;
            SetLeader((mob, member));
        }
    }
}
