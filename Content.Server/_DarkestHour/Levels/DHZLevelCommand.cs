using Content.Server.Administration;
using Content.Shared._DarkestHour.Levels;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._DarkestHour.Levels;

/// <summary>
/// Z-уровни для маппинга: без аргументов — уровни стопки, где стоишь; с номером — пустой уровень в той же стопке
/// (на нём строят этаж и сохраняют грид через savegrid, путь — в Levels базы).
/// </summary>
[AdminCommand(AdminFlags.Mapping)]
public sealed partial class DHZLevelCommand : LocalizedEntityCommands
{
    [Dependency] private DHZLevelSystem _z = default!;
    [Dependency] private DHZTransitSystem _transit = default!;

    public override string Command => "dhzlevel";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } player || EntityManager.GetComponent<TransformComponent>(player).MapUid is not { } map)
        {
            shell.WriteError(Loc.GetString("cmd-dhzlevel-no-map"));
            return;
        }

        if (args.Length == 0)
        {
            if (!_z.TryGetLevel(map, out var here))
            {
                shell.WriteLine(Loc.GetString("cmd-dhzlevel-no-stack"));
                return;
            }

            foreach (var (levelMap, level) in _z.Levels(here.Stack))
            {
                shell.WriteLine(Loc.GetString("cmd-dhzlevel-entry", ("level", level),
                    ("map", EntityManager.GetComponent<TransformComponent>(levelMap).MapID.ToString())));
            }

            return;
        }

        if (args.Length != 1 || !int.TryParse(args[0], out var number))
        {
            shell.WriteError(Loc.GetString("cmd-dhzlevel-help"));
            return;
        }

        if (_transit.CreateEmptyLevel(map, number, out var error) is not { } created)
        {
            shell.WriteError(Loc.GetString(error == "exists" ? "cmd-dhzlevel-exists" : "cmd-dhzlevel-range"));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-dhzlevel-created", ("level", number),
            ("map", EntityManager.GetComponent<TransformComponent>(created).MapID.ToString())));
    }
}

/// <summary>Маппинг: перейти на уровень N в той же точке.</summary>
[AdminCommand(AdminFlags.Mapping)]
public sealed partial class DHZTeleportCommand : LocalizedEntityCommands
{
    [Dependency] private DHZLevelSystem _z = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override string Command => "dhztp";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } player || args.Length != 1 || !int.TryParse(args[0], out var level))
        {
            shell.WriteError(Loc.GetString("cmd-dhztp-help"));
            return;
        }

        var at = _transform.GetMapCoordinates(player);
        if (_z.OnLevel(at, level) is not { } destination)
        {
            shell.WriteError(Loc.GetString("cmd-dhztp-no-level"));
            return;
        }

        _transform.SetMapCoordinates(player, destination);
    }
}

/// <summary>Маппинг: загрузить грид в уровень N (уровень создаётся, если его нет) — для правки готового этажа.</summary>
[AdminCommand(AdminFlags.Mapping)]
public sealed partial class DHZLoadCommand : LocalizedEntityCommands
{
    [Dependency] private DHZLevelSystem _z = default!;
    [Dependency] private DHZTransitSystem _transit = default!;
    [Dependency] private Robust.Shared.EntitySerialization.Systems.MapLoaderSystem _loader = default!;

    public override string Command => "dhzload";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } player ||
            EntityManager.GetComponent<TransformComponent>(player).MapUid is not { } map ||
            args.Length != 2 || !int.TryParse(args[0], out var level))
        {
            shell.WriteError(Loc.GetString("cmd-dhzload-help"));
            return;
        }

        EntityUid? levelMap = _z.TryGetLevel(map, out var here) ? _z.LevelMap(here.Stack, level) : null;
        levelMap ??= _transit.CreateEmptyLevel(map, level, out _);
        if (levelMap == null)
        {
            shell.WriteError(Loc.GetString("cmd-dhzlevel-range"));
            return;
        }

        var mapId = EntityManager.GetComponent<TransformComponent>(levelMap.Value).MapID;
        if (!_loader.TryLoadGrid(mapId, new Robust.Shared.Utility.ResPath(args[1]), out _))
        {
            shell.WriteError(Loc.GetString("cmd-dhzload-failed"));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-dhzload-done", ("level", level), ("map", mapId.ToString())));
    }
}

/// <summary>
/// Маппинг: сохранить все уровни стопки, кроме земли (её — savemap), по одному гриду: папка/level_N.yml.
/// Печатает готовый кусок прототипа для <c>levels</c> базы.
/// </summary>
[AdminCommand(AdminFlags.Mapping)]
public sealed partial class DHZSaveCommand : LocalizedEntityCommands
{
    [Dependency] private DHZLevelSystem _z = default!;
    [Dependency] private Robust.Shared.EntitySerialization.Systems.MapLoaderSystem _loader = default!;

    public override string Command => "dhzsave";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } player ||
            EntityManager.GetComponent<TransformComponent>(player).MapUid is not { } map ||
            args.Length != 1 || !_z.TryGetLevel(map, out var here))
        {
            shell.WriteError(Loc.GetString("cmd-dhzsave-help"));
            return;
        }

        var folder = args[0].TrimEnd('/');
        var snippet = new System.Text.StringBuilder("levels:\n");
        foreach (var (levelMap, level) in _z.Levels(here.Stack))
        {
            if (level == 0)
                continue;

            EntityUid? grid = null;
            var count = 0;
            var query = EntityManager.EntityQueryEnumerator<Robust.Shared.Map.Components.MapGridComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var xform))
            {
                if (xform.MapUid != levelMap || uid == levelMap)
                    continue;

                grid ??= uid;
                count++;
            }

            if (grid == null)
            {
                shell.WriteLine(Loc.GetString("cmd-dhzsave-empty", ("level", level)));
                continue;
            }

            if (count > 1)
                shell.WriteLine(Loc.GetString("cmd-dhzsave-many", ("level", level), ("count", count)));

            var path = $"{folder}/level_{level}.yml";
            if (_loader.TrySaveGrid(grid.Value, new Robust.Shared.Utility.ResPath(path)))
            {
                shell.WriteLine(Loc.GetString("cmd-dhzsave-saved", ("level", level), ("path", path)));
                snippet.Append($"  {level}: {path}\n");
            }
            else
            {
                shell.WriteError(Loc.GetString("cmd-dhzsave-failed", ("level", level)));
            }
        }

        shell.WriteLine(snippet.ToString());
    }
}

/// <summary>Маппинг: проставить флаги крыши под этажами стопки.</summary>
[AdminCommand(AdminFlags.Mapping)]
public sealed partial class DHZRoofsCommand : LocalizedEntityCommands
{
    [Dependency] private DHZLevelSystem _z = default!;
    [Dependency] private DHZTransitSystem _transit = default!;

    public override string Command => "dhzroofs";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } player ||
            EntityManager.GetComponent<TransformComponent>(player).MapUid is not { } map ||
            !_z.TryGetLevel(map, out var here))
        {
            shell.WriteError(Loc.GetString("cmd-dhzlevel-no-stack"));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-dhzroofs-done", ("count", _transit.SyncRoofs(here.Stack))));
    }
}
