using Content.Shared._DarkestHour.Assault;
using Robust.Shared.Serialization;
using Robust.Shared.Localization;

namespace Content.Shared._DarkestHour.Overwatch;

/// <summary>
/// Консоль наблюдения («оверватч», по образцу RMC-14): список бойцов своей стороны, их состояние и положение,
/// вид глазами выбранного бойца и приказы. С 2026-11-26 — по отрядам, как в RMC/CMU: на консоли выбирают отряд,
/// видят его бойцов, приказ уходит только ему; всей стороне — только с командных консолей и планшетов.
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

    /// <summary>Можно ли отдавать приказы (нейролинк «Тактик» — только смотреть).</summary>
    [DataField]
    public bool CanOrder = true;

    /// <summary>Приказы всей стороне (командование: компьютеры наблюдения, планшеты командира базы и штаба операции).
    /// Иначе — только отряду.</summary>
    [DataField]
    public bool FactionOrders;

    /// <summary>Планшет командира отряда ЧВК LA-V: бойцы и приказы — отряд LA-V, а не сторона.</summary>
    [DataField]
    public bool LAV;

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

    /// <summary>Отряд бойца (ID прототипа; null — вне отрядов): на карте — точка цвета отряда.</summary>
    public string? Squad;

    /// <summary>Командир отряда: на карте точка крупнее и с белой рамкой.</summary>
    public bool SquadLeader;
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

    /// <summary>Отряды стороны для выбора на консоли.</summary>
    public List<DHOverwatchSquad> Squads = new();

    /// <summary>С консоли можно приказывать всей стороне.</summary>
    public bool FactionOrders;

    /// <summary>Планшет LA-V: бойцы — отряд ЧВК.</summary>
    public bool LAV;

    /// <summary>Планшет LA-V: задание отряда и как идёт операция (решение автора 2026-11-26).</summary>
    public string? Status;

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

    /// <summary>Челнок десанта в воздухе над полем боя (свой — всегда, чужой — если его видят РЛС).</summary>
    Dropship,

    /// <summary>Своя техника: значок по типу (<see cref="DHTacticalMarker.Icon"/>).</summary>
    OwnVehicle,

    /// <summary>Свой штурмовик в воздухе: курс — <see cref="DHTacticalMarker.Rotation"/>.</summary>
    Aircraft,

    /// <summary>Контрбатарейная засечка: орудие противника где-то в круге радиусом Radius.</summary>
    CounterBattery,

    /// <summary>Маяк сброса: Империи — включённые свои, Республике — тот, к которому летит груз.</summary>
    DropBeacon,
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

    /// <summary>Значок техники (<see cref="Content.Shared._DarkestHour.Vehicles.DHVehicleMapIcon"/>).</summary>
    public byte Icon;

    /// <summary>Курс (самолёт), радианы.</summary>
    public float Rotation;

    /// <summary>Противник сейчас не виден: отметка «последний раз видели здесь».</summary>
    public bool Stale;

    /// <summary>Противника подсветил ЛЦУ: отметка держится дольше. У маяка сброса — к нему летит груз.</summary>
    public bool Lased;
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

/// <summary>Отряд стороны на консоли наблюдения.</summary>
[Serializable, NetSerializable]
public sealed class DHOverwatchSquad
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public Color Color = Color.White;
}

/// <summary>
/// Приказ: <see cref="Squad"/> — ID отряда, <see cref="NoSquad"/> — бойцам вне отрядов, null — всей стороне.
/// </summary>
/// <summary>Командный планшет с объявлениями: открыть окно объявлений стороне.</summary>
[Serializable, NetSerializable]
public sealed class DHOverwatchOpenAnnouncerMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DHOverwatchOrderMessage : BoundUserInterfaceMessage
{
    public const string NoSquad = "";

    public string Text;
    public string? Squad;

    public DHOverwatchOrderMessage(string text, string? squad = null)
    {
        Text = text;
        Squad = squad;
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
