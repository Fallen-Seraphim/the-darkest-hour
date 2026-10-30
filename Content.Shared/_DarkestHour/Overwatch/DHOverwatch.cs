using Content.Shared._DarkestHour.Assault;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Overwatch;

/// <summary>
/// Консоль наблюдения («оверватч», по образцу RMC-14): список бойцов своей стороны, их состояние и положение,
/// вид глазами выбранного бойца и приказы по всей стороне.
/// </summary>
[RegisterComponent]
public sealed partial class DHOverwatchConsoleComponent : Component
{
    [DataField]
    public DHAssaultFaction Faction = DHAssaultFaction.Republic;

    /// <summary>Пауза между приказами с этой консоли.</summary>
    [DataField]
    public TimeSpan OrderCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public int MaxOrderLength = 200;

    /// <summary>Кто за консолью → за кем следит (только на сервере).</summary>
    [ViewVariables]
    public Dictionary<EntityUid, EntityUid> Watching = new();

    [ViewVariables]
    public TimeSpan NextOrder;
}

[Serializable, NetSerializable]
public enum DHOverwatchUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum DHOverwatchStatus : byte
{
    Alive,
    Critical,
    Dead,
}

[Serializable, NetSerializable]
public sealed class DHOverwatchEntry
{
    public NetEntity Entity;
    public string Name = string.Empty;
    public string Job = string.Empty;
    public DHOverwatchStatus Status;

    /// <summary>Где боец: грид или «в поле», «в атмосфере».</summary>
    public string Location = string.Empty;

    public int X;
    public int Y;

    /// <summary>Здоровье до критического состояния, 0..100.</summary>
    public int Health;

    public bool Disconnected;

    /// <summary>Боец на поверхности планеты (рисуется на тактической карте).</summary>
    public bool OnSurface;
}

[Serializable, NetSerializable]
public sealed class DHOverwatchState : BoundUserInterfaceState
{
    public DHAssaultFaction Faction;
    public List<DHOverwatchEntry> Fighters;

    /// <summary>Кто за консолью → за кем следит: у каждого зрителя своя камера.</summary>
    public Dictionary<NetEntity, NetEntity> Watching;

    /// <summary>Тактическая карта: карта поверхности, версия её местности и отметки.</summary>
    public NetEntity? TacticalMap;
    public int TerrainVersion;
    public List<DHTacticalMarker> Markers;

    public DHOverwatchState(DHAssaultFaction faction, List<DHOverwatchEntry> fighters,
        Dictionary<NetEntity, NetEntity> watching, NetEntity? tacticalMap, int terrainVersion,
        List<DHTacticalMarker> markers)
    {
        Faction = faction;
        Fighters = fighters;
        Watching = watching;
        TacticalMap = tacticalMap;
        TerrainVersion = terrainVersion;
        Markers = markers;
    }
}

[Serializable, NetSerializable]
public enum DHTacticalMarkerKind : byte
{
    /// <summary>Зона захвата (радиус, владелец).</summary>
    ControlPoint,
    LandingZone,

    /// <summary>Противник, замеченный своими РЛС или детекторами движения.</summary>
    Enemy,
    LaserMark,
    Radar,
}

[Serializable, NetSerializable]
public sealed class DHTacticalMarker
{
    public DHTacticalMarkerKind Kind;
    public float X;
    public float Y;
    public string Name = string.Empty;
    public DHAssaultFaction? Owner;
    public float Radius;
}

/// <summary>Запрос местности тактической карты (клиент кэширует её по версии).</summary>
[Serializable, NetSerializable]
public sealed class DHTacticalMapRequestMessage : BoundUserInterfaceMessage;

/// <summary>
/// Местность тактической карты: по тайлу — <see cref="Content.Shared._DarkestHour.ControlPoints.DHTacticalTerrain"/>,
/// строками снизу вверх, RLE (пары «сколько, что»). Left/Bottom — мировые координаты левого нижнего тайла.
/// </summary>
[Serializable, NetSerializable]
public sealed class DHTacticalMapTerrainEvent : EntityEventArgs
{
    public NetEntity Map;
    public int Version;
    public int Left;
    public int Bottom;
    public int Width;
    public int Height;
    public byte[] Data = Array.Empty<byte>();
}

/// <summary>Смотреть глазами бойца (null — отключиться).</summary>
[Serializable, NetSerializable]
public sealed class DHOverwatchWatchMessage : BoundUserInterfaceMessage
{
    public NetEntity? Target;

    public DHOverwatchWatchMessage(NetEntity? target)
    {
        Target = target;
    }
}

/// <summary>Приказ всем бойцам стороны.</summary>
[Serializable, NetSerializable]
public sealed class DHOverwatchOrderMessage : BoundUserInterfaceMessage
{
    public string Text;

    public DHOverwatchOrderMessage(string text)
    {
        Text = text;
    }
}
