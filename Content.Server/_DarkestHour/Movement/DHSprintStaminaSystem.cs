using Content.Shared._DarkestHour.Movement;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Movement.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Movement;

/// <summary>
/// Рывок тратит выносливость (решение автора 2026-11-05): пока зажат Shift и игрок движется, копится урон
/// выносливости; на 75 % от порога он выдыхается — рывка нет (обычный бег), пока не отдышится до 40 %.
/// </summary>
public sealed partial class DHSprintStaminaSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>Расход выносливости в рывке в секунду (порог обычно 100: ~12 с рывка до одышки).</summary>
    private const float DrainPerSecond = 6f;

    private const float ExhaustAt = 0.75f;
    private const float RecoverAt = 0.4f;

    /// <summary>Быстрее этого — значит, правда движется, а не стоит с зажатым Shift.</summary>
    private const float MinRunSpeed = 1f;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(0.25);
    private TimeSpan _next;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _next)
            return;

        _next = now + Interval;
        var dt = (float) Interval.TotalSeconds;

        var query = EntityQueryEnumerator<DHSprintComponent, InputMoverComponent, StaminaComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var sprint, out var mover, out var stamina, out var physics))
        {
            var exhausted = HasComp<DHSprintExhaustedComponent>(uid);
            var ratio = stamina.CritThreshold > 0f ? stamina.StaminaDamage / stamina.CritThreshold : 0f;

            if (exhausted)
            {
                if (ratio <= RecoverAt)
                    RemComp<DHSprintExhaustedComponent>(uid);
                continue;
            }

            if (!sprint.Held || !mover.HasDirectionalMovement ||
                physics.LinearVelocity.Length() < MinRunSpeed || !_mobState.IsAlive(uid))
            {
                continue;
            }

            _stamina.TakeStaminaDamage(uid, DrainPerSecond * dt, stamina, visual: false);
            if (stamina.StaminaDamage / stamina.CritThreshold >= ExhaustAt)
            {
                EnsureComp<DHSprintExhaustedComponent>(uid);
                _popup.PopupEntity(Loc.GetString("dh-sprint-exhausted"), uid, uid, PopupType.SmallCaution);
            }
        }
    }
}
