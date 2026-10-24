using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Assault;

// Режим «Штурм» (базовый режим, решение автора 2026-10-24): имперский десантный авианосец прыгает из соседней
// системы, штурмует военную базу Республики на планете и должен вывезти экспериментальный образец.

[Serializable, NetSerializable]
public enum DHAssaultFaction : byte
{
    Republic,
    Empire,
}

/// <summary>
/// Экспериментальный образец — цель Империи. Хранится в хранилище штаба базы. Вывезен, если находится на борту
/// авианосца, когда тот уходит из системы.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHExperimentalSampleComponent : Component;
