using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Recon;

// Разведка и маскировка «Штурма» (обсуждено с автором 2026-10-25, по образцу RMC-14): детектор движения, орбитальный
// скан с авианосца, маскхалат и маскировочная сеть.

/// <summary>Ручной детектор движения: включённый в руках раз в секунду показывает владельцу отметки движущихся рядом.</summary>
[RegisterComponent]
public sealed partial class DHMotionDetectorComponent : Component
{
    [DataField]
    public float Range = 14f;

    /// <summary>Скорость, с которой цель считается движущейся, м/с.</summary>
    [DataField]
    public float MinSpeed = 0.3f;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1.5);

    [ViewVariables]
    public bool Active;

    [ViewVariables]
    public TimeSpan NextScan;
}

[Serializable, NetSerializable]
public enum DHMotionDetectorVisuals : byte
{
    Active,
}

/// <summary>Отметки детектора движения владельцу: мировые координаты на его карте.</summary>
[Serializable, NetSerializable]
public sealed class DHMotionBlipsEvent(List<Vector2> positions) : EntityEventArgs
{
    public readonly List<Vector2> Positions = positions;
}

/// <summary>
/// Маскхалат: надетый (верхняя одежда) делает носителя почти невидимым, пока тот стоит; движение выдаёт.
/// </summary>
[RegisterComponent]
public sealed partial class DHCamoCloakComponent : Component
{
    /// <summary>
    /// Прототип-заготовка с настроенными <c>Stealth</c> и <c>StealthOnMove</c>: их компоненты добавляются носителю.
    /// </summary>
    [DataField]
    public EntProtoId Preset = "DHCamoStealthPreset";
}

/// <summary>Маскировочная сеть: под ней не видно с орбиты (скан её не пробивает), сверху — лоскутная тень.</summary>
[RegisterComponent]
public sealed partial class DHCamoNetComponent : Component
{
    /// <summary>Сколько прикрывает от центра, м.</summary>
    [DataField]
    public float Radius = 1.6f;

    [DataField]
    public EntProtoId Folded = "DHCamoNetFolded";
}

/// <summary>Свёрнутая сеть: растягивается в руке над собой.</summary>
[RegisterComponent]
public sealed partial class DHCamoNetFoldedComponent : Component
{
    [DataField]
    public EntProtoId Net = "DHCamoNet";

    [DataField]
    public TimeSpan DeployTime = TimeSpan.FromSeconds(6);
}

[Serializable, NetSerializable]
public sealed partial class DHCamoNetDeployDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHCamoNetFoldDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Консоль орбитального скана авианосца. Глобальный скан (решение автора 2026-11-04): вся поверхность тела под
/// авианосцем, отчёт — координаты бункера ПКО и базы, а если не под маскировочной сетью — миномёты, пушки, техника и
/// зенитки; пехота — только крупные скопления. Координаты — с небольшим разбросом; по отчёту — метки на тактической
/// карте стороны (до следующего скана).
/// </summary>
[RegisterComponent]
public sealed partial class DHOrbitalScanConsoleComponent : Component
{
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(7.5);

    /// <summary>Разброс координат в отчёте, м (в каждую сторону).</summary>
    [DataField]
    public float Spread = 3f;

    /// <summary>Скопление пехоты: столько бойцов противника…</summary>
    [DataField]
    public int ClusterSize = 15;

    /// <summary>…в круге такого радиуса, м.</summary>
    [DataField]
    public float ClusterRadius = 10f;

    [ViewVariables]
    public TimeSpan NextScan;

    /// <summary>Текст последнего отчёта (показывается на консоли).</summary>
    [ViewVariables]
    public string LastReport = string.Empty;
}

/// <summary>Что видит глобальный скан.</summary>
[Serializable, NetSerializable]
public enum DHScanTargetKind : byte
{
    AntiOrbital,
    Base,
    Mortar,
    FieldGun,
    Vehicle,
    AntiAir,
}

/// <summary>Объект, который попадает в отчёт глобального скана.</summary>
[RegisterComponent]
public sealed partial class DHScanTargetComponent : Component
{
    [DataField(required: true)]
    public DHScanTargetKind Kind;

    /// <summary>Чей объект. Не задан — берётся сторона техники (DHVehicle) или того, кто развернул (миномёт).</summary>
    [DataField]
    public Content.Shared._DarkestHour.Assault.DHAssaultFaction? Faction;

    /// <summary>Виден всегда, даже под маскировочной сетью (база, бункер ПКО).</summary>
    [DataField]
    public bool AlwaysVisible;
}

/// <summary>Отметка глобального скана на тактической карте стороны, которая сканировала.</summary>
[RegisterComponent]
public sealed partial class DHScanContactComponent : Component
{
    [ViewVariables]
    public Content.Shared._DarkestHour.Assault.DHAssaultFaction? Faction;

    [ViewVariables]
    public string Label = string.Empty;
}

[Serializable, NetSerializable]
public enum DHOrbitalScanUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHOrbitalScanState(string surface, bool ready, int cooldown, string status, string report)
    : BoundUserInterfaceState
{
    /// <summary>Поверхность, которую видно с текущего места авианосца; пусто — нечего сканировать.</summary>
    public readonly string Surface = surface;

    public readonly bool Ready = ready;
    public readonly int Cooldown = cooldown;
    public readonly string Status = status;

    /// <summary>Последний отчёт.</summary>
    public readonly string Report = report;
}

[Serializable, NetSerializable]
public sealed class DHOrbitalScanMessage : BoundUserInterfaceMessage;
