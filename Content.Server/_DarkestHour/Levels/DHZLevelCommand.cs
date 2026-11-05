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
