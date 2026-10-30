using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Clothing.ModularArmor;

// Модульная броня ИМБ-1 / РМБ-1 (этап 6). Герметичный корпус (outerClothing) с пятью слотами модулей (торс, руки,
// ноги) и слотом спецсистемы. Модуль защищает только свою часть тела: часть выбирается до брони (травмы бьют туда же),
// модуль теряет прочность на поглощённом уроне и, разбитый, перестаёт защищать. Лёгкие и композитные модули меняются
// на ходу с задержкой, тяжёлые — только у стойки обслуживания брони. Скорость — произведение штрафов модулей.

[Serializable, NetSerializable]
public enum DHArmorSlot : byte
{
    Torso,
    ArmLeft,
    ArmRight,
    LegLeft,
    LegRight,
    Special,
}

[Serializable, NetSerializable]
public enum DHArmorClass : byte
{
    Light,
    Heavy,
    Composite,
}

[Serializable, NetSerializable]
public enum DHArmorSpecialKind : byte
{
    /// <summary>Автоаптечка: сама вкалывает дозу при тяжёлых ранениях.</summary>
    AutoMed,

    /// <summary>Встроенное оружие: действием выдвигается в руку.</summary>
    Weapon,

    /// <summary>Дополнительное хранилище (в т.ч. для баков огнемёта): открывается действием.</summary>
    Storage,
}

/// <summary>Корпус модульной брони.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHModularArmorComponent : Component
{
    /// <summary>Фракция: модули других фракций не встают (ИМБ-1 и РМБ-1 несовместимы).</summary>
    [DataField(required: true)]
    public string Faction = string.Empty;

    [DataField]
    public EntProtoId SpecialAction = "DHActionArmorSpecial";

    [DataField, AutoNetworkedField]
    public EntityUid? SpecialActionEntity;

    /// <summary>Задержка установки/снятия модуля по классам, секунды.</summary>
    [DataField]
    public Dictionary<DHArmorClass, float> SwapTime = new()
    {
        [DHArmorClass.Light] = 3f,
        [DHArmorClass.Composite] = 5f,
        [DHArmorClass.Heavy] = 8f,
    };

    /// <summary>Модули, которые ставятся при создании корпуса (наборы подклассов).</summary>
    [DataField]
    public Dictionary<DHArmorSlot, EntProtoId> StartingModules = new();
}

/// <summary>Модуль брони.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class DHArmorModuleComponent : Component
{
    [DataField(required: true)]
    public DHArmorSlot Slot;

    [DataField]
    public DHArmorClass Class = DHArmorClass.Light;

    /// <summary>Фракция модуля; пусто — подходит к любому корпусу (спецсистемы).</summary>
    [DataField]
    public string Faction = string.Empty;

    /// <summary>Множитель скорости носителя.</summary>
    [DataField]
    public float Speed = 1f;

    [DataField]
    public float MaxIntegrity = 150f;

    [DataField, AutoNetworkedField]
    public float Integrity = 150f;

    /// <summary>Слой на персонаже (rsi + состояние), рисуется поверх корпуса.</summary>
    [DataField]
    public string? EquippedRsi;

    [DataField]
    public string? EquippedState;

    /// <summary>Сколько прочности восстанавливает одна сварка.</summary>
    [DataField]
    public float RepairAmount = 60f;

    public bool Broken => Integrity <= 0f;
}

/// <summary>Спецсистема (модуль слота <see cref="DHArmorSlot.Special"/>).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHArmorSpecialComponent : Component
{
    [DataField(required: true)]
    public DHArmorSpecialKind Kind;

    // --- оружие
    [DataField]
    public EntProtoId? Weapon;

    [DataField, AutoNetworkedField]
    public EntityUid? WeaponEntity;

    [DataField]
    public string WeaponContainer = "dh_armor_weapon";

    // --- автоаптечка
    [DataField]
    public string Solution = "automed";

    /// <summary>Общий урон носителя, с которого аптечка срабатывает.</summary>
    [DataField]
    public float Threshold = 70f;

    [DataField]
    public float Dose = 10f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    [DataField, AutoNetworkedField]
    public TimeSpan NextUse;
}

/// <summary>Отдельная броня по частям тела вне модулей (шлем — голова).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHPartArmorComponent : Component
{
    [DataField(required: true)]
    public List<string> Parts = new();

    /// <summary>Класс брони (0–3): пули ниже этого класса не пробивают.</summary>
    [DataField]
    public int ArmorClass = 1;
}

/// <summary>Стойка обслуживания брони: рядом с ней ставятся и снимаются тяжёлые модули.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHArmorRackComponent : Component
{
    [DataField]
    public float Range = 2f;
}

/// <summary>Встроенное оружие спецсистемы: из руки не выбрасывается, при снятии брони убирается обратно.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHArmorWeaponItemComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Module;
}

[Serializable, NetSerializable]
public sealed partial class DHArmorModuleInsertDoAfterEvent : DoAfterEvent
{
    [DataField]
    public DHArmorSlot Slot;

    private DHArmorModuleInsertDoAfterEvent()
    {
    }

    public DHArmorModuleInsertDoAfterEvent(DHArmorSlot slot)
    {
        Slot = slot;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class DHArmorModuleRemoveDoAfterEvent : DoAfterEvent
{
    [DataField]
    public DHArmorSlot Slot;

    private DHArmorModuleRemoveDoAfterEvent()
    {
    }

    public DHArmorModuleRemoveDoAfterEvent(DHArmorSlot slot)
    {
        Slot = slot;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class DHArmorModuleRepairDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class DHArmorSpecialActionEvent : InstantActionEvent;

public static class DHModularArmor
{
    /// <summary>
    /// Класс брони модуля (решение автора 2026-10-30): без брони — 0, композитный — 1, лёгкий — 2, тяжёлый — 3.
    /// Пуля с классом бронепробития не ниже класса брони пробивает (полный урон), иначе — заброневая травма.
    /// </summary>
    public static int Level(DHArmorClass cls)
    {
        return cls switch
        {
            DHArmorClass.Composite => 1,
            DHArmorClass.Light => 2,
            DHArmorClass.Heavy => 3,
            _ => 0,
        };
    }

    /// <summary>Доля урона, проходящая через непробитую броню (тупым уроном).</summary>
    public const float BlockedFraction = 0.15f;

    /// <summary>Доля физического урона не от пуль (холодное оружие, взрывы), проходящая через броню.</summary>
    public const float NonBulletFraction = 0.5f;

    /// <summary>Износ модуля при пробитии — доля урона пули.</summary>
    public const float PenetratedWear = 0.5f;

    public static string ContainerId(DHArmorSlot slot) => "dh_armor_" + slot.ToString().ToLowerInvariant();

    public static readonly DHArmorSlot[] Slots =
    {
        DHArmorSlot.Torso, DHArmorSlot.ArmLeft, DHArmorSlot.ArmRight, DHArmorSlot.LegLeft, DHArmorSlot.LegRight,
        DHArmorSlot.Special,
    };

    /// <summary>Какой слот защищает часть тела (категория органа из травм).</summary>
    public static DHArmorSlot? SlotFor(string part)
    {
        return part switch
        {
            "Torso" => DHArmorSlot.Torso,
            "ArmLeft" or "HandLeft" => DHArmorSlot.ArmLeft,
            "ArmRight" or "HandRight" => DHArmorSlot.ArmRight,
            "LegLeft" or "FootLeft" => DHArmorSlot.LegLeft,
            "LegRight" or "FootRight" => DHArmorSlot.LegRight,
            _ => null,
        };
    }
}
