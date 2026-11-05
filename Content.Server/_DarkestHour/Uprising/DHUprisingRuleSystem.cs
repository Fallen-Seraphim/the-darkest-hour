using Content.Server.Chat.Systems;
using Content.Server.RoundEnd;
using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Assault;
using Content.Shared.Antag;
using Content.Shared.Cuffs.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking.Rules;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Audio;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._DarkestHour.Uprising;

/// <summary>
/// «Восстание»: стороны по должностям, вступительные инструкции, борт на ЗП-1 по расписанию (эвакуация
/// представителя LA-V), учёт убитых мирных и итоги раунда.
/// </summary>
public sealed partial class DHUprisingRuleSystem : GameRuleSystem<DHUprisingRuleComponent>
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private MetaDataSystem _meta = default!;

    public static readonly Color PoliceBlue = Color.FromHex("#5B8AD6");
    public static readonly Color FrontRed = Color.FromHex("#8B0000"); // тёмно-красный (решение автора 2026-11-05)

    /// <summary>Звук вступления ОФСК — ванильной революции.</summary>
    private static readonly SoundPathSpecifier FrontSound = new("/Audio/Ambience/Antag/headrev_start.ogg");
    public static readonly Color VanguardPurple = Color.FromHex("#A68BE8");
    public static readonly Color CivilianGrey = Color.FromHex("#C8C8C8");

    private static readonly SoundSpecifier FlightSound = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<DHUprisingMemberComponent, MobStateChangedEvent>(OnMemberMobState);
    }

    protected override void Started(Entity<DHUprisingRuleComponent, GameRuleComponent> rule, ref GameRuleStartedEvent args)
    {
        base.Started(rule, ref args);
        rule.Comp1.Start = Timing.CurTime;
    }

    #region Стороны

    private DHUprisingSide SideOf(DHUprisingRuleComponent rule, string? job)
    {
        if (job == null)
            return DHUprisingSide.Civilian;

        if (rule.PoliceJobs.Contains(job))
            return DHUprisingSide.Police;

        if (rule.FrontJobs.Contains(job))
            return DHUprisingSide.Front;

        if (rule.VanguardJobs.Contains(job))
            return DHUprisingSide.Vanguard;

        return DHUprisingSide.Civilian;
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var rule, out _, out _))
        {
            var side = SideOf(rule, ev.JobId);
            var member = EnsureComp<DHUprisingMemberComponent>(ev.Mob);
            member.Side = side;
            member.Vip = ev.JobId != null && rule.VipJobs.Contains(ev.JobId);
            member.Leader = ev.JobId != null && rule.LeaderJobs.Contains(ev.JobId);

            // Военпол и ОФСК — бойцы своих сторон (тактическая карта, детекторы, зоны).
            if (side == DHUprisingSide.Police)
                EnsureComp<DHAssaultFighterComponent>(ev.Mob).Faction = DHAssaultFaction.Republic;
            else if (side == DHUprisingSide.Front)
                EnsureComp<DHAssaultFighterComponent>(ev.Mob).Faction = DHAssaultFaction.Front;

            var (key, color) = side switch
            {
                DHUprisingSide.Police => ("dh-uprising-briefing-police", PoliceBlue),
                DHUprisingSide.Front => (member.Leader ? "dh-uprising-briefing-front-leader" : "dh-uprising-briefing-front", FrontRed),
                DHUprisingSide.Vanguard => (member.Vip ? "dh-uprising-briefing-vanguard-vip" : "dh-uprising-briefing-vanguard", VanguardPurple),
                _ => ("dh-uprising-briefing-civilian", CivilianGrey),
            };

            var flight = NextFlightIn(rule);
            _antag.SendBriefing(ev.Mob, Loc.GetString(key, ("minutes", flight)), color,
                side == DHUprisingSide.Front ? FrontSound : null);
            break;
        }
    }

    /// <summary>Через сколько минут ближайший борт (−1 — бортов больше не будет).</summary>
    private int NextFlightIn(DHUprisingRuleComponent rule)
    {
        if (rule.NextFlight >= rule.Flights.Count)
            return -1;

        var at = rule.Start + rule.Flights[rule.NextFlight];
        return Math.Max(0, (int) Math.Ceiling((at - Timing.CurTime).TotalMinutes));
    }

    private void OnMemberMobState(Entity<DHUprisingMemberComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead ||
            ent.Comp.Side is not (DHUprisingSide.Civilian or DHUprisingSide.Vanguard))
        {
            return;
        }

        // Кто убил мирного: по источнику урона.
        DHUprisingSide? killer = null;
        if (args.Origin is { } origin && TryComp<DHUprisingMemberComponent>(origin, out var originMember))
            killer = originMember.Side;

        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var rule, out _, out _))
        {
            rule.CiviliansKilled++;
            if (killer == DHUprisingSide.Police)
                rule.CiviliansKilledByPolice++;
            else if (killer == DHUprisingSide.Front)
                rule.CiviliansKilledByFront++;
        }
    }

    #endregion

    #region Борт на ЗП-1

    protected override void ActiveTick(EntityUid uid, DHUprisingRuleComponent rule, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, rule, gameRule, frameTime);

        if (rule.Ended)
            return;

        var now = Timing.CurTime;
        if (now >= rule.Start + rule.TimeLimit)
        {
            rule.Ended = true;
            _chat.DispatchGlobalAnnouncement(Loc.GetString("dh-uprising-time-up"),
                Loc.GetString("dh-uprising-sender-city"), true, null, CivilianGrey);
            _roundEnd.EndRound(TimeSpan.FromMinutes(1));
            return;
        }

        // Борт стоит на ЗП-1 — ждём вылета.
        if (rule.Flight is { } flight)
        {
            if (now >= rule.FlightDeparts)
                Depart(rule, flight);
            return;
        }

        if (rule.NextFlight >= rule.Flights.Count)
            return;

        var arrive = rule.Start + rule.Flights[rule.NextFlight];
        if (!rule.FlightWarned && now >= arrive - rule.FlightWarning)
        {
            rule.FlightWarned = true;
            _chat.DispatchGlobalAnnouncement(Loc.GetString("dh-uprising-flight-incoming",
                    ("minutes", (int) Math.Ceiling((arrive - now).TotalMinutes)),
                    ("stay", (int) rule.FlightStay.TotalMinutes)),
                Loc.GetString("dh-uprising-sender-aeronautics"), true, FlightSound, CivilianGrey);
        }

        if (now >= arrive)
            Arrive(rule);
    }

    private void Arrive(DHUprisingRuleComponent rule)
    {
        rule.NextFlight++;
        rule.FlightWarned = false;

        var pads = EntityQueryEnumerator<DHUprisingFlightPadComponent, TransformComponent>();
        if (!pads.MoveNext(out var pad, out _, out var padXform) || padXform.MapUid is not { } map)
        {
            Log.Error("Uprising: no flight pad (DHUprisingFlightPad) on the map");
            return;
        }

        var padPos = _transform.GetWorldPosition(padXform);
        if (!_loader.TryLoadGrid(padXform.MapID, rule.FlightPath, out var grid, offset: padPos))
        {
            Log.Error($"Uprising: failed to load the flight {rule.FlightPath}");
            return;
        }

        // Центр борта — на площадке.
        var center = grid.Value.Comp.LocalAABB.Center;
        _transform.SetWorldPosition(grid.Value.Owner, padPos - center);
        _meta.SetEntityName(grid.Value, Loc.GetString(rule.FlightName));

        rule.Flight = grid.Value;
        rule.FlightDeparts = Timing.CurTime + rule.FlightStay;
        _chat.DispatchGlobalAnnouncement(Loc.GetString("dh-uprising-flight-landed",
                ("minutes", (int) rule.FlightStay.TotalMinutes)),
            Loc.GetString("dh-uprising-sender-aeronautics"), true, FlightSound, CivilianGrey);
    }

    private void Depart(DHUprisingRuleComponent rule, EntityUid flight)
    {
        rule.Flight = null;
        if (!Exists(flight))
            return;

        // Кто из важных персон на борту живым — эвакуирован.
        var evacuated = new List<string>();
        var members = EntityQueryEnumerator<DHUprisingMemberComponent, MobStateComponent, TransformComponent>();
        while (members.MoveNext(out var uid, out var member, out var mob, out var xform))
        {
            if (!member.Vip || mob.CurrentState == MobState.Dead || xform.GridUid != flight)
                continue;

            rule.VipsEvacuated++;
            evacuated.Add(Name(uid));
        }

        var text = evacuated.Count > 0
            ? Loc.GetString("dh-uprising-flight-departed-vip", ("names", string.Join(", ", evacuated)))
            : Loc.GetString("dh-uprising-flight-departed");
        _chat.DispatchGlobalAnnouncement(text, Loc.GetString("dh-uprising-sender-aeronautics"), true, FlightSound,
            evacuated.Count > 0 ? VanguardPurple : CivilianGrey);

        // Борт улетает со всеми на борту.
        QueueDel(flight);
    }

    #endregion

    #region Итоги

    protected override void AppendRoundEndText(Entity<DHUprisingRuleComponent> rule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(rule, ref args);

        var leaders = 0;
        var leadersFree = 0;
        var police = 0;
        var front = 0;
        var query = EntityQueryEnumerator<DHUprisingMemberComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var member, out var mob))
        {
            var alive = mob.CurrentState != MobState.Dead;
            var cuffed = TryComp<CuffableComponent>(uid, out var cuffable) && cuffable.CuffedHandCount > 0;
            if (member.Leader)
            {
                leaders++;
                if (alive && !cuffed)
                    leadersFree++;
            }

            if (!alive || cuffed)
                continue;

            if (member.Side == DHUprisingSide.Police)
                police++;
            else if (member.Side == DHUprisingSide.Front)
                front++;
        }

        // Лидеры ОФСК на свободе — квартал за Фронтом; все схвачены или убиты — за военполом.
        var outcome = leaders == 0 ? "dh-uprising-end-no-leaders"
            : leadersFree > 0 ? "dh-uprising-end-front" : "dh-uprising-end-police";

        args.AddLine(Loc.GetString(outcome, ("free", leadersFree), ("leaders", leaders)));
        args.AddLine(Loc.GetString(rule.Comp.VipsEvacuated > 0 ? "dh-uprising-end-vip-evacuated" : "dh-uprising-end-vip-stayed"));
        args.AddLine(Loc.GetString("dh-uprising-end-forces", ("police", police), ("front", front)));
        args.AddLine(Loc.GetString("dh-uprising-end-civilians", ("total", rule.Comp.CiviliansKilled),
            ("police", rule.Comp.CiviliansKilledByPolice), ("front", rule.Comp.CiviliansKilledByFront)));
        args.AddLine("");
    }

    #endregion
}
