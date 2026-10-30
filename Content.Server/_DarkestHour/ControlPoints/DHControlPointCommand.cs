using Content.Server.Administration;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.ControlPoints;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._DarkestHour.ControlPoints;

/// <summary>Сменить владельца зоны захвата: dhcontrolpoint &lt;id&gt; &lt;Republic|Empire|none&gt;.</summary>
[AdminCommand(AdminFlags.Admin)]
public sealed partial class DHControlPointCommand : LocalizedEntityCommands
{
    [Dependency] private DHControlPointSystem _points = default!;

    public override string Command => "dhcontrolpoint";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("cmd-dhcontrolpoint-help"));
            return;
        }

        DHAssaultFaction? owner = null;
        if (!args[1].Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<DHAssaultFaction>(args[1], true, out var faction))
            {
                shell.WriteError(Loc.GetString("cmd-dhcontrolpoint-help"));
                return;
            }

            owner = faction;
        }

        var found = false;
        var query = EntityManager.EntityQueryEnumerator<DHControlPointComponent>();
        while (query.MoveNext(out var uid, out var point))
        {
            if (point.PointId != args[0])
                continue;

            _points.SetOwner((uid, point), owner);
            found = true;
        }

        if (found)
            shell.WriteLine(Loc.GetString("cmd-dhcontrolpoint-done"));
        else
            shell.WriteError(Loc.GetString("cmd-dhcontrolpoint-not-found"));
    }
}
