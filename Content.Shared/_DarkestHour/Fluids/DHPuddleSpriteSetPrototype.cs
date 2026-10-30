using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._DarkestHour.Fluids;

/// <summary>
/// DarkestHour (порт из Ephemeral Space / Goob Station): свой вид лужи для набора реагентов — вместо сглаженной
/// ванильной лужи рисуется случайный спрайт по объёму: мало — капли, средне — брызги, много — лужа.
/// Так кровь оставляет капельный след за раненым и растекается там, где он лежит.
/// </summary>
[Prototype("dhPuddleSpriteSet")]
public sealed partial class DHPuddleSpriteSetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Реагенты, лужи из которых (по преобладанию в объёме) рисуются этим набором.</summary>
    [DataField(required: true)]
    public List<ProtoId<ReagentPrototype>> Reagents = new();

    [DataField]
    public Color BaseColor = Color.White;

    /// <summary>Красить спрайт в цвет раствора.</summary>
    [DataField]
    public bool Recolor = true;

    [DataField]
    public List<SpriteSpecifier> SmallSprites = new();

    [DataField]
    public List<SpriteSpecifier> MediumSprites = new();

    [DataField]
    public List<SpriteSpecifier> LargeSprites = new();
}

[Serializable, NetSerializable]
public enum DHPuddleVisuals : byte
{
    /// <summary>id <see cref="DHPuddleSpriteSetPrototype"/> или пустая строка — ванильный вид.</summary>
    SpriteSet,
}
