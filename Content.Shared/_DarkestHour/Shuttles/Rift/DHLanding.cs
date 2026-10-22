using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Shuttles.Rift;

// Посадка (этап 5): корабль садится на тело только через консоль посадки — подлетает к телу на орбиту (не выше
// LandingRange над поверхностью), выбирает точку на поверхности и спускается. Поверхность — отдельная карта
// с биомом, создаётся при первой посадке. Взлёт возвращает корабль на орбиту над тем же местом.
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
