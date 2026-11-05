using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Environment;

// Погода (решение автора 2026-11-01, по образцу RussianCM): ванильная погода (статус-эффект карты) плюс опасности
// для тех, кто под открытым небом, и расписание на карте планеты — ясно, потом буря, потом снова ясно.

/// <summary>Опасность погоды: на погодном статус-эффекте; действует на стоящих под открытым небом.</summary>
[RegisterComponent]
public sealed partial class DHWeatherHazardComponent : Component
{
    /// <summary>Урон в секунду на пике погоды (броня работает как обычно).</summary>
    [DataField]
    public DamageSpecifier? Damage;

    /// <summary>Скорость под открытым небом на пике погоды.</summary>
    [DataField]
    public float SpeedMultiplier = 1f;

    /// <summary>Сообщение тем, кто на карте, когда погода начинается.</summary>
    [DataField]
    public LocId? StartMessage;
}

/// <summary>Расписание погоды карты: ясные промежутки и случайные непогоды между ними.</summary>
[RegisterComponent]
public sealed partial class DHWeatherScheduleComponent : Component
{
    [DataField]
    public List<DHWeatherOption> Options = new()
    {
        new() { Weather = "DHWeatherSandstorm", Weight = 7, MinDuration = TimeSpan.FromMinutes(3), MaxDuration = TimeSpan.FromMinutes(6) },
        new() { Weather = "DHWeatherSandstormHeavy", Weight = 3, MinDuration = TimeSpan.FromMinutes(2), MaxDuration = TimeSpan.FromMinutes(4) },
    };

    /// <summary>Ясная погода между непогодами.</summary>
    [DataField]
    public TimeSpan MinClear = TimeSpan.FromMinutes(8);

    [DataField]
    public TimeSpan MaxClear = TimeSpan.FromMinutes(15);

    /// <summary>Первая непогода — не раньше этого от начала раунда.</summary>
    [DataField]
    public TimeSpan FirstDelay = TimeSpan.FromMinutes(10);

    [ViewVariables]
    public TimeSpan NextChange;
}

[DataDefinition]
public sealed partial class DHWeatherOption
{
    [DataField(required: true)]
    public EntProtoId Weather;

    [DataField]
    public float Weight = 1f;

    [DataField]
    public TimeSpan MinDuration = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan MaxDuration = TimeSpan.FromMinutes(5);
}
