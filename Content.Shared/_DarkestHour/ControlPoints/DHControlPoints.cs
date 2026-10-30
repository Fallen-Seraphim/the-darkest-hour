using Content.Shared._DarkestHour.Assault;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.ControlPoints;

// Зоны захвата «Штурма» (решение автора 2026-10-29/30): терминал захвата в каждой локации карты планеты. Захват —
// взаимодействие с терминалом (удержание, нельзя, если в зоне живой противник). Удерживающая сторона раз в
// Interval получает ящик у терминала (деревня — медикаменты, космодром — продвинутые боеприпасы, стройка — мешки и
// стержни); АЭС питает свет и ПВО своей стороны (DHControlPointPoweredComponent), дамба — переправа без ресурса.

/// <summary>Терминал зоны захвата.</summary>
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

    /// <summary>Радиус зоны: противник в нём не даёт захватить; рисуется на тактической карте.</summary>
    [DataField]
    public float Radius = 14f;

    [DataField]
    public TimeSpan CaptureTime = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan Interval = TimeSpan.FromMinutes(5);

    [DataField]
    public List<EntProtoId> RepublicOutput = new();

    [DataField]
    public List<EntProtoId> EmpireOutput = new();

    [ViewVariables]
    public TimeSpan NextOutput;
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

    [ViewVariables]
    public bool Disabled;
}

[Serializable, NetSerializable]
public enum DHControlPointVisuals : byte
{
    /// <summary>int: 0 — ничья, 1 — Республика, 2 — Империя.</summary>
    Owner,
}

[Serializable, NetSerializable]
public sealed partial class DHControlPointCaptureDoAfterEvent : SimpleDoAfterEvent;

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
}
