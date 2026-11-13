using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Flags;

/// <summary>
/// Флаг стороны (решение автора 2026-11-13, по образцу флагов стран CMU/RMC): в руке — «использовать», чтобы
/// водрузить на своей клетке; воткнутый — альт-действие «снять флаг». Флаг Республики, Империи и ОФСК.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHPlantableFlagComponent : Component
{
    [DataField]
    public TimeSpan PlantDelay = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan RemoveDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public SoundSpecifier? RaiseStartSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/flag_raising.ogg");

    [DataField]
    public SoundSpecifier? RaiseEndSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/flag_raised.ogg");

    [DataField]
    public SoundSpecifier? LowerStartSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/flag_lowering.ogg");
}

[Serializable, NetSerializable]
public enum DHPlantableFlagVisuals : byte
{
    Planted,
}

[Serializable, NetSerializable]
public sealed partial class DHPlantFlagDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHRemoveFlagDoAfterEvent : SimpleDoAfterEvent;
