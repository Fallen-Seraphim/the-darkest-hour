using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Characters;

/// <summary>
/// Место рождения персонажа (решение автора 2026-11-11). Известная планета («Балион») или обобщённый вариант
/// («Четвёртая Волна, север»). Выбор зависит от года рождения (2318 − возраст): мир должен быть уже заселён
/// (<see cref="Founded"/>) и ещё существовать (<see cref="Until"/>). Чьё это было государство в год рождения —
/// по <see cref="Polities"/> (до Коллапса — только ЗГА и ОНК), чьё сейчас — <see cref="Now"/>.
/// </summary>
[Prototype("dhBirthplace")]
public sealed partial class DHBirthplacePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>Группа в списке: «Солнечная система», «Первая Волна»…</summary>
    [DataField(required: true)]
    public LocId Group;

    [DataField]
    public int Order;

    /// <summary>Год заселения: родиться здесь можно с этого года.</summary>
    [DataField]
    public int Founded;

    /// <summary>Последний год, когда здесь ещё рождались (Земля — 2301).</summary>
    [DataField]
    public int? Until;

    /// <summary>Государство по годам: действует запись с наибольшим <c>From</c> ≤ года рождения.</summary>
    [DataField(required: true)]
    public List<DHBirthplacePolity> Polities = new();

    /// <summary>Чей мир сейчас, в 2318 году (если отличается — пишется «ныне …»).</summary>
    [DataField]
    public LocId? Now;

    public bool AvailableIn(int year)
    {
        return year >= Founded && (Until == null || year <= Until);
    }

    public LocId? PolityIn(int year)
    {
        LocId? result = null;
        var best = int.MinValue;
        foreach (var polity in Polities)
        {
            if (polity.From <= year && polity.From >= best)
            {
                best = polity.From;
                result = polity.Polity;
            }
        }

        return result;
    }
}

[DataDefinition]
public sealed partial class DHBirthplacePolity
{
    [DataField]
    public int From;

    [DataField(required: true)]
    public LocId Polity;
}

public static class DHBirthplaces
{
    /// <summary>Год действия сервера.</summary>
    public const int CurrentYear = 2318;

    public static int BirthYear(int age)
    {
        return CurrentYear - age;
    }

    /// <summary>«Балион (ЗГА, 2290 г.); ныне — Первая Империя».</summary>
    public static string Describe(DHBirthplacePrototype place, int birthYear)
    {
        var name = Loc.GetString(place.Name);
        var polity = place.PolityIn(birthYear) is { } p ? Loc.GetString(p) : null;
        var now = place.Now is { } n ? Loc.GetString(n) : null;

        var text = polity == null
            ? Loc.GetString("dh-birthplace-describe-plain", ("place", name), ("year", birthYear))
            : Loc.GetString("dh-birthplace-describe", ("place", name), ("polity", polity), ("year", birthYear));

        if (now != null && now != polity)
            text += Loc.GetString("dh-birthplace-describe-now", ("now", now));

        return text;
    }
}
