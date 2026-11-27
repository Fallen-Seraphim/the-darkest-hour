using Content.Shared.Input;
using Robust.Shared.GameStates;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Assault;

// Палубы челнока (решение автора 2026-11-27, как «Мидуэй» в CMU-14): кабина стоит на стойках на уровень выше земли,
// под ней — нижний ярус с рампой и стойками шасси (уровень земли), над ней — ходовая крыша. Ярусы — отдельные гриды
// на картах соседних Z-уровней, которые ходят вместе с кабиной.

/// <summary>Деталь рампы: видна (и у переборок — твёрдая) только при опущенной рампе.</summary>
[RegisterComponent]
public sealed partial class DHDropshipRampSegmentComponent : Component
{
    /// <summary>При опущенной рампе ещё и непроходима (переборки по бокам рампы).</summary>
    [DataField]
    public bool Solid;

    /// <summary>
    /// Ряд рампы от петель (0) к краю: рампа опускается и поднимается по рядам, как в RuCM (решение автора 2026-11-27).
    /// </summary>
    [DataField]
    public int Stage;
}

/// <summary>
/// Край рампы: кто идёт по опущенной рампе через эту клетку в сторону <see cref="Up"/> (к носу) или вниз (к корме),
/// переходит на другой ярус — из кабины на нижний ярус и обратно.
/// </summary>
[RegisterComponent]
public sealed partial class DHDropshipRampEntryComponent : Component
{
    [DataField]
    public bool Up;
}

/// <summary>Пульт рампы (в кабине и снаружи под кормой): опустить или поднять — только когда челнок на земле.</summary>
[RegisterComponent]
public sealed partial class DHDropshipRampControlComponent : Component;

/// <summary>Стойка шасси: выпускается при посадке.</summary>
[RegisterComponent]
public sealed partial class DHDropshipGearComponent : Component;

[Serializable, NetSerializable]
public enum DHDropshipRampVisuals : byte
{
    Lowered,
}

/// <summary>
/// Пилот ведёт челнок вручную (решение автора 2026-11-27): Q/E — поворот корпуса, R/F — уровень выше/ниже, ниже
/// первого уровня — посадка где угодно. Пока компонент на пилоте, у клиента свой набор клавиш.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHDropshipPilotComponent : Component
{
    /// <summary>Кабина, которой управляет (только сервер).</summary>
    [ViewVariables]
    public EntityUid Ship;

    [ViewVariables]
    public bool RotateLeft;

    [ViewVariables]
    public bool RotateRight;
}

/// <summary>Опустить (true) или поднять рампу.</summary>
[Serializable, NetSerializable]
public sealed class DHDropshipRampMessage(bool lower) : BoundUserInterfaceMessage
{
    public readonly bool Lower = lower;
}

/// <summary>Уровень выше (+1) или ниже (−1) в ручном полёте; ниже первого — посадка здесь.</summary>
[Serializable, NetSerializable]
public sealed class DHDropshipAltitudeMessage(int step) : BoundUserInterfaceMessage
{
    public readonly int Step = step;
}

public static class DHDropshipKeyFunctions
{
    public static readonly BoundKeyFunction Ascend = "DHDropshipAscend";
    public static readonly BoundKeyFunction Descend = "DHDropshipDescend";
}

/// <summary>Нажатие клавиши пилота челнока (на сервере).</summary>
public readonly record struct DHDropshipPilotKey(EntityUid Pilot, BoundKeyFunction Function, bool Down);

/// <summary>
/// Клавиши ручного полёта: поворот — обычные «повернуть шаттл» (Q/E), высота — свои (R/F). Клавиши ловятся на
/// сервере и отдаются системе челноков.
/// </summary>
public sealed partial class DHDropshipInputSystem : EntitySystem
{
    /// <summary>Сервер подписывается, чтобы получать нажатия.</summary>
    public event Action<DHDropshipPilotKey>? KeyPressed;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ShuttleRotateLeft, new KeyHandler(this, ContentKeyFunctions.ShuttleRotateLeft))
            .Bind(ContentKeyFunctions.ShuttleRotateRight, new KeyHandler(this, ContentKeyFunctions.ShuttleRotateRight))
            .Bind(DHDropshipKeyFunctions.Ascend, new KeyHandler(this, DHDropshipKeyFunctions.Ascend))
            .Bind(DHDropshipKeyFunctions.Descend, new KeyHandler(this, DHDropshipKeyFunctions.Descend))
            .Register<DHDropshipInputSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<DHDropshipInputSystem>();
    }

    private void Handle(ICommonSession? session, BoundKeyFunction function, bool down)
    {
        if (session?.AttachedEntity is not { } pilot || !HasComp<DHDropshipPilotComponent>(pilot))
            return;

        KeyPressed?.Invoke(new DHDropshipPilotKey(pilot, function, down));
    }

    private sealed class KeyHandler(DHDropshipInputSystem system, BoundKeyFunction function) : InputCmdHandler
    {
        public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session,
            IFullInputCmdMessage message)
        {
            system.Handle(session, function, message.State == BoundKeyState.Down);
            return false;
        }
    }
}
