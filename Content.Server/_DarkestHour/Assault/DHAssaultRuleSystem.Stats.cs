using System.Linq;
using Content.Server.GameTicking;
using Content.Shared._DarkestHour.Assault;
using Content.Shared.Mobs;

namespace Content.Server._DarkestHour.Assault;

/// <summary>
/// Итоги раунда «Осады» (план п. 6, решения автора 2026-11-20): хронология захвата точек, потери сторон, сбитое,
/// лучшие бойцы каждой стороны (больше всех убил, больше всех спас — поднял дефибриллятором или СЛР, больше всех
/// захватов) и награды, которые вручили командиры наградным листом (<see cref="DHMedalSystem"/>).
/// </summary>
public sealed partial class DHAssaultRuleSystem
{
    private void InitializeStats()
    {
        SubscribeLocalEvent<DHAircraftLostEvent>(OnAircraftLost);
    }

    private void OnAircraftLost(DHAircraftLostEvent ev)
    {
        foreach (var rule in ActiveRules())
        {
            rule.Comp.AircraftLost++;
        }
    }

    /// <summary>Смерть бойца — потеря стороны и убийство тому, кто его добил; подъём из мёртвых — спасение.</summary>
    private void RecordMobState(Entity<DHAssaultFighterComponent> ent, MobStateChangedEvent args)
    {
        foreach (var rule in ActiveRules())
        {
            if (args.NewMobState == MobState.Dead)
            {
                rule.Comp.Losses[ent.Comp.Faction] = rule.Comp.Losses.GetValueOrDefault(ent.Comp.Faction) + 1;

                // Убийство — только противнику (своих не считаем).
                if (StatsOf(rule.Comp, args.Origin) is { } killer && killer.Faction != ent.Comp.Faction)
                    killer.Kills++;
            }
            else if (args.OldMobState == MobState.Dead &&
                     StatsOf(rule.Comp, args.Origin) is { } medic && medic.Faction == ent.Comp.Faction &&
                     args.Origin != ent.Owner)
            {
                medic.Saves++;
            }
        }
    }

    /// <summary>Точку захватила сторона: в хронологию; захват — тому, кто захватывал, и своим в зоне точки.</summary>
    public void RecordCapture(string point, DHAssaultFaction faction, DHAssaultFaction? previous,
        IEnumerable<EntityUid> capturers)
    {
        var at = _serverTicker.RoundDuration();
        foreach (var rule in ActiveRules())
        {
            rule.Comp.CaptureLog.Add(new DHAssaultCaptureRecord
            {
                At = at,
                Point = point,
                Faction = faction,
                Previous = previous,
            });

            foreach (var uid in capturers)
            {
                if (StatsOf(rule.Comp, uid) is { } stats && stats.Faction == faction)
                    stats.Captures++;
            }
        }
    }

    /// <summary>Командир вручил награду.</summary>
    public void RecordMedal(DHAssaultFaction faction, LocId medal, string recipient, string giver, string citation)
    {
        var at = _serverTicker.RoundDuration();
        foreach (var rule in ActiveRules())
        {
            rule.Comp.Medals.Add(new DHAssaultMedalRecord
            {
                At = at,
                Faction = faction,
                Medal = medal,
                Recipient = recipient,
                Giver = giver,
                Citation = citation,
            });
        }
    }

    /// <summary>Счёт игрока, который управляет <paramref name="uid"/> (боец «Осады» с разумом).</summary>
    private DHAssaultFighterRecord? StatsOf(DHAssaultRuleComponent rule, EntityUid? uid)
    {
        if (uid is not { } entity || !TryComp<DHAssaultFighterComponent>(entity, out var fighter) ||
            !_mind.TryGetMind(entity, out _, out var mind) || mind.UserId is not { } user)
        {
            return null;
        }

        if (!rule.FighterStats.TryGetValue(user, out var stats))
        {
            stats = new DHAssaultFighterRecord { Faction = fighter.Faction };
            rule.FighterStats[user] = stats;
        }

        // Имя — последнего персонажа, которым играл (после волны подкреплений может смениться).
        stats.Name = Name(entity);
        stats.Faction = fighter.Faction;
        return stats;
    }

