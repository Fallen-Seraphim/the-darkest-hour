using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._DarkestHour.Assault;

/// <summary>Урон авианосцу «Штурма» (проверка падения, пока нет батарей ПКО).</summary>
[AdminCommand(AdminFlags.Admin)]
public sealed partial class DHCarrierDamageCommand : LocalizedEntityCommands
{
    [Dependency] private DHAssaultRuleSystem _assault = default!;

    public override string Command => "dhcarrierdamage";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !float.TryParse(args[0], out var amount) || amount <= 0f)
        {
            shell.WriteError(Loc.GetString("cmd-dhcarrierdamage-help"));
            return;
        }

        if (_assault.DamageCarrier(amount))
            shell.WriteLine(Loc.GetString("cmd-dhcarrierdamage-done", ("amount", amount)));
        else
            shell.WriteError(Loc.GetString("cmd-dhcarrierdamage-no-carrier"));
    }
}
