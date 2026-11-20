using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.ThirdParty;

// Способности ИИ брони M.A.R.S. 1.5 ассоциативного десантника (план осады п. 28, PR 3). Работают, только пока на
// десантнике надет экзоскелет ЭМБ-5.

/// <summary>Метка цели: красный треугольник над целью (видят десантник и M.A.R.S.), десантник точнее стреляет.</summary>
public sealed partial class DHMarsMarkActionEvent : EntityTargetActionEvent;

/// <summary>Активный скан: жёлтые метки всех живых рядом на несколько секунд, но писк выдаёт десантника.</summary>
public sealed partial class DHMarsScanActionEvent : InstantActionEvent;

/// <summary>Тактическая сеть: наблюдение стороны цели (бойцы и тактическая карта), только смотреть.</summary>
public sealed partial class DHMarsTacMapActionEvent : InstantActionEvent;

/// <summary>Детектор движения брони: вкл/выкл.</summary>
public sealed partial class DHMarsMotionActionEvent : InstantActionEvent;

/// <summary>Взлом двери рядом с десантником: снять болты и открыть.</summary>
public sealed partial class DHMarsHackActionEvent : EntityTargetActionEvent;

/// <summary>Глушение радио вокруг десантника (канал ЗГА не глушится).</summary>
public sealed partial class DHMarsJamActionEvent : InstantActionEvent;

/// <summary>Экстренная инъекция автоаптечки, не дожидаясь порога ранений.</summary>
public sealed partial class DHMarsInjectActionEvent : InstantActionEvent;

/// <summary>Аварийный отход: пока десантник без сознания, экзоскелет сам несёт его к точке.</summary>
public sealed partial class DHMarsCarryActionEvent : WorldTargetActionEvent;

/// <summary>Метка M.A.R.S. над сущностью: видна только <see cref="Viewers"/>.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHMarsMarkComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<NetEntity> Viewers = new();

    [DataField, AutoNetworkedField]
    public TimeSpan Until;

    /// <summary>Метка скана (жёлтая), а не метка цели (красная).</summary>
    [DataField, AutoNetworkedField]
    public bool Scan;

    /// <summary>Кто поставил (сервер).</summary>
    [ViewVariables]
    public EntityUid? Mars;
}

/// <summary>Наведение M.A.R.S.: пока у цели метка, разброс оружия десантника меньше.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHMarsAimComponent : Component
{
    public const float SpreadModifier = 0.7f;

    [DataField, AutoNetworkedField]
    public TimeSpan Until;
}
