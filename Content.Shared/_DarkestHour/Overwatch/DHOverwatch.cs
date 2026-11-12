using Content.Shared._DarkestHour.Assault;
using Robust.Shared.Serialization;
using Robust.Shared.Localization;

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

    /// <summary>Цвет приказов вместо цвета стороны (военпол «Восстания» — синий при стороне Республики).</summary>
    [DataField]
    public Color? OrderColor;

    /// <summary>Подпись отправителя ({ $name } — кто за консолью) вместо стандартной.</summary>
    [DataField]
    public LocId? OrderSender;

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

    /// <summary>Z-уровень бойца (0 — земля).</summary>
    public int Level;
}

/// <summary>Уровень тактической карты: карта уровня и версия её местности.</summary>
[Serializable, NetSerializable]
public sealed class DHTacticalLevel
{
    public int Level;
    public NetEntity Map;
    public int Version;
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

    /// <summary>Z-уровни карты (по возрастанию; один — уровней нет).</summary>
    public List<DHTacticalLevel> Levels = new();

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

    /// <summary>Отметка глобального скана (до следующего скана).</summary>
    ScanContact,

    /// <summary>Городской ориентир (DHMapBeacon): здание или адрес, видно всем.</summary>
    Beacon,

    /// <summary>Техника противника, замеченная своими (РЛС, детекторы, бойцы и экипажи в прямой видимости).</summary>
    EnemyVehicle,

    /// <summary>Одиночный боец противника с последнего глобального скана (точка без подписи).</summary>
    ScanBlip,

    /// <summary>Сигнатура с РЛС своего штурмовика в воздухе.</summary>
    AirSignature,
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

    /// <summary>Z-уровень отметки (0 — земля).</summary>
    public int Level;
}

/// <summary>Запрос местности тактической карты (клиент кэширует её по версии). Map — уровень, null — земля.</summary>
[Serializable, NetSerializable]
public sealed class DHTacticalMapRequestMessage : BoundUserInterfaceMessage
{
    public NetEntity? Map;
}

/// <summary>
/// Местность тактической карты: по тайлу — <see cref="Content.Shared._DarkestHour.ControlPoints.DHTacticalTerrain"/>,
/// строками снизу вверх, RLE (пары «сколько, что»). Left/Bottom — мировые координаты левого нижнего тайла.
/// </summary>
[Serializable, NetSerializable]
public sealed class DHTacticalMapTerrainEvent : EntityEventArgs
{
    public NetEntity Map;
    public int Version;

    /// <summary>Z-уровень: на этажах пустота прозрачна (видна земля).</summary>
    public int Level;
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

/// <summary>
/// Приказ с консоли наблюдения на экран бойцов стороны (решение автора 2026-11-05): наверху по центру, шрифтом
/// объявлений, цветом стороны (Империя — жёлтый, Республика — зелёный, Военная Полиция — синий), с отправителем.
/// </summary>
[Serializable, NetSerializable]
public sealed class DHScreenAnnouncementEvent(string sender, string text, Color color) : EntityEventArgs
{
    public readonly string Sender = sender;
    public readonly string Text = text;
    public readonly Color Color = color;
}
