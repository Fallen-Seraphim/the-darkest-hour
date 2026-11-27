// DarkestHour: порт лазерного луча Goobstation из Monolith (Monolith-Station/monolith, AGPL-3.0), правки помечены «DarkestHour».
using System.Numerics;
using Content.Shared.Physics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Goobstation.Weapons.SmartGun;

/// <summary>
/// Activates a laser pointer when wielding an item
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LaserPointerComponent : Component
{
    [DataField]
    // DarkestHour: звук Goobstation под CC-BY-NC — щелчок фонаря.
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Items/flashlight_on.ogg");

    [DataField(customTypeSerializer: typeof(FlagSerializer<CollisionMask>))]
    public int CollisionMask = (int) CollisionGroup.Opaque; // Mono - change to opaque; its a laser

    [DataField]
    public Color TargetedColor = Color.Green;

    [DataField]
    public Color DefaultColor = Color.Red;

    [ViewVariables]
    public TimeSpan LastNetworkEventTime = TimeSpan.Zero;

    [DataField]
    public TimeSpan MaxDelayBetweenNetworkEvents = TimeSpan.FromSeconds(0.5);
}
