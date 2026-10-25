using System.Numerics;
using Content.Shared.Atmos;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
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

    /// <summary>Биом поверхности: карта поверхности создаётся при первой посадке.</summary>
    [DataField]
    public ProtoId<BiomeTemplatePrototype>? Biome;

    /// <summary>Атмосфера поверхности, моль на тайл. Пусто — вакуум.</summary>
    [DataField]
    public Dictionary<Gas, float> SurfaceGases = new();

    [DataField]
    public float SurfaceTemperature = Atmospherics.T20C;

    /// <summary>Небо слоя атмосферы (прототип parallax). Пусто — DHSky.</summary>
    [DataField]
    public string? Sky;

    /// <summary>Освещение поверхности.</summary>
    [DataField]
    public Color SurfaceLight = Color.FromHex("#9DB4CC");

    /// <summary>
    /// Состав верхних слоёв газового гиганта (доли, нормируются): его набирает газозаборник корабля, пока корабль
    /// в слоях.
    /// </summary>
    [DataField]
    public Dictionary<Gas, float> LayerGases = new();

    /// <summary>Температура у верхней границы облаков, К.</summary>
    [DataField]
    public float LayerTemperature = 120f;

    /// <summary>Температура у нижней границы слоёв, куда может опуститься корабль, К (вглубь гиганта теплеет).</summary>
    [DataField]
    public float LayerTemperatureBottom = 450f;

    /// <summary>Давление у верхней границы облаков, кПа (растёт с глубиной экспоненциально).</summary>
    [DataField]
    public float LayerPressureTop = 100f;

    /// <summary>Давление у нижней границы слоёв, кПа.</summary>
    [DataField]
    public float LayerPressureBottom = 2500f;

    /// <summary>
    /// Радиационный пояс (магнитосфера газового гиганта): пиковая мощность, рад/с. 0 — пояса нет.
    /// </summary>
    [DataField]
    public float BeltRadiation;

    /// <summary>Высота пика пояса над облаками, м.</summary>
    [DataField]
    public float BeltAltitude;

    /// <summary>Полуширина пояса, м.</summary>
    [DataField]
    public float BeltWidth = 1f;
}

public static class DHCelestial
{
    /// <summary>
    /// Мощность радиации тела в точке на расстоянии <paramref name="distance"/> от центра, рад/с: излучение тела,
    /// спадающее с высотой, плюс радиационный пояс магнитосферы. В облаках гиганта атмосфера экранирует —
    /// чем глубже, тем тише.
    /// </summary>
    public static float RadiationAt(DHCelestialBody body, float distance)
    {
        var altitude = distance - body.Radius;
        if (altitude <= 0f)
        {
            var top = SurfaceRadiation(body, 0f) + BeltRadiationAt(body, 0f);
            if (body.EnterableDepth <= 0f)
                return top;

            // Шкала экранирования — четверть глубины слоёв: на дне остаётся ~2 %.
            return top * MathF.Exp(altitude / (body.EnterableDepth * 0.25f));
        }

        return SurfaceRadiation(body, altitude) + BeltRadiationAt(body, altitude);
    }

    private static float SurfaceRadiation(DHCelestialBody body, float altitude)
    {
        if (body.Radiation <= 0f)
            return 0f;

        if (body.RadiationFalloff <= 0f)
            return altitude <= 0f ? body.Radiation : 0f;

        if (altitude >= body.RadiationFalloff)
            return 0f;

        var k = 1f - altitude / body.RadiationFalloff;
        return body.Radiation * k * k;
    }

    private static float BeltRadiationAt(DHCelestialBody body, float altitude)
    {
        if (body.BeltRadiation <= 0f)
            return 0f;

        var x = (altitude - body.BeltAltitude) / MathF.Max(body.BeltWidth, 1f);
        return body.BeltRadiation * MathF.Exp(-x * x);
    }

    /// <summary>Глубина в слоях гиганта, 0 (облака) .. 1 (нижняя граница); null — не в слоях.</summary>
    public static float? LayerDepth(DHCelestialBody body, float distance)
    {
        if (!InGasLayer(body, distance))
            return null;

        return Math.Clamp((body.Radius - distance) / body.EnterableDepth, 0f, 1f);
    }

    /// <summary>Давление слоя на глубине (0..1), кПа: экспоненциальный рост, как в настоящей атмосфере.</summary>
    public static float LayerPressure(DHCelestialBody body, float depth)
    {
        var top = MathF.Max(body.LayerPressureTop, 1f);
        var bottom = MathF.Max(body.LayerPressureBottom, top);
        return top * MathF.Pow(bottom / top, Math.Clamp(depth, 0f, 1f));
    }

    /// <summary>Температура слоя на глубине (0..1), К.</summary>
    public static float LayerTemperatureAt(DHCelestialBody body, float depth)
    {
        return body.LayerTemperature + (body.LayerTemperatureBottom - body.LayerTemperature) * Math.Clamp(depth, 0f, 1f);
    }

    /// <summary>Нижняя граница, ниже которой корабль не опускается.</summary>
    public static float SolidRadius(DHCelestialBody body)
    {
        return MathF.Max(0f, body.Radius - body.EnterableDepth);
    }

    /// <summary>Корабль в верхних слоях тела (между поверхностью облаков и нижней границей).</summary>
    public static bool InGasLayer(DHCelestialBody body, float distance)
    {
        return body.EnterableDepth > 0f && distance < body.Radius && distance >= SolidRadius(body);
    }

    /// <summary>Расстояние до поверхности в читаемом виде: метры или километры.</summary>
    public static string FormatDistance(float meters)
    {
        if (meters < 0f)
            return "0 m";

        return meters < 10000f ? $"{meters:0} m" : $"{meters / 1000f:0.0} km";
    }
}

/// <summary>
/// Грид в слоях газового гиганта: клиенту — облака за бортом и дымка, тем гуще, чем глубже.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHGasLayerComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.Gray;

    /// <summary>0 (облака) .. 1 (нижняя граница).</summary>
    [DataField, AutoNetworkedField]
    public float Depth;

    [DataField, AutoNetworkedField]
    public float Pressure;

    [DataField, AutoNetworkedField]
    public float Temperature;
}
