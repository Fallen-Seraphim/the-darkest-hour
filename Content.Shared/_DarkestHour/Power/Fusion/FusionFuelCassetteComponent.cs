using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Power.Fusion;

/// <summary>
/// Fuel cassette for a <see cref="FusionReactorComponent"/>. Burns down while the reactor runs.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FusionFuelCassetteComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Fuel = 100f;

    [DataField, AutoNetworkedField]
    public float MaxFuel = 100f;
}
