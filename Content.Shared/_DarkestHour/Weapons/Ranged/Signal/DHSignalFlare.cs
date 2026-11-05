using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Weapons.Ranged.Signal;

/// <summary>
/// Сигнальная ракета в полёте: летит над головами (бьётся только о стены) и вспыхивает над точкой, куда целились
/// (не дальше <see cref="MaxRange"/>), — остаётся гореть <see cref="Burst"/>. Белая освещает, цветные — сигналы.
/// </summary>
[RegisterComponent]
public sealed partial class DHSignalFlareComponent : Component
{
    /// <summary>Что остаётся гореть над точкой.</summary>
    [DataField(required: true)]
    public EntProtoId Burst;

    /// <summary>Цвет ракеты для сообщения («в небе вспыхивает зелёная ракета»).</summary>
    [DataField(required: true)]
    public LocId ColorName;

    [DataField]
    public float MaxRange = 30f;

    /// <summary>Когда вспыхнуть; задаётся при выстреле по расстоянию до прицела.</summary>
    [ViewVariables]
    public TimeSpan? BurstAt;
}

/// <summary>Ракетница: при выстреле передаёт ракете, куда целились.</summary>
[RegisterComponent]
public sealed partial class DHSignalPistolComponent : Component;
