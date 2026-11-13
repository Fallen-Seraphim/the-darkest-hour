// Перенесено из RMC-14 (github.com/RMC-14/RMC-14, MIT): RankPrototype. DarkestHour: имя и сокращение — ключи
// локализации, знаки различия на форме (Insignia, шевроны CMU/cmss13).
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Marines.Roles.Ranks;

/// <summary>
///     Used for cosmetic ranks.
/// </summary>
[Prototype]
public sealed partial class RankPrototype : IPrototype, IInheritingPrototype
{
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<RankPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     The name of the rank (localization key).
    /// </summary>
    [AlwaysPushInheritance]
    [DataField(required: true)]
    public LocId Name { get; set; } = default!;

    /// <summary>
    ///     The shortened version of the rank, shown before the name in chat and on the radio (localization key).
    /// </summary>
    [AlwaysPushInheritance]
    [DataField(required: true)]
    public LocId Prefix { get; set; } = default!;

    /// <summary>
    ///     Pay grade code, e.g. E3, O2, IM4, IFO1.
    /// </summary>
    [AlwaysPushInheritance]
    [DataField]
    public string? Paygrade { get; set; }

    /// <summary>
    ///     DarkestHour: insignia drawn on the worn uniform and outer clothing.
    /// </summary>
    [AlwaysPushInheritance]
    [DataField]
    public SpriteSpecifier.Rsi? Insignia { get; set; }
}
