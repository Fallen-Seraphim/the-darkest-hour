using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Recon;

// Маскхалат снайпера (план осады п. 13, решение автора 2026-11-20 — как в RMC): носится на шее поверх брони.
// Способность «Маскировка»: замереть на 3 с — и снайпер почти невидим, в т. ч. на тактической карте противника.
// Шаг, выстрел или ранение раскрывают, способность уходит на перезарядку 20 с. Логика — DHGhillieSystem (сервер).

/// <summary>Маскхалат: даёт способность «Маскировка», пока надет в нужный слот.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHGhillieComponent : Component
{
    [DataField]
    public EntProtoId Action = "DHActionGhillie";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField]
    public SlotFlags RequiredSlot = SlotFlags.NECK;

    /// <summary>Сколько стоять неподвижно, чтобы скрыться.</summary>
    [DataField]
    public TimeSpan PrepareTime = TimeSpan.FromSeconds(3);

    /// <summary>Перезарядка после раскрытия.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(20);

    /// <summary>Видимость скрытого (0 — совсем не видно, 1 — как обычно).</summary>
    [DataField]
    public float Visibility = 0.1f;

    /// <summary>На сколько можно сместиться (толчок, поворот), прежде чем раскроешься, м.</summary>
    [DataField]
    public float MoveTolerance = 0.3f;
}

/// <summary>Носитель скрыт маскхалатом. Тактическая карта противника его не показывает.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHGhillieHiddenComponent : Component
{
    [ViewVariables]
    public EntityUid Suit;

    /// <summary>Где замер (мировые координаты).</summary>
    [ViewVariables]
    public System.Numerics.Vector2 Anchor;
}

public sealed partial class DHGhillieActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class DHGhillieDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Выдача способности маскхалата (общая часть, чтобы клиент видел кнопку сразу).</summary>
public sealed class DHGhillieActionsSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHGhillieComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DHGhillieComponent, GetItemActionsEvent>(OnGetActions);
    }

    private void OnMapInit(Entity<DHGhillieComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
        Dirty(ent);
    }

    private void OnGetActions(Entity<DHGhillieComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags is not { } slots || (slots & ent.Comp.RequiredSlot) == 0)
            return;

        args.AddAction(ent.Comp.ActionEntity);
    }
}
