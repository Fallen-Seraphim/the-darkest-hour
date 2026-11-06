using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared._DarkestHour.Uprising;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Mind.Components;
using Robust.Shared.Audio;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._DarkestHour.Uprising;

/// <summary>
/// ЧВК LA-V (решение автора 2026-11-06): отряд из четырёх бойцов (командир, медик, специалист, стрелок) по вызову
/// администрации прилетает на ЗП-1 «Аэронавтика» эвакуационным бортом: если эвакуацию уже заказали — им, если нет —
/// корпорация присылает эвакуационный борт вместе с отрядом. Борт с ними ждёт дольше обычного: отряду нужно
/// найти представителя LA-V и вывезти его любой ценой, остальных — по возможности.
/// </summary>
public sealed partial class DHUprisingRuleSystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private TurfSystem _turf = default!;

    private static readonly SoundSpecifier PmcSound = new SoundPathSpecifier("/Audio/Announcements/attention.ogg");

    private void InitializePmc()
    {
        SubscribeLocalEvent<DHLAVPmcComponent, MindAddedMessage>(OnPmcMindAdded);
    }

    private void OnPmcMindAdded(Entity<DHLAVPmcComponent> ent, ref MindAddedMessage args)
    {
        if (ent.Comp.Briefed)
            return;

        ent.Comp.Briefed = true;
        var member = EnsureComp<DHUprisingMemberComponent>(ent);
        member.Side = DHUprisingSide.Vanguard;
        member.Combatant = true;
        _antag.SendBriefing(ent.Owner, Loc.GetString(ent.Comp.Briefing), VanguardPurple, null);
    }

    /// <summary>Вызов ЧВК: летит заказанным эвакуационным бортом, а если эвакуацию не заказывали — со своим.</summary>
    public bool TryCallPmc(out string message)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var rule, out _, out _))
        {
            if (rule.Ended)
                continue;

            if (rule.PmcPending)
            {
                message = Loc.GetString("dh-uprising-pmc-call-pending");
                return false;
            }

            rule.PmcCalls++;

            // Борт уже на площадке — отряд высаживается с него сейчас.
            if (rule.Flight is { } flight && Exists(flight))
            {
                rule.PmcPending = true;
                LandPmc(rule, flight);
                message = Loc.GetString("dh-uprising-pmc-call-done-now");
                return true;
            }

            int minutes;
            string key;
            if (rule.NextFlight < rule.Flights.Count)
            {
                // Эвакуацию уже заказали — отряд летит этим бортом.
                minutes = Math.Max(0, (int) Math.Ceiling((rule.Start + rule.Flights[rule.NextFlight] - Timing.CurTime).TotalMinutes));
                key = "dh-uprising-pmc-called-evac";
            }
            else
            {
                // Эвакуацию не заказывали — корпорация присылает эвакуационный борт с отрядом.
                rule.Flights.Insert(rule.NextFlight, Timing.CurTime + rule.FlightWarning - rule.Start);
                rule.FlightWarned = false;
                minutes = (int) Math.Ceiling(rule.FlightWarning.TotalMinutes);
                key = "dh-uprising-pmc-called";
            }

            rule.PmcPending = true;
            _chat.DispatchGlobalAnnouncement(Loc.GetString(key, ("minutes", minutes)),
                Loc.GetString("dh-uprising-sender-vanguard"), true, PmcSound, VanguardPurple);
            message = Loc.GetString("dh-uprising-pmc-call-done", ("minutes", minutes));
            return true;
        }

        message = Loc.GetString("dh-uprising-evac-no-mode");
        return false;
    }

    /// <summary>Высадка отряда на борт: точки появления — на свободном полу у центра борта.</summary>
    private void LandPmc(DHUprisingRuleComponent rule, EntityUid flight)
    {
        rule.PmcPending = false;
        if (!TryComp<MapGridComponent>(flight, out var gridComp))
            return;

        // Места высадки: проходимые клетки пола, ближайшие к центру борта.
        var center = gridComp.LocalAABB.Center;
        var free = new List<(float Distance, Vector2i Tile)>();
        foreach (var tile in _maps.GetAllTiles(flight, gridComp))
        {
            if (tile.Tile.IsEmpty || _turf.IsTileBlocked(tile, CollisionGroup.MobMask))
                continue;

            var pos = _maps.GridTileToLocal(flight, gridComp, tile.GridIndices).Position;
            free.Add(((pos - center).LengthSquared(), tile.GridIndices));
        }

        free.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        for (var i = 0; i < rule.PmcSpawners.Count; i++)
        {
            var spot = i < free.Count
                ? _maps.GridTileToLocal(flight, gridComp, free[i].Tile)
                : new EntityCoordinates(flight, center);
            Spawn(rule.PmcSpawners[i], spot);
        }

        // Борт ждёт отряд с представителем.
        var stay = rule.PmcFlightStay > rule.FlightStay ? rule.PmcFlightStay : rule.FlightStay;
        if (rule.FlightDeparts < Timing.CurTime + stay)
            rule.FlightDeparts = Timing.CurTime + stay;

        _chat.DispatchGlobalAnnouncement(Loc.GetString("dh-uprising-pmc-landed",
                ("minutes", (int) Math.Ceiling((rule.FlightDeparts - Timing.CurTime).TotalMinutes))),
            Loc.GetString("dh-uprising-sender-vanguard"), true, PmcSound, VanguardPurple);
    }

    public void LogPmcCall(IConsoleShell shell)
    {
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{shell.Player?.Name ?? "server"} called the LA-V PMC squad (Uprising)");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class DHCallLAVPmcCommand : LocalizedEntityCommands
{
    [Dependency] private DHUprisingRuleSystem _uprising = default!;

    public override string Command => "dhcalllavpmc";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_uprising.TryCallPmc(out var message))
        {
            _uprising.LogPmcCall(shell);
            shell.WriteLine(message);
        }
        else
        {
            shell.WriteError(message);
        }
    }
}
