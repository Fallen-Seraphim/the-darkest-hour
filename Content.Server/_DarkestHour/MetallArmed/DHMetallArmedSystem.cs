using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Utility;

namespace Content.Server._DarkestHour.MetallArmed;

/// <summary>
/// ОБР Metall Armed: вызов отряда одной кнопкой из админ-панели. Шаттл с точками появления шести бойцов
/// (командир, специалист по вооружению, боевой медик, три стрелка) грузится на отдельной карте и прыгает к станции.
/// </summary>
public sealed partial class DHMetallArmedSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private ServerStationSystem _station = default!;

    public static readonly ResPath ShuttlePath = new("/Maps/_DarkestHour/Shuttles/metall_armed_response.yml");

    /// <summary>
    /// Вызывает ОБР к станции. <paramref name="station"/> — станция-цель; без неё — первая станция с экипажем.
    /// </summary>
    public bool TryCallResponse(EntityUid? station, out string message)
    {
        station ??= FindStation();
        if (station == null || _station.GetLargestGrid(station.Value) is not { } target)
        {
            message = Loc.GetString("dh-metall-armed-call-no-station");
            return false;
        }

        var mapUid = _map.CreateMap(out var mapId);
        _meta.SetEntityName(mapUid, Loc.GetString("dh-metall-armed-staging-map"));

        if (!_loader.TryLoadGrid(mapId, ShuttlePath, out var grid))
        {
            Del(mapUid);
            message = Loc.GetString("dh-metall-armed-call-load-failed", ("path", ShuttlePath.ToString()));
            return false;
        }

        if (!TryComp<ShuttleComponent>(grid.Value, out var shuttle))
        {
            message = Loc.GetString("dh-metall-armed-call-load-failed", ("path", ShuttlePath.ToString()));
            return false;
        }

        _shuttle.FTLToDock(grid.Value, shuttle, target);

        _chat.DispatchStationAnnouncement(station.Value,
            Loc.GetString("dh-metall-armed-call-announcement"),
            Loc.GetString("dh-metall-armed-announcement-sender"),
            colorOverride: Color.FromHex("#9A9A9A"));

        message = Loc.GetString("dh-metall-armed-call-done", ("station", Name(station.Value)));
        return true;
    }

    private EntityUid? FindStation()
    {
        var query = EntityQueryEnumerator<StationJobsComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            return uid;
        }

        return null;
    }

    public void LogCall(IConsoleShell shell, EntityUid? station)
    {
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{shell.Player?.Name ?? "server"} called the Metall Armed response team to {ToPrettyString(station)}");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class DHCallMetallArmedCommand : LocalizedEntityCommands
{
    [Dependency] private DHMetallArmedSystem _metallArmed = default!;

    public override string Command => "dhcallmetallarmed";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        EntityUid? station = null;
        if (args.Length > 0)
        {
            if (!NetEntity.TryParse(args[0], out var net) || !EntityManager.TryGetEntity(net, out station))
            {
                shell.WriteError(Loc.GetString("shell-entity-uid-must-be-number"));
                return;
            }
        }

        var ok = _metallArmed.TryCallResponse(station, out var message);
        if (ok)
        {
            _metallArmed.LogCall(shell, station);
            shell.WriteLine(message);
        }
        else
        {
            shell.WriteError(message);
        }
    }
}
