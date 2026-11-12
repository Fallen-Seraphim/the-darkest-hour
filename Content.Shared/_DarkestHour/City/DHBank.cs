using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.City;

// «Восстание», деньги (решение автора 2026-11-05): электронные счета на раунд (личные, корпорации LA-V и военпола),
// банковские карты, банкоматы, компьютеры бюджета. Логин и пароль счёта случайные, видны в окне персонажа (C).

/// <summary>Счёт организации.</summary>
[Serializable, NetSerializable]
public enum DHBankOrg : byte
{
    None,
    Vanguard,
    Police,

    /// <summary>ОФСК: счёт подставной фирмы (решение автора 2026-11-12).</summary>
    Front,
}

/// <summary>Банковская карта: номер счёта и владелец. У карт организаций номер — счёта организации.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHBankCardComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Number = string.Empty;

    [DataField, AutoNetworkedField]
    public string Holder = string.Empty;

    /// <summary>Карта организации: при появлении привязывается к её счёту.</summary>
    [DataField]
    public DHBankOrg Org = DHBankOrg.None;
}

/// <summary>Банкомат: карта в слоте, после пароля — снятие и внесение наличных.</summary>
[RegisterComponent]
public sealed partial class DHAtmComponent : Component
{
    public const string CardSlotId = "dh-atm-card";

    /// <summary>Номер счёта, для которого введён пароль (сбрасывается, когда карту вынули).</summary>
    [ViewVariables]
    public string? Authorized;

    [ViewVariables]
    public string? Error;
}

[Serializable, NetSerializable]
public enum DHAtmUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHAtmState(bool hasCard, bool authorized, string number, string holder, int balance, string? error)
    : BoundUserInterfaceState
{
    public readonly bool HasCard = hasCard;
    public readonly bool Authorized = authorized;
    public readonly string Number = number;
    public readonly string Holder = holder;
    public readonly int Balance = balance;
    public readonly string? Error = error;
}

[Serializable, NetSerializable]
public sealed class DHAtmLoginMessage(string password) : BoundUserInterfaceMessage
{
    public readonly string Password = password;
}

[Serializable, NetSerializable]
public sealed class DHAtmWithdrawMessage(int amount) : BoundUserInterfaceMessage
{
    public readonly int Amount = amount;
}

/// <summary>Компьютер бюджета: счёт организации (LA-V или военпол).</summary>
[RegisterComponent]
public sealed partial class DHBudgetComputerComponent : Component
{
    [DataField(required: true)]
    public DHBankOrg Org;
}

[Serializable, NetSerializable]
public enum DHBudgetUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHBudgetState(DHBankOrg org, string number, int balance, List<string> log, int evacCost,
    bool evacAvailable, string? status) : BoundUserInterfaceState
{
    public readonly DHBankOrg Org = org;
    public readonly string Number = number;
    public readonly int Balance = balance;
    public readonly List<string> Log = log;

    /// <summary>Сколько стоит вызов эвакуационного борта (только LA-V).</summary>
    public readonly int EvacCost = evacCost;

    /// <summary>Борт можно вызвать (на площадке его нет, вызов ещё не сделан).</summary>
    public readonly bool EvacAvailable = evacAvailable;

    public readonly string? Status = status;
}

