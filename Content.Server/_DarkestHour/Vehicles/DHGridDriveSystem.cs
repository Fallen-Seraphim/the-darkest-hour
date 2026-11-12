using System.Numerics;
using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Vehicles;

/// <summary>
/// Вождение техники (см. <see cref="DHGridDriveComponent"/>), перенесено из RMC-14 / CMU (GridVehicleMoverSystem и
/// GridVehicleMotionSimulator): газ и руль у водителя, скорость и вращение корпуса набираются и гаснут постепенно,
/// корпус едет вдоль себя под любым углом. Обычное (физическое) движение у машины выключено: скорость тела обнуляется,
/// позицию и поворот каждый тик ставит эта система, проверяя, помещается ли корпус.
/// </summary>
public sealed partial class DHGridDriveSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Structural = "Structural";

    private const float MinSpeed = 0.01f;
    private const float MinTravel = 0.0001f;

    /// <summary>Быстрее этого машина сбивает людей (медленно — просто расталкивает).</summary>
    private const float RunoverSpeed = 0.5f;

    /// <summary>Шаг поиска места при сдвиге корпуса, тайлов.</summary>
    private const float NudgeStep = 0.05f;

    /// <summary>Скорость сдвига на полосу вбок, тайлов/с.</summary>
    private const float LaneCorrectionSpeed = 4f;

    /// <summary>Кого машина недавно сбила — чтобы не бить каждый тик.</summary>
    private readonly Dictionary<EntityUid, TimeSpan> _runoverCooldown = new();

    /// <summary>Что машина недавно ломала — урон не чаще <see cref="DHGridDriveComponent.SmashCooldown"/>.</summary>
    private readonly Dictionary<EntityUid, TimeSpan> _smashCooldown = new();

    private readonly List<(EntityUid Uid, Vector2 Position)> _blockers = new();
    private readonly List<(EntityUid Uid, Vector2 Position)> _initialBlockers = new();
    private readonly List<EntityUid> _toRemove = new();
    private static readonly float[] Signs = { 1f, -1f };
    private TimeSpan _nextPrune;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHGridDriveComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    /// <summary>Физическое движение выключено: машину двигает только эта система.</summary>
    private void OnRefreshSpeed(Entity<DHGridDriveComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(0f, 0f);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now >= _nextPrune)
        {
            _nextPrune = now + TimeSpan.FromSeconds(10);
            Prune(_runoverCooldown, now);
            Prune(_smashCooldown, now);
        }

        var query = EntityQueryEnumerator<DHGridDriveComponent, DHVehicleComponent, InputMoverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var drive, out var vehicle, out var mover, out var xform))
        {
            if (xform.GridUid == null || xform.MapID == MapId.Nullspace)
            {
                drive.CurrentSpeed = 0f;
                drive.AngularVelocityDegrees = 0f;
                drive.IsMoving = false;
                continue;
            }

            TryComp<DHVehicleHardpointsComponent>(uid, out var hardpoints);
            var throttle = 0f;
            var steering = 0f;
            if (TryComp<VehicleComponent>(uid, out var driven) && driven.Operator != null &&
                hardpoints is not { CanDrive: false })
            {
                var buttons = mover.HeldMoveButtons;
                if ((buttons & MoveButtons.Up) != 0)
                    throttle += 1f;
                if ((buttons & MoveButtons.Down) != 0)
                    throttle -= 1f;
                if ((buttons & MoveButtons.Left) != 0)
                    steering += 1f;
                if ((buttons & MoveButtons.Right) != 0)
                    steering -= 1f;
            }

            UpdateDrive((uid, drive, vehicle), xform, throttle, steering, hardpoints?.SpeedMultiplier ?? 1f, frameTime, now);
        }
    }

    private void UpdateDrive(Entity<DHGridDriveComponent, DHVehicleComponent> ent, TransformComponent xform,
        float throttle, float steering, float speedMultiplier, float frameTime, TimeSpan now)
    {
        var drive = ent.Comp1;
        var rotation = _transform.GetWorldRotation(xform);
        var center = _transform.GetWorldPosition(xform);

        // --- руль: скорость вращения корпуса ---
        if (!CanSteer(drive.TurnInPlace, drive.CurrentSpeed))
        {
            // Колёсная машина на месте не рулит.
            drive.AngularVelocityDegrees = 0f;
        }
        else
        {
            var target = GetEffectiveSteering(steering, drive.CurrentSpeed, throttle) * MathF.Max(0f, drive.MaxRotationSpeedDegrees);
            var acceleration = steering == 0f ? drive.RotationDecelerationDegrees : drive.RotationAccelerationDegrees;
            drive.AngularVelocityDegrees = StepTowards(drive.AngularVelocityDegrees, target,
                MathF.Max(0f, acceleration) * frameTime);
        }

        // Доводка до стороны света на малой скорости.
        if (steering == 0f && throttle != 0f && drive.AlignmentAssistDegrees > 0f &&
            CanSteer(drive.TurnInPlace, drive.CurrentSpeed) &&
            MathF.Abs(drive.CurrentSpeed) <= drive.AlignmentAssistMaxSpeed)
        {
            var aligned = rotation.GetCardinalDir().ToAngle();
            var delta = Angle.ShortestDistance(rotation, aligned).Degrees;
            if (Math.Abs(delta) <= drive.AlignmentAssistDegrees)
                drive.AngularVelocityDegrees = (float) Math.Clamp(delta / Math.Max(frameTime, 0.001f), -30f, 30f);
        }

        if (MathF.Abs(drive.AngularVelocityDegrees) > 0.001f)
        {
            var desired = rotation + Angle.FromDegrees(drive.AngularVelocityDegrees * frameTime);
            if (TryFindTurnPosition(ent, xform.MapID, center, desired, now, out var turnCenter))
            {
                rotation = desired;
                _transform.SetWorldRotation(ent.Owner, rotation);
                if (turnCenter != center)
                {
                    center = turnCenter;
                    _transform.SetWorldPosition(ent.Owner, center);
                }
            }
            else
            {
                drive.AngularVelocityDegrees = 0f;
            }
        }

        // --- газ: скорость вдоль корпуса ---
        var maxSpeed = drive.MaxSpeed * speedMultiplier;
        var maxReverse = drive.MaxReverseSpeed * speedMultiplier;
        if (throttle > 0f)
        {
            drive.CurrentSpeed = drive.CurrentSpeed < 0f
                ? StepTowards(drive.CurrentSpeed, 0f, drive.Deceleration * frameTime)
                : StepTowards(drive.CurrentSpeed, maxSpeed, drive.Acceleration * frameTime);
        }
        else if (throttle < 0f)
        {
            drive.CurrentSpeed = drive.CurrentSpeed > 0f
                ? StepTowards(drive.CurrentSpeed, 0f, drive.Deceleration * frameTime)
                : StepTowards(drive.CurrentSpeed, -maxReverse, drive.ReverseAcceleration * frameTime);
        }
        else
        {
            drive.CurrentSpeed = StepTowards(drive.CurrentSpeed, 0f, drive.Deceleration * frameTime);
        }

        // Модуль ходовой разбили на ходу — машина встаёт.
        if ((maxSpeed <= 0f && drive.CurrentSpeed > 0f) || (maxReverse <= 0f && drive.CurrentSpeed < 0f))
            drive.CurrentSpeed = 0f;

        var travel = MathF.Abs(drive.CurrentSpeed) * frameTime;
        if (travel > MinTravel)
        {
            var forward = rotation.ToWorldVec();
            var direction = drive.CurrentSpeed < 0f ? -forward : forward;
            var runOver = MathF.Abs(drive.CurrentSpeed) >= RunoverSpeed ? MobContact.RunOver : MobContact.Push;

            // Ровно по стороне света и без руля — держим полосу: упёрлись краем — сдвигаемся вбок, в проём.
            var cardinal = Math.Abs(Angle.ShortestDistance(rotation, rotation.GetCardinalDir().ToAngle()).Degrees) < 0.1;
            if (TryMove(ent, xform.MapID, center, center + direction * travel, rotation, runOver, now))
            {
                _transform.SetWorldPosition(ent.Owner, center + direction * travel);
            }
            else if (!(steering == 0f && cardinal &&
                       TryLaneCorrection(ent, xform.MapID, center, direction, travel, rotation, runOver, frameTime, now)))
            {
                drive.CurrentSpeed = 0f;
            }
        }

        drive.IsMoving = MathF.Abs(drive.CurrentSpeed) > MinSpeed || MathF.Abs(drive.AngularVelocityDegrees) > 0.001f;
    }

    /// <summary>Сдвиг вбок до <see cref="DHGridDriveComponent.NudgeLimit"/>: где корпус проходит дальше — туда.</summary>
    private bool TryLaneCorrection(Entity<DHGridDriveComponent, DHVehicleComponent> ent, MapId map, Vector2 center,
        Vector2 direction, float travel, Angle rotation, MobContact runOver, float frameTime, TimeSpan now)
    {
        var side = new Vector2(-direction.Y, direction.X);
        var maxShift = LaneCorrectionSpeed * frameTime;
        for (var offset = NudgeStep; offset <= ent.Comp1.NudgeLimit + 0.001f; offset += NudgeStep)
        {
            foreach (var sign in Signs)
            {
                var lane = side * (offset * sign);
                if (!TryMove(ent, map, center, center + lane + direction * travel, rotation, MobContact.None, now, effects: false))
                    continue;

                // Сдвиг вбок плавный: за тик не больше LaneCorrectionSpeed·dt, вперёд — когда встанем на полосу.
                var shift = side * (MathF.Min(offset, maxShift) * sign);
                var target = offset <= maxShift ? center + lane + direction * travel : center + shift;
                if (!TryMove(ent, map, center, target, rotation, runOver, now))
                    return false;

                _transform.SetWorldPosition(ent.Owner, target);
                return true;
            }
        }

        return false;
    }

    /// <summary>Поворот корпуса: на месте, а если тесно — со сдвигом до <see cref="DHGridDriveComponent.NudgeLimit"/>.</summary>
    private bool TryFindTurnPosition(Entity<DHGridDriveComponent, DHVehicleComponent> ent, MapId map, Vector2 center,
        Angle rotation, TimeSpan now, out Vector2 result)
    {
        result = center;
        if (Blockers(ent, map, center, rotation, now, effects: true, MobContact.Push) == 0)
            return true;

        var forward = rotation.ToWorldVec();
        var side = new Vector2(-forward.Y, forward.X);
        for (var offset = NudgeStep; offset <= ent.Comp1.NudgeLimit + 0.001f; offset += NudgeStep)
        {
            foreach (var axis in new[] { side, -side, forward, -forward })
            {
                var candidate = center + axis * offset;
                if (Blockers(ent, map, candidate, rotation, now, effects: false, MobContact.None) != 0)
                    continue;

                result = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Можно ли встать в <paramref name="target"/>. То, во что корпус уже упирается и от чего отъезжает, не мешает —
    /// иначе машина, прижатая к стене, не смогла бы от неё отъехать.
    /// </summary>
    private bool TryMove(Entity<DHGridDriveComponent, DHVehicleComponent> ent, MapId map, Vector2 center, Vector2 target,
        Angle rotation, MobContact runOver, TimeSpan now, bool effects = true)
    {
        if (Blockers(ent, map, target, rotation, now, effects, runOver) == 0)
            return true;

        var blocking = new List<(EntityUid Uid, Vector2 Position)>(_blockers);
        Blockers(ent, map, center, rotation, now, effects: false, MobContact.None);
        _initialBlockers.Clear();
        _initialBlockers.AddRange(_blockers);

        var delta = target - center;
        foreach (var (uid, position) in blocking)
        {
            var already = false;
            foreach (var initial in _initialBlockers)
            {
                if (initial.Uid == uid)
                {
                    already = true;
                    break;
                }
            }

            if (!already || Vector2.Dot(delta, position - center) >= 0f)
                return false;
        }

        return true;
    }

    private static bool CanSteer(bool turnInPlace, float speed)
    {
        return turnInPlace || MathF.Abs(speed) > MinSpeed;
    }

    /// <summary>Задним ходом руль работает наоборот — как у настоящей машины.</summary>
    private static float GetEffectiveSteering(float steering, float speed, float throttle)
    {
        var reversing = speed < -MinSpeed || MathF.Abs(speed) <= MinSpeed && throttle < 0f;
        return reversing ? -steering : steering;
    }

    private static float StepTowards(float current, float target, float maxDelta)
    {
        if (current < target)
            return MathF.Min(current + maxDelta, target);

        if (current > target)
            return MathF.Max(current - maxDelta, target);

        return current;
    }

    /// <summary>
    /// Что не даёт корпусу встать в этом месте и повороте (список — в <see cref="_blockers"/>). Людей сбивает и
    /// отталкивает (они не мешают), постройки ломает (пока не сломает — мешают), о стены, двери и другую технику
    /// упирается; двери пробует открыть.
    /// </summary>
    private int Blockers(Entity<DHGridDriveComponent, DHVehicleComponent> ent, MapId map, Vector2 center,
        Angle rotation, TimeSpan now, bool effects, MobContact mobs)
    {
        var (uid, drive, vehicle) = ent;
        var half = vehicle.HullHalfSize - new Vector2(0.15f, 0.15f);
        var box = new Box2Rotated(Box2.CenteredAround(center, half * 2f), rotation, center);

        _blockers.Clear();
        foreach (var other in _lookup.GetEntitiesIntersecting(map, box, LookupFlags.Dynamic | LookupFlags.Static))
        {
            if (other == uid || other == vehicle.HullEntity || Transform(other).ParentUid == uid ||
                HasComp<ItemComponent>(other) || HasComp<MapGridComponent>(other))
            {
                continue;
            }

            if (HasComp<MobStateComponent>(other))
            {
                if (mobs != MobContact.None)
                    RunOver((uid, vehicle), other, center, rotation, now, mobs == MobContact.RunOver);
                continue;
            }

            if (!TryComp<PhysicsComponent>(other, out var physics) || !physics.CanCollide || !physics.Hard)
                continue;

            var position = _transform.GetWorldPosition(other);
            if (HasComp<DHVehicleComponent>(other))
            {
                _blockers.Add((other, position));
                continue;
            }

            if (TryComp<DoorComponent>(other, out var door))
            {
                if (effects)
                    _door.TryOpen(other, door, uid);
                _blockers.Add((other, position));
                continue;
            }

            if (physics.BodyType != BodyType.Static)
                continue;

            // Стены — преграда; остальное, что ломается (баррикады, заборы, окна, мебель), машина ломает.
            if (effects && !_tag.HasTag(other, WallTag) && HasComp<DamageableComponent>(other) &&
                (!_smashCooldown.TryGetValue(other, out var next) || now >= next))
            {
                _smashCooldown[other] = now + drive.SmashCooldown;
                var damage = new DamageSpecifier(_proto.Index(Structural), drive.SmashDamage);
                damage.DamageDict[Blunt] = drive.SmashDamage / 3f;
                _damageable.TryChangeDamage(other, damage, origin: uid);
            }

            _blockers.Add((other, position));
        }

        return _blockers.Count;
    }

    /// <summary>Вытолкнуть из-под корпуса вбок; на скорости ещё и сбить с ног и ударить.</summary>
    private void RunOver(Entity<DHVehicleComponent> vehicle, EntityUid mob, Vector2 center, Angle rotation, TimeSpan now,
        bool harm)
    {
        if (harm && (!_runoverCooldown.TryGetValue(mob, out var next) || now >= next))
            Harm(vehicle, mob, now);

        // Вбок от оси движения — за борт.
        var position = _transform.GetWorldPosition(mob);
        var side = rotation.RotateVec(new Vector2(1f, 0f));
        var offset = Vector2.Dot(position - center, side);
        var push = (vehicle.Comp.HullHalfSize.X + 0.6f) - MathF.Abs(offset);
        if (push > 0f)
            _transform.SetWorldPosition(mob, position + side * (offset >= 0f ? push : -push));
    }

    private void Harm(Entity<DHVehicleComponent> vehicle, EntityUid mob, TimeSpan now)
    {
        _runoverCooldown[mob] = now + TimeSpan.FromSeconds(1.5);
        _damageable.TryChangeDamage(mob, new DamageSpecifier(_proto.Index(Blunt), vehicle.Comp.RunoverDamage),
            origin: vehicle);
        _stun.TryKnockdown(mob, TimeSpan.FromSeconds(2), true);
        _popup.PopupEntity(Loc.GetString("dh-vehicle-runover", ("vehicle", vehicle.Owner)), mob, mob,
            PopupType.MediumCaution);
    }

    private void Prune(Dictionary<EntityUid, TimeSpan> cooldowns, TimeSpan now)
    {
        _toRemove.Clear();
        foreach (var (uid, until) in cooldowns)
        {
            if (until < now || TerminatingOrDeleted(uid))
                _toRemove.Add(uid);
        }

        foreach (var uid in _toRemove)
        {
            cooldowns.Remove(uid);
        }
    }

    private enum MobContact : byte
    {
        None,
        /// <summary>Медленно: людей только отодвигает.</summary>
        Push,
        /// <summary>На скорости: сбивает, бьёт и отбрасывает.</summary>
        RunOver,
    }
}
