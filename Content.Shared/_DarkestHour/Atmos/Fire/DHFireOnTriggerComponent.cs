using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Atmos.Fire;

/// <summary>
/// При срабатывании поджигает пол вокруг (наш огонь, <c>DHFireSystem</c>) — как зажигательная граната морпехов RMC
/// (решение автора 2026-11-18). Горит и без кислорода, гасится водой и пеной.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHFireOnTriggerComponent : BaseXOnTriggerComponent
{
    /// <summary>Радиус пожара, клеток.</summary>
    [DataField]
    public float Radius = 2.5f;

    /// <summary>Сколько горит каждая клетка, секунд (с разбросом ±25%).</summary>
    [DataField]
    public float Duration = 15f;

    /// <summary>Шанс, что клетка в радиусе загорится.</summary>
    [DataField]
    public float Chance = 1f;

    /// <summary>Горючее (его огонь и стойкость к тушению).</summary>
    [DataField]
    public ProtoId<DHFlammableReagentPrototype> Fuel = "Napalm";
}
