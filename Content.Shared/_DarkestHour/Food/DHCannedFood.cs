using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Food;

/// <summary>Консерву вскрыли ножом (план осады п. 27).</summary>
[Serializable, NetSerializable]
public sealed partial class DHCanOpenDoAfterEvent : SimpleDoAfterEvent;
