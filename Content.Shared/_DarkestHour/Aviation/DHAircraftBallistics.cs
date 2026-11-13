using System.Numerics;

namespace Content.Shared._DarkestHour.Aviation;

/// <summary>
/// Баллистика штурмовика — общая для сервера (куда реально ложатся бомбы и очереди) и для баллистического вычислителя
/// в интерфейсе управления вооружением (решение автора 2026-11-13: он в реальном времени показывает на карте точку
/// падения бомб и линию огня пушек по тому, что выбрано на подвесках). Числа здесь — единственный источник правды.
/// </summary>
public static class DHAircraftBallistics
{
    /// <summary>Интервал между бомбами в серии, с: серия ложится цепочкой вдоль курса.</summary>
    public const float SalvoInterval = 0.25f;

    /// <summary>Интервал между пусками управляемых в залпе, с (они идут дольше и не мешают друг другу).</summary>
    public const float GuidedSalvoInterval = 0.6f;

    /// <summary>Смещение очереди контейнера к своему крылу, клеток.</summary>
    public const float PodSideOffset = 1.5f;

    /// <summary>Кассетная: эллипс суббоеприпасов вдоль и поперёк курса (полуоси), клеток.</summary>
    public const float ClusterAlong = 7f;

    public const float ClusterAcross = 3f;

    /// <summary>Напалм: полоса огня вдоль курса — пять очагов через 2.5 клетки.</summary>
    public const float NapalmHalfLength = 5f;

    public const float NapalmStep = 2.5f;

    /// <summary>Магниевая: очаги в круге такого радиуса.</summary>
    public const float MagnesiumRadius = 5f;

    /// <summary>Разброс очередей пушки, клеток.</summary>
    public static float CannonSpread(DHAircraftAltitude altitude) => altitude switch
    {
        DHAircraftAltitude.Low => 0.8f,
        DHAircraftAltitude.Medium => 1.6f,
        _ => 2.6f,
    };

    /// <summary>Рассеивание бомб, клеток.</summary>
    public static float BombScatter(DHAircraftAltitude altitude) => altitude switch
    {
        DHAircraftAltitude.Low => 1f,
        DHAircraftAltitude.Medium => 2.5f,
        _ => 4.5f,
    };

    /// <summary>Время падения бомбы, с (самолёт за это время пролетает упреждение).</summary>
    public static float BombFall(DHAircraftAltitude altitude) => altitude switch
    {
        DHAircraftAltitude.Low => 0.8f,
        DHAircraftAltitude.Medium => 1.3f,
        _ => 1.9f,
    };

    /// <summary>Насколько ПВО опаснее (или безопаснее) на этой высоте.</summary>
    public static float AirDefenceFactor(DHAircraftAltitude altitude) => altitude switch
    {
        DHAircraftAltitude.Low => 1.6f,
        DHAircraftAltitude.Medium => 1f,
        _ => 0.55f,
    };

    public static bool IsGuided(DHAircraftMunition munition)
    {
        return munition is DHAircraftMunition.GuidedBomb or DHAircraftMunition.MissileAT or DHAircraftMunition.MissileHE;
    }

    public static bool IsMissile(DHAircraftMunition munition)
    {
        return munition is DHAircraftMunition.MissileAT or DHAircraftMunition.MissileHE;
    }

    public static bool IsFreeFall(DHAircraftMunition munition)
    {
        return munition is DHAircraftMunition.BombHE or DHAircraftMunition.BombCluster or DHAircraftMunition.BombNapalm or
            DHAircraftMunition.BombMagnesium;
    }

    /// <summary>Направление носа по курсу (Heading.RotateVec(вверх)).</summary>
    public static Vector2 Nose(Angle heading)
    {
        return heading.RotateVec(Vector2.UnitY);
    }

    /// <summary>Узлы 1–2 — левое крыло (-1), 3–4 — правое (+1).</summary>
    public static float PodSide(int slot, int hardpoints)
    {
        return slot < hardpoints / 2 ? -1f : 1f;
    }

    /// <summary>
    /// Центр падения <paramref name="index"/>-й бомбы серии (без случайного рассеивания), если сброс — сейчас:
    /// упреждение по курсу за время падения плюс отставание за интервал серии.
    /// </summary>
    public static Vector2 BombImpact(Vector2 position, Vector2 nose, float speed, DHAircraftAltitude altitude, int index)
    {
        return position + nose * speed * (BombFall(altitude) + index * SalvoInterval);
    }

    /// <summary>Центр попадания очереди контейнера (без разброса).</summary>
    public static Vector2 CannonImpact(Vector2 position, Vector2 nose, float lead, int slot, int hardpoints)
    {
        var right = new Vector2(nose.Y, -nose.X);
        return position + nose * lead + right * PodSide(slot, hardpoints) * PodSideOffset;
    }
}
