using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._DarkestHour.Movement;

/// <summary>Временное замедление (погода, отход от стены): множитель скорости до <see cref="Until"/>.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHTimedSlowComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Multiplier = 1f;

    [DataField, AutoNetworkedField]
    public TimeSpan Until;
}

public sealed partial class DHTimedSlowSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly List<EntityUid> _expired = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHTimedSlowComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<DHTimedSlowComponent, ComponentShutdown>(OnShutdown);
    }

    /// <summary>
    /// Замедлить на <paramref name="duration"/>: из действующих замедлений берётся сильнейшее и самое долгое.
    /// </summary>
    public void Slow(EntityUid uid, float multiplier, TimeSpan duration)
    {
        var now = _timing.CurTime;
        var slow = EnsureComp<DHTimedSlowComponent>(uid);
        var active = slow.Until > now;
        var newMultiplier = active ? MathF.Min(slow.Multiplier, multiplier) : multiplier;
        var newUntil = active && slow.Until > now + duration ? slow.Until : now + duration;
        if (MathF.Abs(newMultiplier - slow.Multiplier) < 0.001f && newUntil == slow.Until)
            return;

        var changed = MathF.Abs(newMultiplier - slow.Multiplier) >= 0.001f;
        slow.Multiplier = newMultiplier;
        slow.Until = newUntil;
        Dirty(uid, slow);
        if (changed)
            _movement.RefreshMovementSpeedModifiers(uid);
    }

    private void OnRefreshSpeed(Entity<DHTimedSlowComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running || ent.Comp.Until <= _timing.CurTime)
            return;

        args.ModifySpeed(ent.Comp.Multiplier, ent.Comp.Multiplier);
    }

    private void OnShutdown(Entity<DHTimedSlowComponent> ent, ref ComponentShutdown args)
    {
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        _expired.Clear();
        var query = EntityQueryEnumerator<DHTimedSlowComponent>();
        while (query.MoveNext(out var uid, out var slow))
        {
            if (slow.Until <= now)
                _expired.Add(uid);
        }

        foreach (var uid in _expired)
        {
            RemComp<DHTimedSlowComponent>(uid);
        }
    }
}
