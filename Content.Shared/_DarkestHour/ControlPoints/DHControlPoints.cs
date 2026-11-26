using Content.Shared._DarkestHour.Assault;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.ControlPoints;

// Зоны захвата «Осады»: флагшток в каждой локации карты планеты. Захват флагами (решение автора 2026-11-26): флаг
// стороны воткнут в зоне, чужих флагов нет <see cref="DHControlPointComponent.CaptureTime"/> — точка переходит к этой
// стороне; живые враги рядом не мешают — захват срывают, снимая или ломая флаг. Стоят флаги обеих сторон — точка у
// прежнего хозяина, очков никому. Флаг ЧВК LA-V глушит точку: не захватить, нет очков и эффектов. Поставок с точек
// больше нет. АЭС питает свет и ПВО своей стороны (DHControlPointPoweredComponent), дамба — переправа без ресурса.

/// <summary>Флагшток зоны захвата.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHControlPointComponent : Component
{
    /// <summary>Id зоны: на него ссылаются DHControlPointPowered и карта.</summary>
    [DataField(required: true)]
    public string PointId = string.Empty;

    [DataField(required: true)]
    public LocId PointName;

    /// <summary>Что даёт зона (при осмотре).</summary>
    [DataField]
    public LocId? PointDescription;

    [DataField, AutoNetworkedField]
    public DHAssaultFaction? Holder;

    /// <summary>Радиус зоны: флаги в нём берут точку; рисуется на тактической карте.</summary>
    [DataField]
    public float Radius = 14f;

    /// <summary>Сколько свой флаг должен простоять в зоне без чужих, чтобы точка перешла (решение автора 2026-11-26).</summary>
    [DataField]
    public TimeSpan CaptureTime = TimeSpan.FromSeconds(30);

    /// <summary>В зоне флаги обеих сторон: точка у прежнего хозяина, очков никому.</summary>
    [DataField, AutoNetworkedField]
    public bool Contested;

    /// <summary>В зоне флаг ЧВК LA-V: точка не захватывается, очков и эффектов нет.</summary>
    [DataField, AutoNetworkedField]
    public bool Jammed;

    /// <summary>Чей флаг сейчас берёт точку и с какого времени.</summary>
    [ViewVariables]
    public DHAssaultFaction? Capturing;

    [ViewVariables]
    public TimeSpan CaptureStart;

    /// <summary>Очков и эффектов нет (оспорена или заглушена).</summary>
    public bool Paused => Contested || Jammed;

    /// <summary>Очков командования в минуту, пока точку держат (у точек разная ценность).</summary>
    [DataField]
    public int PointsPerMinute = 3;

    /// <summary>Очки за первый захват точки стороной за раунд (повторный захват той же точки бонуса не даёт).</summary>
    [DataField]
    public int CaptureBonus = 40;

    /// <summary>
    /// Пока точка не у <see cref="JamFaction"/>, её каналы <see cref="JamChannels"/> глушатся: слышно только в
    /// пределах <see cref="JamRange"/> (РЛС/связь гарнизона — общий канал Республики, решение автора 2026-11-19).
    /// </summary>
    [DataField]
    public List<string> JamChannels = new();

    [DataField]
    public DHAssaultFaction JamFaction = DHAssaultFaction.Republic;

    [DataField]
    public float JamRange = 15f;

}

/// <summary>
/// Работает, только пока зона <see cref="Point"/> у стороны <see cref="Faction"/>: свет (питание отключается) и ПВО
/// (перехват не стреляет). АЭС — свет и турели по карте.
/// </summary>
[RegisterComponent]
public sealed partial class DHControlPointPoweredComponent : Component
{
    [DataField(required: true)]
    public string Point = string.Empty;

    [DataField]
    public DHAssaultFaction Faction = DHAssaultFaction.Republic;

    /// <summary>
    /// Резервный дизель (военные объекты «Терминуса», решение автора 2026-11-12): после потери зоны работает ещё
    /// столько, потом гаснет. Ноль — резерва нет. Зону вернули раньше — резерв снова полный.
    /// </summary>
    [DataField]
    public TimeSpan Backup = TimeSpan.Zero;

    /// <summary>
    /// Ещё зоны, которые должна держать <see cref="Faction"/> (цепочка целей, решение автора 2026-11-19): ПВО и РЛС
    /// работают, только пока у Республики и АЭС (с резервом), и сама точка объекта (без резерва).
    /// </summary>
    [DataField]
    public List<string> Requires = new();

    /// <summary>Работает от резерва до этого времени (null — не на резерве).</summary>
    [ViewVariables]
    public TimeSpan? BackupUntil;

    [ViewVariables]
    public bool Disabled;

    /// <summary>Питание уже выставлялось (дальше трогаем только при смене состояния).</summary>
    [ViewVariables]
    public bool Applied;
}

[Serializable, NetSerializable]
public enum DHControlPointVisuals : byte
{
    /// <summary>int: 0 — ничья, 1 — Республика, 2 — Империя, 3 — заглушена флагом LA-V.</summary>
    Owner,
}


/// <summary>Метка местности для тактической карты (вода, глубокая вода).</summary>
[RegisterComponent]
public sealed partial class DHTacticalTerrainComponent : Component
{
    [DataField]
    public DHTacticalTerrain Kind = DHTacticalTerrain.Water;
}

[Serializable, NetSerializable]
public enum DHTacticalTerrain : byte
{
    Empty,
    Ground,
    Floor,
    Road,
    Wall,
    Rock,
    Water,
    DeepWater,
    Trench,
    /// <summary>Трава (умеренная зона за рекой и т. п.) — зелёным, а не цветом пустыни.</summary>
    Grass,
}