[Serializable, NetSerializable]
public sealed class DHBudgetTransferMessage(string number, int amount) : BoundUserInterfaceMessage
{
    public readonly string Number = number;
    public readonly int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class DHBudgetWithdrawMessage(int amount) : BoundUserInterfaceMessage
{
    public readonly int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class DHBudgetEvacMessage : BoundUserInterfaceMessage;

// ---------------------------------------------------------------- чековая машинка (решение автора 2026-11-12)
// Переносной платёжный терминал «Банка LA-V»: продавец привязывает свою карту (или карту организации), выставляет
// сумму (и кладёт товар в лоток), покупатель вставляет карту и вводит пароль — деньги переводятся, печатаются два чека
// (покупателю и продавцу), товар отдаётся покупателю. По чекам потом отслеживаются продажи (задания LA-V).

/// <summary>Чековая машинка.</summary>
[RegisterComponent]
public sealed partial class DHChequeMachineComponent : Component
{
    public const string CardSlotId = "dh-cheque-card";
    public const string GoodsSlotId = "dh-cheque-goods";

    [ViewVariables]
    public string SellerNumber = string.Empty;

    [ViewVariables]
    public string SellerName = string.Empty;

    [ViewVariables]
    public int Amount;

    /// <summary>Что продаётся, если товара в лотке нет (услуга и т. п.).</summary>
    [ViewVariables]
    public string Description = string.Empty;

    [ViewVariables]
    public string? Error;

    [ViewVariables]
    public string? Status;
}

/// <summary>Чек «Банка LA-V»: данные операции (текст — в бумаге).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHChequeComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Serial;

    [DataField, AutoNetworkedField]
    public string Buyer = string.Empty;

    [DataField, AutoNetworkedField]
    public string BuyerNumber = string.Empty;

    [DataField, AutoNetworkedField]
    public string Seller = string.Empty;

    [DataField, AutoNetworkedField]
    public string SellerNumber = string.Empty;

    [DataField, AutoNetworkedField]
    public int Amount;

    [DataField, AutoNetworkedField]
    public string Goods = string.Empty;

    /// <summary>Прототип проданного товара (пусто — услуга без товара).</summary>
    [DataField, AutoNetworkedField]
    public string GoodsProto = string.Empty;

    [DataField, AutoNetworkedField]
    public string Time = string.Empty;

    /// <summary>Экземпляр продавца (иначе — покупателя).</summary>
    [DataField, AutoNetworkedField]
    public bool SellerCopy;
}

[Serializable, NetSerializable]
public enum DHChequeMachineUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHChequeMachineState(bool hasCard, string cardHolder, string cardNumber, string sellerName,
    string sellerNumber, int amount, string description, string goods, string? error, string? status)
    : BoundUserInterfaceState
{
    public readonly bool HasCard = hasCard;
    public readonly string CardHolder = cardHolder;
    public readonly string CardNumber = cardNumber;
    public readonly string SellerName = sellerName;
    public readonly string SellerNumber = sellerNumber;
    public readonly int Amount = amount;
    public readonly string Description = description;

    /// <summary>Товар в лотке (пусто — нет).</summary>
    public readonly string Goods = goods;

    public readonly string? Error = error;
    public readonly string? Status = status;
}

/// <summary>Карта в слоте становится картой продавца.</summary>
[Serializable, NetSerializable]
public sealed class DHChequeSetSellerMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DHChequeSetAmountMessage(int amount, string description) : BoundUserInterfaceMessage
{
    public readonly int Amount = amount;
    public readonly string Description = description;
}

/// <summary>Покупатель: карта в слоте, пароль — оплатить.</summary>
[Serializable, NetSerializable]
public sealed class DHChequePayMessage(string password) : BoundUserInterfaceMessage
{
    public readonly string Password = password;
}

[Serializable, NetSerializable]
public sealed class DHChequeResetMessage : BoundUserInterfaceMessage;

/// <summary>Покупка по чеку прошла (рассылается всем системам — задания продаж LA-V).</summary>
public sealed class DHChequeIssuedEvent(EntityUid machine, EntityUid buyerUser, string sellerNumber, string buyerNumber,
    int amount, string goodsProto, EntityUid? goods) : EntityEventArgs
{
    public readonly EntityUid Machine = machine;
    public readonly EntityUid BuyerUser = buyerUser;
    public readonly string SellerNumber = sellerNumber;
    public readonly string BuyerNumber = buyerNumber;
    public readonly int Amount = amount;
    public readonly string GoodsProto = goodsProto;
    public readonly EntityUid? Goods = goods;
}
