using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.City;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Supply;

// Снабжение стороны, как карго-лифт RMC/CMU (решение автора 2026-11-11), но без денег: в каталоге стороны лежит
// n-ное количество ящиков (миномётные мины, боеприпасы и т. д.), их заказывают с компьютера, и лифт поднимает их из
// трюма. Запас пополняется раз в интервал, если сторона держит больше двух точек (POI).

/// <summary>Каталог снабжения стороны.</summary>
[Prototype("dhSupplyCatalog")]
public sealed partial class DHSupplyCatalogPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public DHAssaultFaction Faction = DHAssaultFaction.Republic;

    /// <summary>
    /// Покупка за деньги со счёта организации (грузовой лифт LA-V в «Восстании», решение автора 2026-11-12): запаса
    /// нет, каждая позиция стоит <see cref="DHSupplyEntry.Price"/> кредитов.
    /// </summary>
    [DataField]
    public DHBankOrg Bank = DHBankOrg.None;

    [DataField(required: true)]
    public List<DHSupplyEntry> Entries = new();

    /// <summary>Как часто пополняется запас (если сторона держит больше <see cref="RefillMinPoints"/> точек).</summary>
    [DataField]
    public TimeSpan RefillInterval = TimeSpan.FromMinutes(10);

    /// <summary>Пополнение — только если точек у стороны больше этого числа.</summary>
    [DataField]
    public int RefillMinPoints = 2;
}

[DataDefinition]
public sealed partial class DHSupplyEntry
{
    [DataField(required: true)]
    public string Id = string.Empty;

    /// <summary>Что поднимает лифт (обычно ящик).</summary>
    [DataField(required: true)]
    public EntProtoId Proto;

    /// <summary>Раздел каталога (ключ локализации).</summary>
    [DataField]
    public LocId Category = "dh-supply-category-misc";

    /// <summary>Сколько доступно в начале раунда.</summary>
    [DataField]
    public int Initial = 1;

    /// <summary>Сколько добавляется при пополнении.</summary>
    [DataField]
    public int Refill = 1;

    /// <summary>Больше этого запас не растёт.</summary>
    [DataField]
    public int Max = 5;

    /// <summary>Цена в кредитах (только у каталогов за деньги).</summary>
    [DataField]
    public int Price;
}

/// <summary>Компьютер снабжения: заказ из каталога стороны и управление лифтом.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHSupplyConsoleComponent : Component
{
    [DataField(required: true)]
    public ProtoId<DHSupplyCatalogPrototype> Catalog;

    /// <summary>Как далеко на том же гриде искать лифт.</summary>
    [DataField]
    public float ElevatorRange = 30f;
}

[Serializable, NetSerializable]
public enum DHSupplyElevatorMode : byte
{
    Lowered,
    Raising,
    Raised,
    Lowering,
}

/// <summary>Грузовой лифт 5×5: опущен — яма, поднят — платформа с заказанными ящиками.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHSupplyElevatorComponent : Component
{
    /// <summary>Платформа — квадрат со стороной 2·Radius+1 клеток.</summary>
    [DataField]
    public int Radius = 2;

    [DataField, AutoNetworkedField]
    public DHSupplyElevatorMode Mode = DHSupplyElevatorMode.Lowered;

    /// <summary>Заказанное, что поднимется с лифтом.</summary>
    [DataField]
    public List<EntProtoId> Orders = new();

    [DataField]
    public TimeSpan RaiseTime = TimeSpan.FromSeconds(12.5);

    [DataField]
    public TimeSpan LowerTime = TimeSpan.FromSeconds(12.5);

    [ViewVariables]
    public TimeSpan? DoneAt;

    [DataField]
    public SoundSpecifier RaisingSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/asrs_raising.ogg");

    [DataField]
    public SoundSpecifier LoweringSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/asrs_lowering.ogg");

    /// <summary>Хитбокс, который не пускает на опущенный лифт.</summary>
    [DataField]
    public string PitFixture = "pit";
}

[Serializable, NetSerializable]
public enum DHSupplyElevatorVisuals : byte
{
    Mode,
}

[Serializable, NetSerializable]
public enum DHSupplyElevatorLayers : byte
{
    Base,
}

[Serializable, NetSerializable]
public enum DHSupplyUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHSupplyStockRow
{
    public string Id = string.Empty;
    public string Proto = string.Empty;
    public string Category = string.Empty;
    public int Count;
    public int Price;

    public DHSupplyStockRow(string id, string proto, string category, int count, int price = 0)
    {
        Id = id;
        Proto = proto;
        Category = category;
        Count = count;
        Price = price;
    }
}

[Serializable, NetSerializable]
public sealed class DHSupplyBuiState : BoundUserInterfaceState
{
    public List<DHSupplyStockRow> Rows;
    public bool HasElevator;
    public DHSupplyElevatorMode Mode;
    public int Pending;
    public int Points;
    public int RefillMinPoints;
    public int RefillMinutes;

    /// <summary>Каталог за деньги: показывается баланс счёта, а не запас.</summary>
    public bool Paid;
    public string Account = string.Empty;
    public int Balance;

    public DHSupplyBuiState(List<DHSupplyStockRow> rows, bool hasElevator, DHSupplyElevatorMode mode, int pending,
        int points, int refillMinPoints, int refillMinutes)
    {
        Rows = rows;
        HasElevator = hasElevator;
        Mode = mode;
        Pending = pending;
        Points = points;
        RefillMinPoints = refillMinPoints;
        RefillMinutes = refillMinutes;
    }
}

[Serializable, NetSerializable]
public sealed class DHSupplyOrderMessage : BoundUserInterfaceMessage
{
    public string Id;

    public DHSupplyOrderMessage(string id)
    {
        Id = id;
    }
}

[Serializable, NetSerializable]
public sealed class DHSupplyToggleElevatorMessage : BoundUserInterfaceMessage;
