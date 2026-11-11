using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Vehicles;

// Модули техники (hardpoints), как в CM-SS13 / RMC (решение автора 2026-11-11).
// Каждый модуль — отдельный предмет: колёса или гусеницы, основное и вспомогательное орудие, поддержка (дымовые
// гранатомёты, связь), броня у танков, бортовые пулемёты-порты у БТР. Модуль ставят на машину (предметом по ней) и
// снимают гаечным ключом; у каждого своя прочность — он забирает часть урона по машине, разбитый не работает
// (без колёс машина не едет, без орудия не стреляет), чинят сваркой — на машине или снятым.

public enum DHHardpointSlot : byte
{
    Primary,
    Secondary,
    Support,
    Armor,
    Wheels,
    PortLeft,
    PortRight,
}

/// <summary>Модуль техники (предмет).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHHardpointComponent : Component
{
    [DataField(required: true)]
    public DHHardpointSlot Slot;

    /// <summary>На какие машины встаёт (их <see cref="DHVehicleHardpointsComponent.Family"/>).</summary>
    [DataField(required: true)]
    public List<string> Families = new();

    [DataField]
    public float MaxIntegrity = 300f;

    [DataField, AutoNetworkedField]
    public float Integrity = 300f;

    /// <summary>Доля урона по машине, которая достаётся модулю.</summary>
    [DataField]
    public float DamageShare = 0.1f;

    /// <summary>Состояние слоя машины с этим модулем (целым и разбитым); пусто — модуль снаружи не виден.</summary>
    [DataField]
    public string? VehicleState;

    [DataField]
    public string? DamagedVehicleState;

    /// <summary>Орудие модуля: появляется на машине, пока модуль стоит.</summary>
    [DataField]
    public EntProtoId? Gun;

    /// <summary>Сколько выстрелов было в орудии, когда модуль сняли (вернётся при установке).</summary>
    [DataField]
    public int? StoredAmmo;

    /// <summary>Броня: какую долю урона по машине забирает на себя.</summary>
    [DataField]
    public float ArmorAbsorb;

    /// <summary>Колёса или гусеницы: множитель скорости машины.</summary>
    [DataField]
    public float SpeedMultiplier = 1f;

    /// <summary>Дымовые гранатомёты.</summary>
    [DataField]
    public bool Smoke;

    /// <summary>Сварка за один цикл ремонта.</summary>
    [DataField]
    public float RepairPerCycle = 50f;

    public bool Broken => Integrity <= 0f;
}

/// <summary>Машина с модулями.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHVehicleHardpointsComponent : Component
{
    /// <summary>Модель машины: модули ставятся только на свою.</summary>
    [DataField(required: true)]
    public string Family = string.Empty;

    /// <summary>Какие места под модули есть у машины и слой спрайта каждого (пусто — слоя нет).</summary>
    [DataField(required: true)]
    public Dictionary<DHHardpointSlot, string> Slots = new();

    /// <summary>Модули с завода.</summary>
    [DataField]
    public Dictionary<DHHardpointSlot, EntProtoId> Starting = new();

    [DataField]
    public TimeSpan InstallTime = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan RepairTime = TimeSpan.FromSeconds(3);

    /// <summary>Машина может ехать (колёса стоят и целы).</summary>
    [DataField, AutoNetworkedField]
    public bool CanDrive = true;

    /// <summary>Множитель скорости от колёс или гусениц.</summary>
    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 1f;
}

/// <summary>Состояние слоя модуля на машине: ключ — <see cref="DHHardpointSlot"/>, значение — строка (пусто — скрыт).</summary>
[Serializable, NetSerializable]
public enum DHHardpointVisuals : byte
{
    Primary,
    Secondary,
    Support,
    Armor,
    Wheels,
    PortLeft,
    PortRight,
}

[Serializable, NetSerializable]
public sealed partial class DHHardpointInstallDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHHardpointRemoveDoAfterEvent : DoAfterEvent
{
    [DataField]
    public DHHardpointSlot Slot;

    private DHHardpointRemoveDoAfterEvent()
    {
    }

    public DHHardpointRemoveDoAfterEvent(DHHardpointSlot slot)
    {
        Slot = slot;
    }

    public override DoAfterEvent Clone()
    {
        return this;
    }
}

[Serializable, NetSerializable]
public sealed partial class DHHardpointRepairDoAfterEvent : DoAfterEvent
{
    /// <summary>Место модуля на машине; пусто — чиним снятый модуль (цель — сам модуль).</summary>
    [DataField]
    public DHHardpointSlot? Slot;

    private DHHardpointRepairDoAfterEvent()
    {
    }

    public DHHardpointRepairDoAfterEvent(DHHardpointSlot? slot)
    {
        Slot = slot;
    }

    public override DoAfterEvent Clone()
    {
        return this;
    }
}

public sealed partial class DHVehicleHornActionEvent : InstantActionEvent;

public sealed partial class DHVehicleLightsActionEvent : InstantActionEvent;

/// <summary>Смотровой прибор в отсеке: посмотреть наружу (пока не отойдёшь).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHVehiclePeriscopeComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;
}

/// <summary>Смотрит наружу через смотровой прибор.</summary>
[RegisterComponent]
public sealed partial class DHVehicleViewerComponent : Component
{
    [ViewVariables]
    public EntityUid Vehicle;
}

public static class DHHardpoints
{
    public static string SlotName(DHHardpointSlot slot)
    {
        return slot switch
        {
            DHHardpointSlot.Primary => "primary",
            DHHardpointSlot.Secondary => "secondary",
            DHHardpointSlot.Support => "support",
            DHHardpointSlot.Armor => "armor",
            DHHardpointSlot.Wheels => "wheels",
            DHHardpointSlot.PortLeft => "port_left",
            DHHardpointSlot.PortRight => "port_right",
            _ => "unknown",
        };
    }

    public static DHHardpointVisuals Visual(DHHardpointSlot slot)
    {
        return (DHHardpointVisuals) (byte) slot;
    }

    public static string Container(DHHardpointSlot slot)
    {
        return "dh_hardpoint_" + SlotName(slot);
    }
}
