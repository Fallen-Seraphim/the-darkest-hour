using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Environment;

// Смена дня и ночи (решение автора 2026-11-01, по образцу RussianCM / NC14): свет карты поверхности плавно идёт
// рассвет → утро → полдень → закат → сумерки → ночь. Цвет считает клиент каждый кадр по времени раунда (ничего не
// шлётся по сети, кроме начала отсчёта); сервер держит такой же цвет в MapLight раз в несколько секунд.

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHDayNightCycleComponent : Component
{
    /// <summary>Длина суток.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromMinutes(45);

    /// <summary>Время раунда, от которого считается цикл.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    /// <summary>Какая доля суток уже прошла в момент <see cref="Start"/> (0.25 — восход).</summary>
    [DataField, AutoNetworkedField]
    public float StartFraction = 0.28f;

    /// <summary>Свет планеты в полдень; цвета суток умножаются на него.</summary>
    [DataField, AutoNetworkedField]
    public Color BaseColor = Color.White;

    [DataField, AutoNetworkedField]
    public List<DHDayNightEntry> Entries = new()
    {
        new(0.00f, "#07080F"), // полночь
        new(0.08f, "#0B0C16"), // глубокая ночь
        new(0.17f, "#2E241C"), // предрассветные сумерки
        new(0.25f, "#7A5436"), // восход
        new(0.33f, "#DCA865"), // раннее утро
        new(0.42f, "#FFF4E3"), // утро
        new(0.50f, "#FFFFFF"), // полдень
        new(0.58f, "#F7F7F7"), // после полудня
        new(0.67f, "#F6C07E"), // ближе к вечеру
        new(0.75f, "#DC8F42"), // закат
        new(0.83f, "#5A3A69"), // сумерки
        new(0.92f, "#0E0E1C"), // ночь
    };
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class DHDayNightEntry
{
    [DataField]
    public float Time;

    [DataField]
    public Color Color = Color.White;

    public DHDayNightEntry()
    {
    }

    public DHDayNightEntry(float time, string hex)
    {
        Time = time;
        Color = Color.FromHex(hex);
    }
}

public static class DHDayNight
{
    /// <summary>Доля суток (0–1) в момент <paramref name="now"/>.</summary>
    public static float Fraction(DHDayNightCycleComponent cycle, TimeSpan now)
    {
        var duration = MathF.Max(1f, (float) cycle.Duration.TotalSeconds);
        var elapsed = (float) (now - cycle.Start).TotalSeconds;
        var fraction = cycle.StartFraction + elapsed / duration;
        fraction %= 1f;
        return fraction < 0f ? fraction + 1f : fraction;
    }

    /// <summary>Свет карты в эту долю суток.</summary>
    public static Color GetColor(DHDayNightCycleComponent cycle, float fraction)
    {
        var entries = cycle.Entries;
        if (entries.Count == 0)
            return cycle.BaseColor;

        Color tint;
        if (entries.Count == 1)
        {
            tint = entries[0].Color;
        }
        else
        {
            var next = entries.FindIndex(e => e.Time > fraction);
            DHDayNightEntry from, to;
            float fromTime, toTime;
            if (next == -1)
            {
                from = entries[^1];
                fromTime = from.Time;
                to = entries[0];
                toTime = to.Time + 1f;
            }
            else if (next == 0)
            {
                from = entries[^1];
                fromTime = from.Time - 1f;
                to = entries[0];
                toTime = to.Time;
            }
            else
            {
                from = entries[next - 1];
                fromTime = from.Time;
                to = entries[next];
                toTime = to.Time;
            }

            var t = toTime > fromTime ? Math.Clamp((fraction - fromTime) / (toTime - fromTime), 0f, 1f) : 0f;
            tint = Color.InterpolateBetween(from.Color, to.Color, t);
        }

        var b = cycle.BaseColor;
        return new Color(b.R * tint.R, b.G * tint.G, b.B * tint.B, 1f);
    }
}
