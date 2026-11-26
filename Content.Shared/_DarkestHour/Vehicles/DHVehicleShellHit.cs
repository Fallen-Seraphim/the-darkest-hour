using Robust.Shared.Audio;

namespace Content.Shared._DarkestHour.Vehicles;

/// <summary>
/// Тяжёлый снаряд (танковая пушка, полевое орудие): попадание в технику (танк, БТР, грузовик и т. п.) слышно снаружи
/// у машины и внутри её отсека (решение автора 2026-11-26).
/// </summary>
[RegisterComponent]
public sealed partial class DHVehicleShellHitComponent : Component
{
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_DarkestHour/Impacts/shell-hit-armor.ogg");
}
