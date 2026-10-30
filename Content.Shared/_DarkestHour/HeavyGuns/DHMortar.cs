using System.Numerics;
using Content.Shared._DarkestHour.Assault;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.HeavyGuns;

// Миномёт и лазерный целеуказатель (ЛЦУ) по образцу RMC-14. Мина летит тем же путём, что и снаряд артиллерии
// (предупреждение в точке падения, перехват ПВО, подрыв и подпалины — DHHeavyGunSystem).

/// <summary>
/// Лазерный целеуказатель: в руках (лучше — взяв двумя руками, как бинокль) наводится на точку в прямой видимости
/// и за несколько секунд ставит метку с номером. Метку видят миномёты и консоли артиллерии своей стороны.
/// </summary>
[RegisterComponent]
public sealed partial class DHLaserDesignatorComponent : Component
{
    [DataField]
    public float Range = 60f;

    [DataField]
    public TimeSpan LaseTime = TimeSpan.FromSeconds(3);

    /// <summary>Сколько метка держится самое большее (пока наводчик держит лазер).</summary>
    [DataField]
    public TimeSpan MarkLifetime = TimeSpan.FromMinutes(3);

    /// <summary>Текущая метка: новая заменяет старую.</summary>
    [ViewVariables]
    public EntityUid? Mark;
}

/// <summary>Метка ЛЦУ на земле.</summary>
[RegisterComponent]
public sealed partial class DHLaserMarkComponent : Component
{
    [ViewVariables]
    public int Code;

    /// <summary>Чья метка; null — вне «Штурма», видна всем.</summary>
    [ViewVariables]
    public DHAssaultFaction? Faction;

    [ViewVariables]
    public EntityUid? Designator;

    /// <summary>Кто держит лазер. Метка живёт, пока он держит целеуказатель в руках и стоит на месте.</summary>
    [ViewVariables]
    public EntityUid? User;

    /// <summary>Где стоял наводчик, когда поставил метку.</summary>
    [ViewVariables]
    public MapCoordinates UserPosition;
}

/// <summary>Развёрнутый миномёт.</summary>
[RegisterComponent]
public sealed partial class DHMortarComponent : Component
{
    [DataField]
    public float MinRange = 12f;

    [DataField]
    public float MaxRange = 90f;

    /// <summary>Рассеивание на максимальной дальности, м (ближе — меньше).</summary>
    [DataField]
    public float Spread = 3f;

    [DataField]
    public TimeSpan FlightTime = TimeSpan.FromSeconds(7);

    [DataField]
    public TimeSpan ReloadTime = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan LoadTime = TimeSpan.FromSeconds(1.5);

    [DataField]
    public TimeSpan FoldTime = TimeSpan.FromSeconds(4);

    /// <summary>Калибр, мм: от него сила взрыва (как у тяжёлых орудий).</summary>
    [DataField]
    public int Caliber = 81;

    [DataField(required: true)]
    public EntProtoId Folded;

    /// <summary>Точка прицеливания (мировые координаты карты миномёта); null — не наведён.</summary>
    [ViewVariables]
    public Vector2? Target;

    [ViewVariables]
    public TimeSpan NextShot;
}

/// <summary>Свёрнутый миномёт: разворачивается в руке перед собой.</summary>
[RegisterComponent]
public sealed partial class DHMortarFoldedComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Mortar;

    [DataField]
    public TimeSpan DeployTime = TimeSpan.FromSeconds(4);
}

/// <summary>Мина: опускается в ствол наведённого миномёта — и сразу выстрел.</summary>
[RegisterComponent]
public sealed partial class DHMortarShellComponent : Component
{
    [DataField]
    public DHWarhead Warhead = DHWarhead.HighExplosive;
}

/// <summary>Под землёй (бункер): миномёт там не стреляет, ЛЦУ не светит в небо.</summary>
[RegisterComponent]
public sealed partial class DHUndergroundMapComponent : Component
{
}

[Serializable, NetSerializable]
public sealed partial class DHLaserDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public NetCoordinates Point;

    private DHLaserDoAfterEvent()
    {
    }

    public DHLaserDoAfterEvent(NetCoordinates target)
    {
        Point = target;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class DHMortarLoadDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHMortarDeployDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHMortarFoldDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum DHMortarUiKey : byte
{
    Key,
}

/// <summary>Метка ЛЦУ в интерфейсе: номер, координаты, тело (для консолей артиллерии), расстояние.</summary>
[Serializable, NetSerializable]
public sealed class DHLaserMarkEntry(int code, Vector2 position, string? body, float distance)
{
    public readonly int Code = code;
    public readonly Vector2 Position = position;
    public readonly string? Body = body;
    public readonly float Distance = distance;
}

[Serializable, NetSerializable]
public sealed class DHMortarState(
    Vector2 position,
    Vector2? target,
    float minRange,
    float maxRange,
    List<DHLaserMarkEntry> marks,
    string status) : BoundUserInterfaceState
{
    public readonly Vector2 Position = position;
    public readonly Vector2? Target = target;
    public readonly float MinRange = minRange;
    public readonly float MaxRange = maxRange;
    public readonly List<DHLaserMarkEntry> Marks = marks;
    public readonly string Status = status;
}

/// <summary>Навести миномёт на точку (мировые координаты).</summary>
[Serializable, NetSerializable]
public sealed class DHMortarAimMessage(Vector2 target) : BoundUserInterfaceMessage
{
    public readonly Vector2 Target = target;
}
