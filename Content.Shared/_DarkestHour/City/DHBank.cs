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
