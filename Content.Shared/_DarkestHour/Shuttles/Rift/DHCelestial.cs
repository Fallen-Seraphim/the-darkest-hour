using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Shuttles.Rift;

// Небесные тела звёздной системы (этап 5). Тела — не сущности, а данные прототипа системы: карта несёт только id
// системы и начало отсчёта, клиент рисует тела по прототипу. Тела неподвижны; орбиты рисуются линиями вокруг
// родительского тела. Огромные тела не загружаются гридами: долететь до поверхности нельзя (сажает посадочная
// консоль), у звезды и газовых гигантов — радиация, в верхние слои гигантов можно нырнуть за газом.

[Serializable, NetSerializable]
public enum DHCelestialKind : byte
{
    Star,
    GasGiant,
    Planet,
    Moon,
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class DHCelestialBody
{
    /// <summary>Уникальный в пределах системы id: на него ссылаются орбиты и консоли.</summary>
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    [DataField]
    public DHCelestialKind Kind = DHCelestialKind.Planet;

    /// <summary>Центр тела в метрах от начала отсчёта системы.</summary>
    [DataField(required: true)]
    public Vector2 Position;

    /// <summary>Радиус видимой поверхности (у газового гиганта — верхняя граница облаков).</summary>
    [DataField(required: true)]
    public float Radius;

    [DataField]
    public Color Color = Color.Gray;

    /// <summary>Тело, вокруг которого рисуется орбита. Нет — орбиты нет.</summary>
    [DataField]
    public string? OrbitParent;

    /// <summary>Радиация у поверхности, рад/с. 0 — тело не излучает.</summary>
    [DataField]
    public float Radiation;

    /// <summary>Высота над поверхностью, на которой радиация спадает до нуля.</summary>
    [DataField]
    public float RadiationFalloff;

    /// <summary>
    /// Глубина под поверхностью, куда корабль может опуститься (верхние слои газового гиганта). Глубже — тело
    /// выталкивает корабль. 0 — твёрдое тело.
    /// </summary>
    [DataField]
    public float EnterableDepth;

    /// <summary>Тело можно выбрать целью посадки (консоль посадки).</summary>
    [DataField]
    public bool Landable;
}

public static class DHCelestial
{
    /// <summary>Мощность радиации тела в точке на расстоянии <paramref name="distance"/> от центра, рад/с.</summary>
    public static float RadiationAt(DHCelestialBody body, float distance)
    {
        if (body.Radiation <= 0f)
            return 0f;

        var altitude = distance - body.Radius;
        if (altitude <= 0f)
        {
            // В верхних слоях гиганта радиация растёт с глубиной.
            var depth = body.EnterableDepth > 0f ? -altitude / body.EnterableDepth : 1f;
            return body.Radiation * (1f + Math.Clamp(depth, 0f, 1f));
        }

        if (body.RadiationFalloff <= 0f || altitude >= body.RadiationFalloff)
            return 0f;

        var k = 1f - altitude / body.RadiationFalloff;
        return body.Radiation * k * k;
    }

    /// <summary>Нижняя граница, ниже которой корабль не опускается.</summary>
    public static float SolidRadius(DHCelestialBody body)
    {
        return MathF.Max(0f, body.Radius - body.EnterableDepth);
    }

    /// <summary>Расстояние до поверхности в читаемом виде: метры или километры.</summary>
    public static string FormatDistance(float meters)
    {
        if (meters < 0f)
            return "0 m";

        return meters < 10000f ? $"{meters:0} m" : $"{meters / 1000f:0.0} km";
    }
}
