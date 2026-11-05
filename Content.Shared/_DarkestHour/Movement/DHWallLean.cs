using System.Numerics;
using Content.Shared.Buckle.Components;
using Content.Shared.DragDrop;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._DarkestHour.Movement;

// Прижаться к стене и выглянуть из-за угла (решение автора 2026-11-01, по образцу RussianCM / CMU): перетащите себя
// на стену — встанете вплотную к ней, вжавшись (спрайт сдвинут к стене, через вас проходят свои). Любое движение —
// отход от стены с коротким замедлением.

/// <summary>К этому можно прижаться (стены).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHWallLeanableComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHWallLeaningComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Wall;

    /// <summary>Сдвиг спрайта к стене.</summary>
    [DataField, AutoNetworkedField]
    public Vector2 Offset;

    /// <summary>Стоит у стены с её «верхней» стороны — рисуется под стеной.</summary>
    [DataField, AutoNetworkedField]
    public bool BehindWall;

    /// <summary>Где стоит, в системе грида стены.</summary>
    [DataField]
    public Vector2 Position;

    /// <summary>Замедление при отходе от стены.</summary>
    [DataField]
    public float SlowAfter = 0.5f;

    [DataField]
    public TimeSpan SlowAfterTime = TimeSpan.FromSeconds(1.5);
}

public sealed partial class DHWallLeanSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DHTimedSlowSystem _slow = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float SideShift = 10f / 32f;
    private const float UnderWallShift = 16f / 32f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHWallLeanableComponent, CanDropTargetEvent>(OnCanDropTarget);
        SubscribeLocalEvent<DHWallLeanableComponent, DragDropTargetEvent>(OnDragDropTarget);
        SubscribeLocalEvent<DHWallLeaningComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<DHWallLeaningComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<DHWallLeaningComponent, AttemptMobCollideEvent>(OnAttemptMobCollide);
        SubscribeLocalEvent<DHWallLeaningComponent, AttemptMobTargetCollideEvent>(OnAttemptMobTargetCollide);
    }

    private void OnPreventCollide(Entity<DHWallLeaningComponent> ent, ref PreventCollideEvent args)
    {
        if (ent.Comp.Wall != null && HasComp<MobStateComponent>(args.OtherEntity))
            args.Cancelled = true;
    }

    private void OnAttemptMobCollide(Entity<DHWallLeaningComponent> ent, ref AttemptMobCollideEvent args)
    {
        if (ent.Comp.Wall != null)
            args.Cancelled = true;
    }

    private void OnAttemptMobTargetCollide(Entity<DHWallLeaningComponent> ent, ref AttemptMobTargetCollideEvent args)
    {
        if (ent.Comp.Wall != null)
            args.Cancelled = true;
    }

    private void OnCanDropTarget(Entity<DHWallLeanableComponent> ent, ref CanDropTargetEvent args)
    {
        if (args.User != args.Dragged || !HasComp<HumanoidProfileComponent>(args.User))
            return;

        args.CanDrop = true;
        args.Handled = true;
    }

    private void OnDragDropTarget(Entity<DHWallLeanableComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.User != args.Dragged || !HasComp<HumanoidProfileComponent>(args.User))
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        TryLean(args.User, ent);
    }

    private void TryLean(EntityUid user, EntityUid wall)
    {
        if (HasComp<DHWallLeaningComponent>(user))
            return;

        string? blocked = null;
        if (TryComp<PullerComponent>(user, out var puller) && puller.Pulling != null)
            blocked = "dh-wall-lean-grabbing";
        else if (_mobState.IsIncapacitated(user))
            blocked = "dh-wall-lean-incapacitated";
        else if (_standing.IsDown(user))
            blocked = "dh-wall-lean-resting";
        else if (TryComp<BuckleComponent>(user, out var buckle) && buckle.Buckled)
            blocked = "dh-wall-lean-buckled";

        if (blocked != null)
        {
            _popup.PopupEntity(Loc.GetString(blocked), user, user, PopupType.SmallCaution);
            return;
        }

        var wallPos = _transform.GetWorldPosition(wall);
        var userPos = _transform.GetWorldPosition(user);
        var diff = userPos - wallPos;
        Direction direction;
        if (MathF.Abs(diff.X) > MathF.Abs(diff.Y))
            direction = diff.X > 0 ? Direction.East : Direction.West;
        else
            direction = diff.Y > 0 ? Direction.North : Direction.South;

        var spot = wallPos + direction.ToVec();
        if ((spot - userPos).Length() > 0.75f)
        {
            _popup.PopupEntity(Loc.GetString("dh-wall-lean-too-far"), user, user, PopupType.SmallCaution);
            return;
        }

        var offset = direction switch
        {
            Direction.North => new Vector2(0, -SideShift),
            Direction.South => new Vector2(0, UnderWallShift),
            Direction.East => new Vector2(-SideShift, 0),
            _ => new Vector2(SideShift, 0),
        };

        _transform.SetWorldPosition(user, spot);
        _transform.SetWorldRotation(user, direction.ToAngle());

        var leaning = EnsureComp<DHWallLeaningComponent>(user);
        leaning.Wall = wall;
        leaning.Offset = offset;
        leaning.BehindWall = direction == Direction.North;
        leaning.Position = FramePosition(user, wall);
        Dirty(user, leaning);
        _physics.RegenerateContacts(user);
    }

    private void OnMoveInput(Entity<DHWallLeaningComponent> ent, ref MoveInputEvent args)
    {
        if (_net.IsClient || !args.HasDirectionalMovement)
            return;

        StopLeaning(ent);
    }

    public void StopLeaning(Entity<DHWallLeaningComponent> ent)
    {
        if (ent.Comp.Wall == null)
            return;

        ent.Comp.Wall = null;
        Dirty(ent);
        _physics.RegenerateContacts(ent.Owner);
        _slow.Slow(ent, ent.Comp.SlowAfter, ent.Comp.SlowAfterTime);
        RemCompDeferred<DHWallLeaningComponent>(ent);
    }

    /// <summary>Положение в системе того, на чём стоит стена (летящий челнок не считается отходом).</summary>
    private Vector2 FramePosition(EntityUid user, EntityUid wall)
    {
        var frame = Transform(wall).ParentUid;
        var world = _transform.GetWorldPosition(user);
        return frame.IsValid() ? Vector2.Transform(world, _transform.GetInvWorldMatrix(frame)) : world;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<DHWallLeaningComponent>();
        while (query.MoveNext(out var uid, out var leaning))
        {
            if (leaning.Wall is not { } wall)
                continue;

            if (TerminatingOrDeleted(wall) ||
                _mobState.IsIncapacitated(uid) ||
                _standing.IsDown(uid) ||
                TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled ||
                (FramePosition(uid, wall) - leaning.Position).Length() > 0.1f)
            {
                StopLeaning((uid, leaning));
            }
        }
    }
}
