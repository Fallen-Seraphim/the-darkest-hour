using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Movement;

// Рывок на Shift (решение автора 2026-11-05): обычное движение — ванильный бег; пока зажат Shift — рывок быстрее
// бега, и он тратит выносливость. Выдохся — рывка нет (обычный бег), пока не отдышится.

/// <summary>Зажат ли Shift (рывок). Ставится вводом, предсказывается клиентом.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHSprintComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Held;

    /// <summary>Во сколько раз рывок быстрее обычного бега.</summary>
    [DataField, AutoNetworkedField]
    public float Multiplier = 1.4f;
}

/// <summary>Выдохся: рывка нет.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHSprintExhaustedComponent : Component;

public sealed partial class DHSprintExhaustionSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHSprintComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<DHSprintExhaustedComponent, ComponentStartup>(OnExhaustedStartup);
        SubscribeLocalEvent<DHSprintExhaustedComponent, ComponentShutdown>(OnExhaustedShutdown);
    }

    /// <summary>Shift зажат/отпущен.</summary>
    public void SetSprintHeld(EntityUid uid, bool held)
    {
        if (!held && !HasComp<DHSprintComponent>(uid))
            return;

        var sprint = EnsureComp<DHSprintComponent>(uid);
        if (sprint.Held == held)
            return;

        sprint.Held = held;
        Dirty(uid, sprint);
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    public bool IsSprinting(EntityUid uid)
    {
        return TryComp<DHSprintComponent>(uid, out var sprint) && sprint.Held && !HasComp<DHSprintExhaustedComponent>(uid);
    }

    private void OnExhaustedStartup(Entity<DHSprintExhaustedComponent> ent, ref ComponentStartup args)
    {
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnExhaustedShutdown(Entity<DHSprintExhaustedComponent> ent, ref ComponentShutdown args)
    {
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnRefreshSpeed(Entity<DHSprintComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!ent.Comp.Held || TryComp<DHSprintExhaustedComponent>(ent, out var exhausted) &&
            exhausted.LifeStage <= ComponentLifeStage.Running)
        {
            return;
        }

        // Рывок ускоряет обычное движение (ванильный бег); шаг здесь не используется.
        args.ModifySpeed(1f, ent.Comp.Multiplier);
    }
}
