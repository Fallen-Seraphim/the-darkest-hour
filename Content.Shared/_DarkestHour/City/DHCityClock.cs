namespace Content.Shared._DarkestHour.City;

/// <summary>
/// Часы и календарь «Восстания» (решение автора 2026-11-05): события в Лост-Энджелсе — 5 апреля 2318 года, раунд
/// начинается в 9:00 по местному времени. Терминалы, телефоны, банк и документы пишут дату и время по этим часам.
/// </summary>
public static class DHCityClock
{
    public const int Year = 2318;
    public static readonly DateTime Start = new(Year, 4, 5, 9, 0, 0);

    public static DateTime Now(TimeSpan roundDuration)
    {
        return Start + roundDuration;
    }

    public static string DateTimeText(TimeSpan roundDuration)
    {
        return Now(roundDuration).ToString("dd.MM.yyyy HH:mm");
    }

    public static string TimeText(TimeSpan roundDuration)
    {
        return Now(roundDuration).ToString("HH:mm");
    }

    public static string DateText(TimeSpan roundDuration)
    {
        return Now(roundDuration).ToString("dd.MM.yyyy");
    }
}
