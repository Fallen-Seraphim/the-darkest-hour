using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Paradrop;

// Парашюты (решение автора 2026-10-30). Кто выпрыгнул из корабля в атмосфере без парашюта — разбивается.
// Груз (пушки, ящики, поддоны с техникой) садится, только если к нему прицеплено достаточно парашютов.

/// <summary>
/// Парашют. Сложенный нужен бойцу при прыжке (в руках, на поясе, в кармане, на спине — где угодно в снаряжении,
/// но не в рюкзаке) и цепляется к грузу. После прыжка остаётся раскрытым — его можно уложить заново.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHParachuteComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Packed = true;

    [DataField]
    public TimeSpan RepackTime = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan AttachTime = TimeSpan.FromSeconds(3);
}

[Serializable, NetSerializable]
public enum DHParachuteVisuals : byte
{
    Packed,
}

/// <summary>
/// Поддон сброса: на земле распаковывается в <see cref="Spawn"/> (техника). Сам по себе — груз с парашютами.
/// </summary>
[RegisterComponent]
public sealed partial class DHDropPalletComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Spawn;
}

[Serializable, NetSerializable]
public sealed partial class DHAttachParachuteDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHRepackParachuteDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHAirdropDoAfterEvent : SimpleDoAfterEvent;
