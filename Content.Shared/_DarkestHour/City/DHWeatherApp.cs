using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.City;

// «Восстание», приложение «Погода» (решение автора 2026-11-06): погода Лост-Энджелса сейчас и прогноз на несколько
// часов — по плану погоды карты (DHWeatherSchedule), время — по городским часам.

[RegisterComponent]
public sealed partial class DHWeatherAppCartridgeComponent : Component;

/// <summary>Строка прогноза: промежуток по городским часам, погода, температура.</summary>
[Serializable, NetSerializable]
public sealed class DHWeatherForecastLine(string time, string condition, int temperature, bool current)
{
    public readonly string Time = time;
    public readonly string Condition = condition;
    public readonly int Temperature = temperature;
    public readonly bool Current = current;
}

[Serializable, NetSerializable]
public sealed class DHWeatherAppUiState(string updated, List<DHWeatherForecastLine> lines) : BoundUserInterfaceState
{
    /// <summary>Время обновления по городским часам; пусто — метеослужба недоступна.</summary>
    public readonly string Updated = updated;

    public readonly List<DHWeatherForecastLine> Lines = lines;
}

[Serializable, NetSerializable]
public sealed class DHWeatherAppRefreshMessage : CartridgeMessageEvent;
