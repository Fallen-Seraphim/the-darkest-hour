using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Shuttles.Rift;

// Посадка (этап 5, «Штурм»): корабль садится на тело только через консоль посадки. С орбиты (не выше LandingRange)
// спускается в атмосферу — отдельная карта слоя атмосферы с небом вместо звёзд, где корабль висит над поверхностью
// (координаты те же, что на поверхности) и летает на своих двигателях. Из атмосферы садится на поверхность под
// собой или поднимается на орбиту; с поверхности взлетает в атмосферу. Кто выходит с корабля в атмосфере — прыгает
// с парашютом и приземляется под собой или у ближайшего включённого маяка высадки.
// Газозаборник набирает газ, пока корабль в верхних слоях газового гиганта.

[RegisterComponent, NetworkedComponent]
public sealed partial class DHLandingConsoleComponent : Component
{
    /// <summary>Наибольшая высота над поверхностью, с которой можно начать посадку, м.</summary>
    [DataField]
    public float LandingRange = 3000f;

    /// <summary>Половина стороны зоны посадки на карте поверхности, м.</summary>
    [DataField]
    public float ZoneSize = 1500f;

    [ViewVariables]
    public string? SelectedBody;
}

/// <summary>Карта поверхности тела.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHPlanetSurfaceMapComponent : Component
{
    [DataField, AutoNetworkedField]
    public string System = string.Empty;

    [DataField, AutoNetworkedField]
    public string Body = string.Empty;
}

/// <summary>Карта слоя атмосферы тела: небо вместо звёзд, корабли висят над поверхностью (координаты как на ней).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHPlanetAtmosphereMapComponent : Component
{
    [DataField, AutoNetworkedField]
    public string System = string.Empty;

    [DataField, AutoNetworkedField]
    public string Body = string.Empty;
}

/// <summary>Маяк высадки: включённый на поверхности — парашютисты приземляются у него.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHDropBeaconComponent : Component
{
    [DataField]
    public bool Active;

    /// <summary>Парашютисты, выпрыгнувшие не дальше этого (по горизонтали), летят к маяку, м.</summary>
    [DataField]
    public float Range = 80f;
}

[Serializable, NetSerializable]
public enum DHDropBeaconVisuals : byte
{
    Active,
}

/// <summary>Газозаборник: набирает газ в трубу, пока корабль в верхних слоях газового гиганта.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHGasScoopComponent : Component
{
    /// <summary>Набор на нижней границе слоёв, моль в секунду; у поверхности облаков — четверть.</summary>
    [DataField]
    public float MolesPerSecond = 30f;

    /// <summary>Выше этого давления в трубе заборник не качает, кПа.</summary>
    [DataField]
    public float MaxOutputPressure = 4500f;

    [DataField]
    public string OutletNode = "pipe";

    [ViewVariables]
    public float LastMolesPerSecond;
}

[Serializable, NetSerializable]
public enum DHGasScoopVisuals : byte
{
    Working,
}

[Serializable, NetSerializable]
public enum DHLandingUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHLandingBodyEntry(string id, string name, float altitude, bool inRange)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly float Altitude = altitude;
    public readonly bool InRange = inRange;
}

[Serializable, NetSerializable]
public sealed class DHLandingState(
    string? surfaceBody,
    string? atmosphereBody,
    List<DHLandingBodyEntry> bodies,
    string? selectedBody,
    List<DHRiftNavGrid> surfaceGrids,
    float zoneSize,
    float landingRange,
    string status,
    bool canAct) : BoundUserInterfaceState
{
    /// <summary>Имя тела, на поверхности которого стоит корабль; null — корабль в космосе.</summary>
    public readonly string? SurfaceBody = surfaceBody;

    /// <summary>Имя тела, в атмосфере которого висит корабль; null — не в атмосфере.</summary>
    public readonly string? AtmosphereBody = atmosphereBody;

    public readonly List<DHLandingBodyEntry> Bodies = bodies;
    public readonly string? SelectedBody = selectedBody;

    /// <summary>Корабли и постройки на поверхности выбранного тела (или того, где стоит корабль).</summary>
    public readonly List<DHRiftNavGrid> SurfaceGrids = surfaceGrids;

    public readonly float ZoneSize = zoneSize;
    public readonly float LandingRange = landingRange;
    public readonly string Status = status;
    public readonly bool CanAct = canAct;
}

[Serializable, NetSerializable]
public sealed class DHLandingSelectBodyMessage(string body) : BoundUserInterfaceMessage
{
    public readonly string Body = body;
}

[Serializable, NetSerializable]
public sealed class DHLandingDescendMessage(string body, Vector2 position) : BoundUserInterfaceMessage
{
    public readonly string Body = body;
    public readonly Vector2 Position = position;
}

[Serializable, NetSerializable]
public sealed class DHLandingAscendMessage : BoundUserInterfaceMessage;

/// <summary>Из атмосферы — посадка на поверхность под кораблём.</summary>
[Serializable, NetSerializable]
public sealed class DHLandingLandMessage : BoundUserInterfaceMessage;
