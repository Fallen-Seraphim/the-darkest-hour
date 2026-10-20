using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Medical.Surgery;

[Serializable, NetSerializable]
public enum DHSurgeryUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHSurgeryStepData
{
    public string Name = string.Empty;
    public string? ToolHint;
    public bool Done;
    public bool Skipped;
}

[Serializable, NetSerializable]
public sealed class DHSurgeryOptionData
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string? Description;
}

[Serializable, NetSerializable]
public sealed class DHSurgeryPartData
{
    public string Category = string.Empty;
    public string Name = string.Empty;
    public bool Present;
    public DHPartState State;

    public string? ActiveId;
    public string? ActiveName;
    public List<DHSurgeryStepData> ActiveSteps = new();

    public List<DHSurgeryOptionData> Options = new();
}

[Serializable, NetSerializable]
public sealed class DHSurgeryBuiState : BoundUserInterfaceState
{
    public string PatientName = string.Empty;
    public List<DHSurgeryPartData> Parts = new();

    /// <summary>Предупреждения: пациент без наркоза, нет маски/перчаток, операция на полу.</summary>
    public List<string> Warnings = new();
}

/// <summary>Хирург выбрал часть тела в окне.</summary>
[Serializable, NetSerializable]
public sealed class DHSurgerySelectPartMessage(string category) : BoundUserInterfaceMessage
{
    public readonly string Category = category;
}

/// <summary>Начать операцию на части тела.</summary>
[Serializable, NetSerializable]
public sealed class DHSurgeryStartMessage(string category, string surgery) : BoundUserInterfaceMessage
{
    public readonly string Category = category;
    public readonly string Surgery = surgery;
}

/// <summary>Сделать следующий шаг предметом в активной руке.</summary>
[Serializable, NetSerializable]
public sealed class DHSurgeryStepMessage(string category) : BoundUserInterfaceMessage
{
    public readonly string Category = category;
}

/// <summary>Прервать операцию (состояние разреза остаётся — его нужно закрыть).</summary>
[Serializable, NetSerializable]
public sealed class DHSurgeryCancelMessage(string category) : BoundUserInterfaceMessage
{
    public readonly string Category = category;
}

[Serializable, NetSerializable]
public sealed partial class DHSurgeryStepDoAfterEvent : DoAfterEvent
{
    [DataField]
    public string Category = string.Empty;

    [DataField]
    public string Surgery = string.Empty;

    [DataField]
    public int Step;

    private DHSurgeryStepDoAfterEvent()
    {
    }

    public DHSurgeryStepDoAfterEvent(string category, string surgery, int step)
    {
        Category = category;
        Surgery = surgery;
        Step = step;
    }

    public override DoAfterEvent Clone() => this;
}
