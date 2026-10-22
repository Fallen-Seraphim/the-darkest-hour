using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.AlertLevel;
using Content.Shared.Database;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._DarkestHour.Republic;

/// <summary>
/// Отряд зачистки Соединённой Республики: вызов одной кнопкой из админ-панели. Шаттл RAC Cordon с точками появления
/// восьми бойцов (командир, снайпер, пулемётчик, два автоматчика, два медика, огнемётчик) грузится на отдельной
/// карте и прыгает к станции, а станция сразу переходит на код Индомитус (его объявление и есть оповещение экипажа).
/// </summary>
public sealed partial class DHRepublicSweepSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AlertLevelSystem _alert = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private StationSystem _station = default!;

    public static readonly ResPath ShuttlePath = new("/Maps/_DarkestHour/Shuttles/republic_sweep.yml");
    public static readonly ProtoId<AlertLevelPrototype> Indomitus = "DHIndomitus";

    /// <summary>
    /// Вызывает отряд зачистки к станции. <paramref name="station"/> — станция-цель; без неё — первая станция с экипажем.
    /// </summary>
    public bool TryCallSweep(EntityUid? station, out string message)
    {
        station ??= FindStation();
        if (station == null || _station.GetLargestGrid(station.Value) is not { } target)
        {
            message = Loc.GetString("dh-republic-call-no-station");
            return false;
        }

        var mapUid = _map.CreateMap(out var mapId);
        _meta.SetEntityName(mapUid, Loc.GetString("dh-republic-staging-map"));

        if (!_loader.TryLoadGrid(mapId, ShuttlePath, out var grid) ||
            !TryComp<ShuttleComponent>(grid.Value, out var shuttle))
        {
            Del(mapUid);
            message = Loc.GetString("dh-republic-call-load-failed", ("path", ShuttlePath.ToString()));
            return false;
        }

        _shuttle.FTLToDock(grid.Value, shuttle, target);

        // Индомитус: контракты расторгнуты, военное положение. Ставится принудительно (код не выбирается с консоли).
        _alert.SetLevel(station.Value, Indomitus, playSound: true, announce: true, force: true, locked: true);

        message = Loc.GetString("dh-republic-call-done", ("station", Name(station.Value)));
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
        _adminLog.Add(LogType.Action, LogImpact.Extreme,
            $"{shell.Player?.Name ?? "server"} called the Republic sweep team (Indomitus) to {ToPrettyString(station)}");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class DHCallRepublicCommand : LocalizedEntityCommands
{
    [Dependency] private DHRepublicSweepSystem _republic = default!;

    public override string Command => "dhcallrepublic";

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

        if (_republic.TryCallSweep(station, out var message))
        {
            _republic.LogCall(shell, station);
            shell.WriteLine(message);
        }
        else
        {
            shell.WriteError(message);
        }
    }
}
