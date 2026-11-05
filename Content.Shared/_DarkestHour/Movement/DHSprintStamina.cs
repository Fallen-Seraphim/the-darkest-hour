using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Movement;

// Спринт на Shift (решение автора 2026-11-05): по умолчанию шаг, бег — пока зажат Shift, и он тратит выносливость.
// Выдохся — бежать не может (скорость бега = скорость шага), пока не отдышится.

/// <summary>Бегущий выдохся: бег не быстрее шага.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHSprintExhaustedComponent : Component;

public sealed partial class DHSprintExhaustionSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHSprintExhaustedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<DHSprintExhaustedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DHSprintExhaustedComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<DHSprintExhaustedComponent> ent, ref ComponentStartup args)
    {
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnShutdown(Entity<DHSprintExhaustedComponent> ent, ref ComponentShutdown args)
    {
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnRefreshSpeed(Entity<DHSprintExhaustedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running ||
            !TryComp<MovementSpeedModifierComponent>(ent, out var speed) || speed.BaseSprintSpeed <= 0f)
        {
            return;
        }

        args.ModifySpeed(1f, MathF.Min(1f, speed.BaseWalkSpeed / speed.BaseSprintSpeed));
    }
}
