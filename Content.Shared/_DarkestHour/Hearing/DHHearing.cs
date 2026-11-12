using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Hearing;

// Слух и контузия (решение автора 2026-10-29): громкие выстрелы копят «шум» у стрелка и тех, кто рядом; долгая
// стрельба, выстрел пушки или близкий разрыв — звон в ушах. Пулемёты, крупный калибр и орудия трясут экран
// окружающим. Наушники-гарнитуры (закрытые) вдвое снижают шум.

/// <summary>Своя громкость оружия (иначе — по классу оружия из его id).</summary>
[RegisterComponent]
public sealed partial class DHGunNoiseComponent : Component
{
    /// <summary>Шум одного выстрела стрелку.</summary>
    [DataField]
    public float Noise = 4f;

    /// <summary>Тряска экрана окружающим за выстрел (0 — нет).</summary>
    [DataField]
    public float Shake;

    [DataField]
    public float ShakeRadius = 3f;
}

/// <summary>Защита слуха (закрытые наушники): шум умножается на <see cref="Factor"/>.</summary>
[RegisterComponent]
public sealed partial class DHHearingProtectionComponent : Component
{
    [DataField]
    public float Factor = 0.5f;
}

/// <summary>Звон в ушах до <see cref="EndTime"/>: клиент играет звон, затухающий к концу.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHTinnitusComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;

    [DataField, AutoNetworkedField]
    public TimeSpan StartTime;
}

/// <summary>
/// Контузия от близкого разрыва (решение автора 2026-11-12): вспышка и «муть» в глазах от <see cref="StartTime"/> до
/// <see cref="EndTime"/>, сила — <see cref="Strength"/> (0–1); клиент рисует, затухая к концу.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHConcussionComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan StartTime;

    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;

    [DataField, AutoNetworkedField]
    public float Strength;
}
