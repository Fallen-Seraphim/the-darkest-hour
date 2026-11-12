using Content.Shared.DoAfter;
using Content.Shared.Stacks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Fortifications;

// Складные баррикады и улучшения металлических баррикад — порт механики RMC-14 (MIT; RMCFoldingBarricadeSystem,
// RMCFoldingBarricadeLinkingSystem, RMCUpgradeSystem) под наши укрепления, без навыков и ксеноморфов
// (решение автора 2026-11-12).

#region Улучшения металлических баррикад

/// <summary>
/// Баррикаду можно улучшить: с двумя листами стали в руке — действие «Улучшить…» и выбор варианта (как в CM:
/// композит, биозащита, усиление). Мультитулом улучшение снимается, листы возвращаются.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHBarricadeUpgradeableComponent : Component
{
    [DataField(required: true)]
    public List<DHBarricadeUpgradeEntry> Upgrades = new();

    /// <summary>Материал улучшения (тип стопки) и сколько уходит.</summary>
    [DataField]
    public ProtoId<StackPrototype> Material = "Steel";

    [DataField]
    public int Amount = 2;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);
}

[DataDefinition]
public sealed partial class DHBarricadeUpgradeEntry
{
    /// <summary>Название варианта в меню действий.</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>Во что превращается баррикада.</summary>
    [DataField(required: true)]
    public EntProtoId Result;

    /// <summary>Сообщение после улучшения.</summary>
    [DataField(required: true)]
    public LocId Popup;
}

/// <summary>Улучшенная баррикада: мультитулом возвращается в исходную, листы выпадают.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHBarricadeUpgradedComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Base;

    [DataField]
    public EntProtoId Refund = "SheetSteel1";

    [DataField]
    public int RefundAmount = 2;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);
}

[Serializable, NetSerializable]
public sealed partial class DHBarricadeUpgradeDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int Index;

    private DHBarricadeUpgradeDoAfterEvent()
    {
    }

    public DHBarricadeUpgradeDoAfterEvent(int index)
    {
        Index = index;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class DHBarricadeDowngradeDoAfterEvent : SimpleDoAfterEvent;

#endregion

#region Складная баррикада MB-6

/// <summary>
/// Сложенные баррикады MB-6 (стопка до 3): в руке — использовать, баррикада встаёт на твоей клетке лицом туда, куда
/// смотришь. Урон каждой сложенной баррикады хранится в стопке; сваркой по стопке чинятся все повреждённые.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHFoldingBarricadeStackComponent : Component
{
    [DataField]
    public EntProtoId Deployed = "DHBarricadeFolding";

    [DataField]
    public float MaxDamage = 350;

    /// <summary>Урон сложенных баррикад (по одной на штуку в стопке).</summary>
    [DataField]
    public List<float> StoredDamage = new();

    [DataField]
    public TimeSpan DeployDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public float RepairAmount = 200;

    [DataField]
    public float RepairFuel = 2;

    /// <summary>Время ремонта — за каждую повреждённую баррикаду в стопке.</summary>
    [DataField]
    public TimeSpan RepairDelayPerDamaged = TimeSpan.FromSeconds(10);

    [ViewVariables]
    public bool SuppressCountChange;

    [ViewVariables]
    public List<float> PendingSplitDamage = new();
}

/// <summary>Развёрнутая MB-6: альтернативным действием или перетаскиванием на себя сложить (3 с), ломом — 1,5 с.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHFoldingBarricadeComponent : Component
{
    [DataField]
    public EntProtoId Folded = "DHFoldingBarricade";

    [DataField]
    public float MaxDamage = 350;

    [DataField]
    public TimeSpan CollapseDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan CrowbarCollapseDelay = TimeSpan.FromSeconds(1.5);
}

[Serializable, NetSerializable]
public sealed partial class DHFoldingBarricadeDeployDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHFoldingBarricadeCollapseDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHFoldingBarricadeRepairDoAfterEvent : SimpleDoAfterEvent;

#endregion

#region Складные металлические и пласталевые баррикады (ворота)

/// <summary>
/// Складная баррикада-ворота: открывается и закрывается кликом (только закреплённая). Пласталевые можно сцепить
/// с соседними в ряд (действие «Сцепить»): сцепленные открываются и закрываются вместе.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHFoldingGateComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Linked;

    [DataField, AutoNetworkedField]
    public bool Linkable;

    /// <summary>Состояния слоёв стыка по стороне экрана: сторона → (закрыта/открыта → состояние RSI).</summary>
    [DataField]
    public Dictionary<Direction, Dictionary<DHFoldingGateLinkState, string>> ConnectionStates = new();
}

[Serializable, NetSerializable]
public enum DHFoldingGateLinkLayers : byte
{
    North,
    South,
    East,
    West,
}

[Serializable, NetSerializable]
public enum DHFoldingGateLinkVisuals : byte
{
    North,
    South,
    East,
    West,
}

[Serializable, NetSerializable]
public enum DHFoldingGateLinkState : byte
{
    None,
    Closed,
    Open,
}

#endregion
