using Content.Shared._DarkestHour.Overwatch;
using Content.Shared.CartridgeLoader;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.City;

// «Восстание», приложение «Карты» (решение автора 2026-11-06): карта поверхности Лост-Энджелса (местность — как у
// тактической карты), где ты сейчас и городские ориентиры (DHMapBeacon). Только поверхность (уровень 0).

/// <summary>
/// Городской ориентир, как станционный маяк: название здания или адреса на тактической карте и в приложении
/// «Карты». Ставится на уровне 0.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHMapBeaconComponent : Component
{
    [DataField(required: true)]
    public LocId BeaconName;
}

[RegisterComponent]
public sealed partial class DHMapAppCartridgeComponent : Component;

[Serializable, NetSerializable]
public sealed class DHMapAppUiState : BoundUserInterfaceState
{
    /// <summary>Карта поверхности (местность — по <see cref="TerrainVersion"/>); null — города нет.</summary>
    public NetEntity? Map;
    public int TerrainVersion;

    /// <summary>Ты в городе: точка на карте; на этажах и под землёй — уровень не 0.</summary>
    public bool Located;
    public float X;
    public float Y;
    public int Level;

    public List<DHTacticalMarker> Beacons = new();
}

/// <summary>Обновить положение (клиент шлёт раз в секунду, пока приложение открыто).</summary>
[Serializable, NetSerializable]
public sealed class DHMapAppRefreshMessage : CartridgeMessageEvent;

/// <summary>Прислать местность карты (клиент кэширует её по версии).</summary>
[Serializable, NetSerializable]
public sealed class DHMapAppTerrainRequestMessage : CartridgeMessageEvent;
