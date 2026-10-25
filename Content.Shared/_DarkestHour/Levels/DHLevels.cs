using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Levels;

// Уровни по образцу RMC-14 (у SS14 нет Z-уровней): подземелье — отдельная карта под поверхностью, переходы —
// лестницы/люки и грузовой лифт, связанные общим ключом Link.

/// <summary>Лестница или люк: подняться/спуститься к лестнице с тем же <see cref="Link"/> на другом уровне.</summary>
[RegisterComponent]
public sealed partial class DHLadderComponent : Component
{
    [DataField(required: true)]
    public string Link = string.Empty;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);

    /// <summary>true — ведёт вниз (люк), false — наверх.</summary>
    [DataField]
    public bool Down;
}

/// <summary>Платформа грузового лифта: всё незакреплённое на ней (люди, ящики) переезжает на платформу-пару.</summary>
[RegisterComponent]
public sealed partial class DHLiftComponent : Component
{
    [DataField(required: true)]
    public string Link = string.Empty;

    /// <summary>Половина стороны платформы, м (3x3 тайла — 1.45).</summary>
    [DataField]
    public float HalfSize = 1.45f;

    [DataField]
    public TimeSpan TravelTime = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(4);

    /// <summary>Рейс назначен: отправка в это время (только сервер).</summary>
    [ViewVariables]
    public TimeSpan? DepartAt;

    [ViewVariables]
    public TimeSpan NextUse;
}

/// <summary>Пульт лифта: отправить груз со своей платформы или вызвать с другого уровня.</summary>
[RegisterComponent]
public sealed partial class DHLiftPanelComponent : Component
{
    [DataField(required: true)]
    public string Link = string.Empty;
}

[Serializable, NetSerializable]
public sealed partial class DHLadderDoAfterEvent : SimpleDoAfterEvent;
