using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.City;

// «Восстание», уличные автоматы (решение автора 2026-11-05): принимают только республиканские кредиты (наличными).
// Сунул купюры в автомат — они на его счётчике; выбрал товар — списалось; «сдача» возвращает остаток.

/// <summary>Республиканский кредит — валюта Лост-Энджелса.</summary>
public static class DHCityCurrency
{
    public const string Stack = "DHRepublicCredit";
    /// <summary>Сущность купюр (EntProtoId — чтобы не путать перегрузки StackSystem со стеком).</summary>
    public static readonly EntProtoId Cash = "DHRepublicCredit";
}

[DataDefinition]
public sealed partial class DHCityVendorItem
{
    [DataField(required: true)]
    public EntProtoId Id;

    [DataField(required: true)]
    public int Price;
}

/// <summary>Уличный автомат: товары с ценами и внесённые наличные.</summary>
[RegisterComponent]
public sealed partial class DHCityVendorComponent : Component
{
    [DataField(required: true)]
    public List<DHCityVendorItem> Items = new();

    /// <summary>Внесено и ещё не потрачено.</summary>
    [DataField]
    public int Balance;
}

[Serializable, NetSerializable]
public enum DHCityVendorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHCityVendorEntry(string id, string name, int price)
{
    public readonly string Id = id;
    public readonly string Name = name;
    public readonly int Price = price;
}

[Serializable, NetSerializable]
public sealed class DHCityVendorState(int balance, List<DHCityVendorEntry> items) : BoundUserInterfaceState
{
    public readonly int Balance = balance;
    public readonly List<DHCityVendorEntry> Items = items;
}

[Serializable, NetSerializable]
public sealed class DHCityVendorBuyMessage(string id) : BoundUserInterfaceMessage
{
    public readonly string Id = id;
}

[Serializable, NetSerializable]
public sealed class DHCityVendorChangeMessage : BoundUserInterfaceMessage;