    private void AppendStats(DHAssaultRuleComponent rule, ref RoundEndTextAppendEvent args)
    {
        // Хронология захвата точек.
        args.AddLine(Loc.GetString("dh-assault-stats-captures-header"));
        if (rule.CaptureLog.Count == 0)
            args.AddLine(Loc.GetString("dh-assault-stats-none"));

        foreach (var capture in rule.CaptureLog)
        {
            args.AddLine(Loc.GetString(capture.Previous is { } previous && previous != capture.Faction
                    ? "dh-assault-stats-capture-from"
                    : "dh-assault-stats-capture",
                ("time", $"{(int) capture.At.TotalHours:D2}:{capture.At.Minutes:D2}:{capture.At.Seconds:D2}"),
                ("side", SideName(capture.Faction)),
                ("color", SideColor(capture.Faction)),
                ("point", capture.Point),
                ("from", capture.Previous is { } from ? SideName(from) : string.Empty)));
        }

        args.AddLine("");

        // Потери и сбитое.
        args.AddLine(Loc.GetString("dh-assault-stats-losses",
            ("republic", rule.Losses.GetValueOrDefault(DHAssaultFaction.Republic)),
            ("empire", rule.Losses.GetValueOrDefault(DHAssaultFaction.Empire))));

        var carrierDown = rule.Carrier is { } carrier && TryComp<DHAssaultCarrierComponent>(carrier, out var carrierComp)
            && carrierComp.Downed;
        args.AddLine(Loc.GetString("dh-assault-stats-downed",
            ("aircraft", rule.AircraftLost),
            ("carrier", Loc.GetString(carrierDown ? "dh-assault-stats-carrier-down" : "dh-assault-stats-carrier-intact"))));
        args.AddLine("");

        // Лучшие бойцы каждой стороны.
        args.AddLine(Loc.GetString("dh-assault-stats-best-header"));
        foreach (var faction in new[] { DHAssaultFaction.Empire, DHAssaultFaction.Republic })
        {
            var side = rule.FighterStats.Values.Where(s => s.Faction == faction).ToList();
            args.AddLine(Loc.GetString("dh-assault-stats-best-side",
                ("side", SideName(faction)),
                ("color", SideColor(faction)),
                ("killer", Best(side, s => s.Kills)),
                ("medic", Best(side, s => s.Saves)),
                ("captor", Best(side, s => s.Captures))));
        }

        args.AddLine("");

        // Награды.
        args.AddLine(Loc.GetString("dh-assault-stats-medals-header"));
        if (rule.Medals.Count == 0)
            args.AddLine(Loc.GetString("dh-assault-stats-none"));

        foreach (var medal in rule.Medals)
        {
            args.AddLine(Loc.GetString("dh-assault-stats-medal",
                ("color", SideColor(medal.Faction)),
                ("medal", Loc.GetString(medal.Medal)),
                ("recipient", medal.Recipient),
                ("giver", medal.Giver),
                ("citation", medal.Citation)));
        }

        args.AddLine("");
    }

    private static string Best(List<DHAssaultFighterRecord> side, Func<DHAssaultFighterRecord, int> score)
    {
        var best = side.OrderByDescending(score).FirstOrDefault();
        return best == null || score(best) <= 0
            ? Loc.GetString("dh-assault-stats-nobody")
            : Loc.GetString("dh-assault-stats-best-entry", ("name", best.Name), ("count", score(best)));
    }

    private static string SideName(DHAssaultFaction faction)
    {
        return Loc.GetString(faction == DHAssaultFaction.Republic ? "dh-assault-stats-republic" : "dh-assault-stats-empire");
    }

    private static string SideColor(DHAssaultFaction faction)
    {
        return (faction == DHAssaultFaction.Republic ? RepublicGreen : ImperialYellow).ToHex();
    }
}
