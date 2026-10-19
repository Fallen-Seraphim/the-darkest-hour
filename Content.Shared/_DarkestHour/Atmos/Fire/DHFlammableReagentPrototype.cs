using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Atmos.Fire;

/// <summary>
/// Горючесть реагента: как он горит на полу, в луже и в огнемёте.
/// ID прототипа совпадает с ID реагента (например, <c>WeldingFuel</c>).
/// Механика по образцу огня RMC-14 (MIT), переписана под DarkestHour.
/// </summary>
[Prototype("dhFlammableReagent")]
public sealed partial class DHFlammableReagentPrototype : IPrototype
{
    /// <summary>ID реагента, к которому относится запись.</summary>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Урон Heat в секунду тем, кто стоит в огне (снижается огнезащитой).</summary>
    [DataField]
    public float Intensity = 3f;

    /// <summary>Сколько секунд горит тайл, подожжённый огнемётом.</summary>
    [DataField]
    public float Duration = 10f;

    /// <summary>Сколько секунд горения даёт одна единица реагента в луже.</summary>
    [DataField]
    public float PuddleDurationPerUnit = 0.5f;

    /// <summary>Максимальная дальность струи огнемёта, тайлов.</summary>
    [DataField]
    public int Range = 5;

    /// <summary>Огненные стаки в секунду тем, кто стоит в огне.</summary>
    [DataField]
    public float FireStacks = 1f;

    /// <summary>Струя расплёскивается: после второго тайла горит полоса шириной 3.</summary>
    [DataField]
    public bool Spread;

    /// <summary>Урон Structural в секунду закреплённым объектам на тайле (двери, окна, стены).</summary>
    [DataField]
    public float StructuralDamage;

    /// <summary>Струя не останавливается перед стеной/дверью, а поджигает и её тайл.</summary>
    [DataField]
    public bool BurnsBlockers;

    /// <summary>Температура, которой огонь «облизывает» атмосферу тайла (поджигает горючие газы).</summary>
    [DataField]
    public float HotspotTemperature = 700f;

    /// <summary>Цвет пламени и света.</summary>
    [DataField]
    public Color Color = Color.FromHex("#ee6515");
}
