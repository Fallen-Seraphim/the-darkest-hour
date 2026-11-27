using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Characters;

/// <summary>
/// Место рождения персонажа (решение автора 2026-11-11). Известная планета («Балион») или обобщённый вариант
/// («Четвёртая Волна, север»). Выбор зависит от года рождения (2318 − возраст): мир должен быть уже заселён
/// (<see cref="Founded"/>) и ещё существовать (<see cref="Until"/>). Чьё это было государство в год рождения —
/// по <see cref="Polities"/> (до Коллапса — только ЗГА и ОНК), чьё сейчас — <see cref="Now"/>. Индекс мира тоже по
/// государству в год рождения (решение автора 2026-11-27): при ЗГА — KL-…, потом REP-, EMP-, FED-, ONK-….
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
        return EntryIn(year)?.Polity;
    }

    /// <summary>Индекс мира в этот год («KL-534», «FED-001»); у Солнечной системы и обобщённых вариантов — нет.</summary>
    public string? DesignationIn(int year)
    {
        return EntryIn(year)?.Designation;
    }

    private DHBirthplacePolity? EntryIn(int year)
    {
        DHBirthplacePolity? result = null;
        var best = int.MinValue;
        foreach (var polity in Polities)
        {
            if (polity.From <= year && polity.From >= best)
            {
                best = polity.From;
                result = polity;
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

    /// <summary>Индекс мира при этом государстве (KL-…, REP-…, EMP-…, FED-…, ONK-…).</summary>
    [DataField]
    public string? Designation;
}

public static class DHBirthplaces
{
    /// <summary>Год действия сервера.</summary>
    public const int CurrentYear = 2318;

    public static int BirthYear(int age)
    {
        return CurrentYear - age;
    }

    /// <summary>Название с индексом на этот год: «Балион (KL-433)», «Балион (EMP-001)».</summary>
    public static string Title(DHBirthplacePrototype place, int year)
    {
        var name = Loc.GetString(place.Name);
        return place.DesignationIn(year) is { } designation
            ? Loc.GetString("dh-birthplace-title", ("place", name), ("designation", designation))
            : name;
    }

    /// <summary>«Балион (KL-433) — ЗГА, 2290 г.; ныне — Первая Империя (EMP-001)».</summary>
    public static string Describe(DHBirthplacePrototype place, int birthYear)
    {
        var title = Title(place, birthYear);
        var polity = place.PolityIn(birthYear) is { } p ? Loc.GetString(p) : null;
        var now = place.Now is { } n ? Loc.GetString(n) : null;
        var designation = place.DesignationIn(birthYear);
        var nowDesignation = place.DesignationIn(CurrentYear);

        var text = polity == null
            ? Loc.GetString("dh-birthplace-describe-plain", ("place", title), ("year", birthYear))
            : Loc.GetString("dh-birthplace-describe", ("place", title), ("polity", polity), ("year", birthYear));

        if (now != null && (now != polity || nowDesignation != designation))
        {
            text += nowDesignation != null && nowDesignation != designation
                ? Loc.GetString("dh-birthplace-describe-now-designation", ("now", now), ("designation", nowDesignation))
                : Loc.GetString("dh-birthplace-describe-now", ("now", now));
        }

        return text;
    }
}
