using System.Numerics;
using Content.Shared._DarkestHour.Movement;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.Movement.Components;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Movement;

/// <summary>
/// Перелезание с разбега: игрок, который идёт в перила или лёгкий забор (<see cref="DHVaultOnBumpComponent"/>) лицом
/// к ним, начинает перелезать, как через ванильное «перелезть». Отошёл — отменяется, как обычно. Тонкие перила легко
/// уже касаться, поэтому, пока игрок движется, проверяются и текущие касания.
/// </summary>
public sealed partial class DHVaultOnBumpSystem : EntitySystem
{
    [Dependency] private ClimbSystem _climb = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(0.2);

    /// <summary>Насколько прямо нужно смотреть на перила, чтобы не перелезать, идя вдоль.</summary>
    private const float FacingDot = 0.5f;

    private readonly Dictionary<EntityUid, TimeSpan> _nextTry = new();
    private readonly HashSet<EntityUid> _contacts = new();
    private TimeSpan _nextPoll;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClimbingComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<ClimbingComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<ClimbingComponent> ent, ref ComponentShutdown args)
    {
        _nextTry.Remove(ent);
    }

    private void OnStartCollide(Entity<ClimbingComponent> ent, ref StartCollideEvent args)
    {
        if (HasComp<ActorComponent>(ent) && WantsVault(ent))
            TryVault(ent, args.OtherEntity);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextPoll)
            return;

        _nextPoll = now + PollInterval;
        foreach (var session in _player.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { } mob ||
                !TryComp(mob, out ClimbingComponent? climbing) ||
                !WantsVault((mob, climbing)))
            {
                continue;
            }

            _contacts.Clear();
            _physics.GetContactingEntities(mob, _contacts);
            foreach (var other in _contacts)
            {
                if (TryVault((mob, climbing), other))
                    break;
            }
        }
    }

    private bool WantsVault(Entity<ClimbingComponent> ent)
    {
        return !ent.Comp.IsClimbing &&
               ent.Comp.DoAfter == null &&
               TryComp(ent, out InputMoverComponent? mover) &&
               mover.HasDirectionalMovement;
    }

    private bool TryVault(Entity<ClimbingComponent> ent, EntityUid climbable)
    {
        if (!HasComp<DHVaultOnBumpComponent>(climbable) ||
            !TryComp(climbable, out ClimbableComponent? climbableComp) ||
            !climbableComp.Vaultable ||
            !Facing(ent, climbable))
        {
            return false;
        }

        var now = _timing.CurTime;
        if (_nextTry.TryGetValue(ent, out var next) && now < next)
            return false;

        _nextTry[ent] = now + Cooldown;

        // Нельзя прямо сейчас — молча, без всплывающего объяснения на каждый толчок.
        if (!_climb.CanVault(climbableComp, ent, climbable, out _))
            return false;

        return _climb.TryClimb(ent, ent, climbable, out _, climbableComp, ent.Comp);
    }

    private bool Facing(EntityUid mob, EntityUid climbable)
    {
        var facing = _transform.GetWorldRotation(mob).ToWorldVec();
        var toTarget = _physics.GetWorldAABB(climbable).Center - _transform.GetWorldPosition(mob);
        if (toTarget.LengthSquared() < 0.0001f)
            return true;

        return Vector2.Dot(facing, Vector2.Normalize(toTarget)) >= FacingDot;
    }
}
