using Content.Server._DarkestHour.Environment;
using Content.Server.GameTicking;
using Content.Shared._DarkestHour.City;
using Content.Shared._DarkestHour.Environment;
using Content.Shared.CartridgeLoader;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.City;

/// <summary>
/// Приложение «Погода»: что сейчас и что будет по плану погоды карты. Температура — суточный ход (теплее всего
/// к 15:00) минус похолодание от непогоды.
/// </summary>
public sealed partial class DHWeatherAppSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridges = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ServerGameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DHWeatherSystem _weather = default!;

    private const int MaxLines = 7;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHWeatherAppCartridgeComponent, CartridgeUiReadyEvent>(OnReady);
        SubscribeLocalEvent<DHWeatherAppCartridgeComponent, CartridgeMessageEvent>(OnMessage);
    }

    private void OnReady(Entity<DHWeatherAppCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUi(args.Loader);
    }

    private void OnMessage(Entity<DHWeatherAppCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        if (args is DHWeatherAppRefreshMessage)
            UpdateUi(GetEntity(args.LoaderUid));
    }

    private void UpdateUi(EntityUid loader)
    {
        var lines = new List<DHWeatherForecastLine>();
        if (_weather.ScheduleFor(Transform(loader).MapUid) is not { } schedule)
        {
            _cartridges.UpdateCartridgeUiState(loader, new DHWeatherAppUiState(string.Empty, lines));
            return;
        }

        var now = _timing.CurTime;
        var round = _ticker.RoundDuration();
        foreach (var period in schedule.Comp.Plan)
        {
            if (period.End <= now)
                continue;

            var current = period.Start <= now;
            var from = current ? now : period.Start;
            var hazard = Hazard(period.Weather);
            var condition = Loc.GetString(hazard?.Forecast ?? "dh-weather-forecast-clear");
            var middle = from + (period.End - from) / 2;
            var temperature = Temperature(schedule.Comp, round + (middle - now)) - (hazard?.TemperatureShift ?? 0f);
            var time = current
                ? Loc.GetString("dh-weather-app-until", ("time", DHCityClock.TimeText(round + (period.End - now))))
                : $"{DHCityClock.TimeText(round + (period.Start - now))}–{DHCityClock.TimeText(round + (period.End - now))}";

            lines.Add(new DHWeatherForecastLine(time, condition, (int) MathF.Round(temperature), current));
            if (lines.Count >= MaxLines)
                break;
        }

        _cartridges.UpdateCartridgeUiState(loader, new DHWeatherAppUiState(DHCityClock.TimeText(round), lines));
    }

    private DHWeatherHazardComponent? Hazard(EntProtoId? weather)
    {
        if (weather is not { } id || !_proto.TryIndex(id, out var proto))
            return null;

        return proto.TryGetComponent<DHWeatherHazardComponent>(out var hazard, EntityManager.ComponentFactory) ? hazard : null;
    }

    /// <summary>Суточный ход температуры по городским часам: максимум в 15:00, минимум в 3:00.</summary>
    private static float Temperature(DHWeatherScheduleComponent schedule, TimeSpan roundTime)
    {
        var clock = DHCityClock.Now(roundTime);
        var hour = clock.Hour + clock.Minute / 60f;
        return schedule.Temperature + schedule.DailySwing * MathF.Cos((hour - 15f) / 24f * MathF.Tau);
    }
}
