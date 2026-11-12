using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;

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

    /// <summary>Как погода называется в прогнозе (приложение «Погода»).</summary>
    [DataField]
    public LocId? Forecast;

    /// <summary>На сколько градусов погода холоднее обычного (в прогнозе).</summary>
    [DataField]
    public float TemperatureShift;
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

    /// <summary>
    /// План погоды наперёд (решение автора 2026-11-06: прогноз в приложении «Погода»): ясные промежутки и непогоды
    /// по порядку, на <see cref="Horizon"/> вперёд. Непогода включается, когда наступает её начало.
    /// </summary>
    [ViewVariables]
    public List<DHWeatherPeriod> Plan = new();

    [DataField]
    public TimeSpan Horizon = TimeSpan.FromHours(3);

    /// <summary>Средняя температура днём, °C (для прогноза).</summary>
    [DataField]
    public float Temperature = 18f;

    /// <summary>Суточный размах: днём теплее на столько, ночью холоднее, °C.</summary>
    [DataField]
    public float DailySwing = 5f;
}

/// <summary>Отрезок плана погоды: с Start до End — Weather (null — ясно).</summary>
public sealed class DHWeatherPeriod
{
    public TimeSpan Start;
    public TimeSpan End;
    public EntProtoId? Weather;
    public bool Applied;
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

/// <summary>
/// Районы грида без погоды (решение автора 2026-11-12: на «Терминусе» за рекой и дамбой умеренный климат — песчаная
/// буря туда не доходит). Прямоугольники в клетках грида: левый нижний включительно, правый верхний — нет.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHWeatherFreeAreaComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<Box2i> Areas = new();

    public bool Contains(Vector2i tile)
    {
        foreach (var area in Areas)
        {
            if (tile.X >= area.Left && tile.X < area.Right && tile.Y >= area.Bottom && tile.Y < area.Top)
                return true;
        }

        return false;
    }
}
