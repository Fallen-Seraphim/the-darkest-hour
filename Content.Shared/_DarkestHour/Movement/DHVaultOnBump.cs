using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Movement;

/// <summary>
/// Перила, лёгкие заборы: если идти в них — перелезаешь с разбега (решение автора 2026-11-01, по образцу RussianCM).
/// Баррикады из мешков, окопы, столы этим не помечены и остаются препятствием.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHVaultOnBumpComponent : Component;
