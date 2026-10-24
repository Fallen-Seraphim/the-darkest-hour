using Content.Shared.Antag.Selectors;
using Robust.Shared.Random;

namespace Content.Shared._DarkestHour.Antag;

/// <summary>
/// Ни одного антагониста сам по себе: определение есть только для того, чтобы админ мог назначить игрока этой ролью
/// через меню антагонистов (без лишних гост-ролей).
/// </summary>
public sealed partial class DHNoAntagCount : AntagCountSelector
{
    public override int GetTargetAntagCount(IRobustRandom random, int playerCount)
    {
        return 0;
    }
}
