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
/// Движение техники по клеткам (см. <see cref="DHGridDriveComponent"/>). Водитель жмёт направление: вдоль корпуса —
/// машина едет вперёд, против — сдаёт назад, вбок — поворачивает на месте. Обычное (физическое) движение у такой
/// машины выключено: скорость обнуляется, а позицию шаг за шагом ставит эта система.
/// </summary>
public sealed partial class DHGridDriveSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Structural = "Structural";

    /// <summary>Кого машина недавно сбила — чтобы не бить каждый шаг.</summary>
    private readonly Dictionary<EntityUid, TimeSpan> _runoverCooldown = new();

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
        var query = EntityQueryEnumerator<DHGridDriveComponent, DHVehicleComponent, InputMoverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var drive, out var vehicle, out var mover, out var xform))
        {
            if (drive.Moving)
            {
                Advance((uid, drive), xform, frameTime, now);
                continue;
            }

            if (now < drive.BusyUntil)
                continue;

            // Долго стоял — разгон заново.
            if (now - drive.LastStep > TimeSpan.FromSeconds(0.4))
                drive.Streak = 0;

            if (!TryComp<VehicleComponent>(uid, out var driven) || driven.Operator == null ||
                xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            {
                continue;
            }

            if (TryComp<DHVehicleHardpointsComponent>(uid, out var hardpoints) && !hardpoints.CanDrive)
                continue;

            if (PickDirection(mover.HeldMoveButtons, grid) is not { } wish)
                continue;

            TryDrive((uid, drive, vehicle), xform, (grid, gridComp), wish, hardpoints, now);
        }
    }

    /// <summary>Нажатое направление в мире (одна из четырёх сторон с учётом поворота грида).</summary>
    private Vector2? PickDirection(MoveButtons buttons, EntityUid grid)
    {
        Vector2 local;
        if ((buttons & MoveButtons.Up) != 0)
            local = new Vector2(0, 1);
        else if ((buttons & MoveButtons.Down) != 0)
            local = new Vector2(0, -1);
        else if ((buttons & MoveButtons.Left) != 0)
            local = new Vector2(-1, 0);
        else if ((buttons & MoveButtons.Right) != 0)
            local = new Vector2(1, 0);
        else
            return null;

        return _transform.GetWorldRotation(grid).RotateVec(local);
    }

    private void TryDrive(Entity<DHGridDriveComponent, DHVehicleComponent> ent, TransformComponent xform,
        Entity<MapGridComponent> grid, Vector2 wish, DHVehicleHardpointsComponent? hardpoints, TimeSpan now)
    {
        var (uid, drive, vehicle) = ent;
        var rotation = _transform.GetWorldRotation(xform);
        var forward = rotation.ToWorldVec();
        var dot = Vector2.Dot(forward, wish);

        // Вбок — поворот на месте, если новый силуэт помещается.
        if (MathF.Abs(dot) < 0.5f)
        {
            var newRotation = Angle.FromWorldVec(wish);
            var center = _transform.GetWorldPosition(xform);
            if (!FootprintClear(ent, xform.MapID, center, newRotation, now, smash: false))
            {
                drive.BusyUntil = now + TimeSpan.FromSeconds(0.3);
                return;
            }

            _transform.SetWorldRotation(uid, newRotation);
            drive.Streak = 0;
            drive.BusyUntil = now + drive.TurnTime;
            return;
        }

        var reverse = dot < 0f;
        var direction = reverse ? -forward : forward;
        direction = new Vector2(MathF.Round(direction.X), MathF.Round(direction.Y));

        // Машина стоит по центру клетки: шаг — ровно на клетку.
        var position = _transform.GetWorldPosition(xform);
        var from = SnapToTile(grid, position);
        var to = from + direction;

        if (!FootprintClear(ent, xform.MapID, to, rotation, now, smash: true))
        {
            drive.Streak = 0;
            drive.BusyUntil = now + TimeSpan.FromSeconds(0.35);
            return;
        }

        var speed = drive.TilesPerSecond * (hardpoints?.SpeedMultiplier ?? 1f);
        var ramp = drive.RampSteps <= 0 ? 1f
            : MathF.Min(1f, drive.StartFraction + (1f - drive.StartFraction) * drive.Streak / drive.RampSteps);
        speed *= ramp * (reverse ? drive.ReverseFraction : 1f);
        if (speed <= 0.01f)
            return;

        drive.From = from;
        drive.To = to;
        drive.Progress = 0f;
        drive.StepDuration = 1f / speed;
        drive.Moving = true;
    }

    private void Advance(Entity<DHGridDriveComponent> ent, TransformComponent xform, float frameTime, TimeSpan now)
    {
        var drive = ent.Comp;
        drive.Progress += frameTime / MathF.Max(0.05f, drive.StepDuration);
        if (drive.Progress >= 1f)
        {
            _transform.SetWorldPosition(ent, drive.To);
            drive.Moving = false;
            drive.Streak++;
            drive.LastStep = now;
            return;
        }

        _transform.SetWorldPosition(ent, Vector2.Lerp(drive.From, drive.To, drive.Progress));
    }

    private Vector2 SnapToTile(Entity<MapGridComponent> grid, Vector2 world)
    {
        var tile = _map.WorldToTile(grid, grid.Comp, world);
        return _transform.ToMapCoordinates(_map.GridTileToLocal(grid, grid.Comp, tile)).Position;
    }

    /// <summary>
    /// Помещается ли корпус в этом месте и повороте. Людей сбивает и отталкивает (они не мешают), постройки ломает
    /// (шаг не засчитывается, пока не сломает), о стены, двери и другую технику упирается.
    /// </summary>
    private bool FootprintClear(Entity<DHGridDriveComponent, DHVehicleComponent> ent, MapId map, Vector2 center,
        Angle rotation, TimeSpan now, bool smash)
    {
        var (uid, drive, vehicle) = ent;
        var half = vehicle.HullHalfSize - new Vector2(0.15f, 0.15f);
        var box = new Box2Rotated(Box2.CenteredAround(center, half * 2f), rotation, center);

        var clear = true;
        foreach (var other in _lookup.GetEntitiesIntersecting(map, box, LookupFlags.Dynamic | LookupFlags.Static))
        {
            if (other == uid || other == vehicle.HullEntity || Transform(other).ParentUid == uid ||
                HasComp<ItemComponent>(other) || HasComp<MapGridComponent>(other))
            {
                continue;
            }

            if (HasComp<MobStateComponent>(other))
            {
                if (smash)
                    RunOver((uid, vehicle), other, center, rotation, now);
                continue;
            }

            if (!TryComp<PhysicsComponent>(other, out var physics) || !physics.CanCollide || !physics.Hard)
                continue;

            if (HasComp<DHVehicleComponent>(other))
            {
                clear = false;
                continue;
            }

            if (TryComp<DoorComponent>(other, out var door))
            {
                if (smash)
                    _door.TryOpen(other, door, uid);
                clear = false;
                continue;
            }

            if (physics.BodyType != BodyType.Static)
                continue;

            // Стены — преграда; остальное, что ломается (баррикады, заборы, окна, мебель), машина ломает.
            if (smash && !_tag.HasTag(other, WallTag) && HasComp<DamageableComponent>(other))
            {
                var damage = new DamageSpecifier(_proto.Index(Structural), drive.SmashDamage);
                damage.DamageDict[Blunt] = drive.SmashDamage / 3f;
                _damageable.TryChangeDamage(other, damage, origin: uid);
            }

            clear = false;
        }

        return clear;
    }

    /// <summary>Сбить с ног, ударить и вытолкнуть из-под корпуса вбок.</summary>
    private void RunOver(Entity<DHVehicleComponent> vehicle, EntityUid mob, Vector2 center, Angle rotation, TimeSpan now)
    {
        if (_runoverCooldown.TryGetValue(mob, out var next) && now < next)
            return;

        _runoverCooldown[mob] = now + TimeSpan.FromSeconds(1.5);
        _damageable.TryChangeDamage(mob, new DamageSpecifier(_proto.Index(Blunt), vehicle.Comp.RunoverDamage),
            origin: vehicle);
        _stun.TryKnockdown(mob, TimeSpan.FromSeconds(2), true);
        _popup.PopupEntity(Loc.GetString("dh-vehicle-runover", ("vehicle", vehicle.Owner)), mob, mob,
            PopupType.MediumCaution);

        // Вбок от оси движения — за борт.
        var position = _transform.GetWorldPosition(mob);
        var side = rotation.RotateVec(new Vector2(1f, 0f));
        var offset = Vector2.Dot(position - center, side);
        var push = (vehicle.Comp.HullHalfSize.X + 0.6f) - MathF.Abs(offset);
        if (push > 0f)
            _transform.SetWorldPosition(mob, position + side * (offset >= 0f ? push : -push));
    }
}
