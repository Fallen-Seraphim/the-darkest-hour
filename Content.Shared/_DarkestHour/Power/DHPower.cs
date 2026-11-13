using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Power;

[Serializable, NetSerializable]
public enum DHDieselGeneratorVisuals : byte
{
    /// <summary>bool: генератор заведён.</summary>
    Running,
}
