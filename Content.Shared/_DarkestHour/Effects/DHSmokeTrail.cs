using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Effects;

/// <summary>
/// Дымный шлейф за движущейся сущностью (ЛТЦ, план осады п. 8): клиент сам ставит за ней облачка дыма
/// (<see cref="Puff"/>, клиентские сущности) раз в <see cref="Interval"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHSmokeTrailComponent : Component
{
    [DataField]
    public EntProtoId Puff = "DHSmokeTrailPuff";

    [DataField]
    public float Interval = 0.12f;

    [ViewVariables]
    public float Accumulator;
}

/// <summary>
/// Падающая с неба ЛТЦ (план осады п. 8): на земле видна как яркая точка, которая «приближается» — клиент уменьшает
/// спрайт от <see cref="StartScale"/> до 1 за <see cref="FallTime"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHFallingFlareComponent : Component
{
    [DataField]
    public float StartScale = 2.2f;

    [DataField]
    public float FallTime = 2.5f;

    [ViewVariables]
    public float Elapsed;
}
