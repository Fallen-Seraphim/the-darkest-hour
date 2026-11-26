using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Vehicles;

/// <summary>
/// Повреждения техники на спрайте, как в RMC-14 / CM-SS13 (решение автора 2026-11-26): ниже 90 % прочности поверх
/// корпуса, башни и модулей проступают слои damaged_* — чем сильнее разбито, тем они плотнее (от 10 % до 100 %).
/// Корпус и башня — по прочности машины, модуль — по своей прочности (снят — слоя нет).
/// </summary>
[RegisterComponent]
public sealed partial class DHVehicleDamageVisualsComponent : Component
{
    /// <summary>Слои, проступающие по прочности корпуса (damaged_frame, damaged_turret).</summary>
    [DataField]
    public List<string> Hull = new();

    /// <summary>Слои повреждений модулей по местам (damaged_hardpt_*).</summary>
    [DataField]
    public Dictionary<DHHardpointSlot, string> Hardpoints = new();

    /// <summary>Выше этой доли прочности повреждений не видно.</summary>
    [DataField]
    public float ShowThreshold = 0.9f;

    [DataField]
    public float MinAlpha = 0.1f;
}

/// <summary>Доля прочности корпуса (float, 0–1).</summary>
[Serializable, NetSerializable]
public enum DHVehicleDamageVisuals : byte
{
    Hull,
}

/// <summary>Доля прочности модуля на месте (float, 0–1); ключ — как у <see cref="DHHardpointSlot"/>.</summary>
[Serializable, NetSerializable]
public enum DHHardpointDamageVisuals : byte
{
    Primary,
    Secondary,
    Support,
    Armor,
    Wheels,
    PortLeft,
    PortRight,
}

public static class DHVehicleDamage
{
    /// <summary>Шаг, до которого округляется доля прочности: не гонять внешний вид на каждую пулю.</summary>
    public const float Step = 0.05f;

    public static float Quantize(float fraction)
    {
        return MathF.Round(Math.Clamp(fraction, 0f, 1f) / Step) * Step;
    }

    public static DHHardpointDamageVisuals Visual(DHHardpointSlot slot)
    {
        return (DHHardpointDamageVisuals) (byte) slot;
    }
}
