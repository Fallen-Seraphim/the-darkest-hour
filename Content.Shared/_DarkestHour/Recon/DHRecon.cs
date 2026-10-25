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

/// <summary>Консоль орбитального скана авианосца.</summary>
[RegisterComponent]
public sealed partial class DHOrbitalScanConsoleComponent : Component
{
    /// <summary>Радиус скана на поверхности, м.</summary>
    [DataField]
    public float Radius = 14f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(3);

    /// <summary>Сколько держатся отметки скана на поверхности.</summary>
    [DataField]
    public TimeSpan BlipLifetime = TimeSpan.FromSeconds(20);

    [ViewVariables]
    public TimeSpan NextScan;
}

[Serializable, NetSerializable]
public enum DHOrbitalScanUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class DHOrbitalScanMarkEntry(int code, Vector2 position)
{
    public readonly int Code = code;
    public readonly Vector2 Position = position;
}

[Serializable, NetSerializable]
public sealed class DHOrbitalScanState(string surface, bool ready, int cooldown, float radius, string status,
    List<DHOrbitalScanMarkEntry> marks) : BoundUserInterfaceState
{
    /// <summary>Поверхность, которую видно с текущего места авианосца; пусто — нечего сканировать.</summary>
    public readonly string Surface = surface;

    public readonly bool Ready = ready;
    public readonly int Cooldown = cooldown;
    public readonly float Radius = radius;
    public readonly string Status = status;
    public readonly List<DHOrbitalScanMarkEntry> Marks = marks;
}

[Serializable, NetSerializable]
public sealed class DHOrbitalScanMessage(Vector2 position) : BoundUserInterfaceMessage
{
    public readonly Vector2 Position = position;
}
