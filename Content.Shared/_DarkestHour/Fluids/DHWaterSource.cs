using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Fluids;

/// <summary>
/// Природная вода (река, пляж, пруд): из неё набирают воду в ёмкости (вёдра, бутылки, огнетушители) — сколько
/// влезет, запас бесконечный. На воде не горит огонь. Раствора у самой клетки нет: таких клеток на карте тысячи.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHWaterSourceComponent : Component
{
    [DataField]
    public string Reagent = "Water";
}
