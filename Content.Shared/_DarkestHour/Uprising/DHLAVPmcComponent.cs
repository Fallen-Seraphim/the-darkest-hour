using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Uprising;

/// <summary>
/// Боец ЧВК LA-V (гост-роль, прилетает бортом «Аэронавтики» по вызову администрации): при занятии роли получает
/// вводную — вывезти представителя LA-V любой ценой, остальных — по возможности — и становится бойцом стороны LA-V.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHLAVPmcComponent : Component
{
    [DataField]
    public LocId Briefing = "dh-uprising-briefing-pmc";

    [ViewVariables]
    public bool Briefed;
}
