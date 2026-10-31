using Content.Shared._DarkestHour.Weapons.Ranged.Emplacements;
using Content.Shared.Actions;
using System.Numerics;
using Content.Shared.Buckle.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;

namespace Content.Shared._DarkestHour.Vehicles;

/// <summary>
/// Посты экипажа в отсеке (механик-водитель ведёт машину, наводчик и командир управляют орудиями и видят то, что
/// вокруг машины), точка выстрела орудий и поворот башни за прицелом наводчика.
/// </summary>
public sealed partial class DHVehicleSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContentEyeSystem _contentEye = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private DHWeaponMountSystem _mount = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private Content.Shared.Vehicle.Systems.VehicleSystem _vehicle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHVehicleStationComponent, InteractHandEvent>(OnStationInteract);
        SubscribeLocalEvent<DHVehicleStationComponent, ComponentShutdown>(OnStationShutdown);
        SubscribeLocalEvent<DHVehicleCrewComponent, UnbuckledEvent>(OnCrewUnbuckled);
        SubscribeLocalEvent<DHVehicleCrewComponent, MobStateChangedEvent>(OnCrewMobState);
        SubscribeLocalEvent<DHVehicleComponent, PreventCollideEvent>(OnVehiclePreventCollide);
        SubscribeLocalEvent<DHVehicleGunComponent, DHGunShotOriginEvent>(OnGunShotOrigin);
        SubscribeLocalEvent<DHVehicleGunComponent, AttemptShootEvent>(OnGunAttemptShoot);
        SubscribeNetworkEvent<DHVehicleTurretAimEvent>(OnTurretAim);
    }

    #region Посты экипажа

    private void OnStationInteract(Entity<DHVehicleStationComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (_net.IsServer)
            ToggleStation(ent, args.User);
    }

    /// <summary>Сесть за пульт (если сидишь в кресле рядом) или отпустить его.</summary>
    public void ToggleStation(Entity<DHVehicleStationComponent> ent, EntityUid user)
    {
        if (ent.Comp.User == user)
        {
            ReleaseStation(ent);
            return;
        }

        if (ent.Comp.User is { } other && Exists(other))
        {
            _popup.PopupEntity(Loc.GetString("dh-vehicle-station-occupied"), ent, user);
            return;
        }

        if (!TryComp<BuckleComponent>(user, out var buckle) || buckle.BuckledTo is not { } seat ||
            !_transform.InRange(Transform(seat).Coordinates, Transform(ent).Coordinates, ent.Comp.Range))
        {
            _popup.PopupEntity(Loc.GetString("dh-vehicle-station-sit"), ent, user);
            return;
        }

        if (TryComp<DHVehicleCrewComponent>(user, out var crew) && crew.Station is { } previous &&
            TryComp<DHVehicleStationComponent>(previous, out var previousComp))
        {
            ReleaseStation((previous, previousComp));
        }

        TakeStation(ent, user);
    }

    private void TakeStation(Entity<DHVehicleStationComponent> ent, EntityUid user)
    {
        if (ent.Comp.Vehicle is not { } vehicle || !TryComp<DHVehicleComponent>(vehicle, out var vehicleComp))
            return;

        ent.Comp.User = user;
        Dirty(ent);
        var crew = EnsureComp<DHVehicleCrewComponent>(user);
        crew.Station = ent;
        Dirty(user, crew);

        switch (ent.Comp.Role)
        {
            case DHVehicleSeatRole.Driver:
                if (TryComp<VehicleComponent>(vehicle, out var driven))
                    _vehicle.TrySetOperator((vehicle, driven), user);
                break;
            case DHVehicleSeatRole.Gunner:
                if (ent.Comp.Weapon is { } slot && TryGetWeapon((vehicle, vehicleComp), slot, out var weapon))
                    _mount.SetController(user, weapon);
                _eye.SetTarget(user, vehicle);
                break;
        }

        if (ent.Comp.Role is DHVehicleSeatRole.Driver or DHVehicleSeatRole.Gunner &&
            TryComp<ContentEyeComponent>(user, out var contentEye))
        {
            var zoom = new Vector2(vehicleComp.CrewZoom);
            ent.Comp.PreviousMaxZoom = contentEye.MaxZoom;
            _contentEye.SetMaxZoom(user, Vector2.Max(contentEye.MaxZoom, zoom), contentEye);
            _contentEye.SetZoom(user, zoom, eye: contentEye);
            _eye.SetPvsScale(user, vehicleComp.CrewZoom);
        }

        if (ent.Comp.SmokeControl && vehicleComp.Smoke)
            _actions.AddAction(user, ref ent.Comp.SmokeActionEntity, vehicleComp.SmokeAction, vehicle);

        _popup.PopupEntity(Loc.GetString("dh-vehicle-station-taken", ("station", ent.Owner)), ent, user);
    }

    public void ReleaseStation(Entity<DHVehicleStationComponent> ent)
    {
        if (ent.Comp.User is not { } user)
            return;

        ent.Comp.User = null;
        Dirty(ent);

        if (!TerminatingOrDeleted(user))
        {
            if (TryComp<DHVehicleCrewComponent>(user, out var crew) && crew.Station == ent.Owner)
                RemComp<DHVehicleCrewComponent>(user);

            if (ent.Comp.Vehicle is { } vehicle && TryComp<DHVehicleComponent>(vehicle, out var vehicleComp))
            {
                switch (ent.Comp.Role)
                {
                    case DHVehicleSeatRole.Driver:
                        if (TryComp<VehicleComponent>(vehicle, out var driven) && driven.Operator == user)
                            _vehicle.TryRemoveOperator((vehicle, driven));
                        break;
                    case DHVehicleSeatRole.Gunner:
                        if (ent.Comp.Weapon is { } slot && TryGetWeapon((vehicle, vehicleComp), slot, out var weapon))
                            _mount.ClearController(user, weapon);
                        _eye.SetTarget(user, null);
                        break;
                }
            }

            if (ent.Comp.PreviousMaxZoom is { } previousZoom && TryComp<ContentEyeComponent>(user, out var contentEye))
            {
                _contentEye.SetMaxZoom(user, previousZoom, contentEye);
                _contentEye.ResetZoom(user, contentEye);
            }

            if (ent.Comp.SmokeActionEntity is { } action)
                _actions.RemoveAction(user, action);
        }

        ent.Comp.PreviousMaxZoom = null;
        ent.Comp.SmokeActionEntity = null;
    }

    private void ReleaseCrew(EntityUid user)
    {
        if (!_net.IsServer || !TryComp<DHVehicleCrewComponent>(user, out var crew) || crew.Station is not { } station ||
            !TryComp<DHVehicleStationComponent>(station, out var stationComp))
        {
            return;
        }

        ReleaseStation((station, stationComp));
    }

    private void OnCrewUnbuckled(Entity<DHVehicleCrewComponent> ent, ref UnbuckledEvent args)
    {
        ReleaseCrew(ent);
    }

    private void OnCrewMobState(Entity<DHVehicleCrewComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            ReleaseCrew(ent);
    }

    private void OnStationShutdown(Entity<DHVehicleStationComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsServer)
            ReleaseStation(ent);
    }

    /// <summary>Снаряды своих орудий не задевают саму машину.</summary>
    private void OnVehiclePreventCollide(Entity<DHVehicleComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || !TryComp<ProjectileComponent>(args.OtherEntity, out var projectile) ||
            projectile.Weapon is not { } weapon || !TryComp<DHVehicleGunComponent>(weapon, out var gun))
        {
            return;
        }

        if (gun.Vehicle == ent.Owner)
            args.Cancelled = true;
    }

    /// <summary>Орудие места <paramref name="slot"/>: сначала по записи машины, иначе — среди орудий, привязанных к ней.</summary>
    public bool TryGetWeapon(Entity<DHVehicleComponent> vehicle, string slot, out EntityUid weapon)
    {
        if (vehicle.Comp.WeaponEntities.TryGetValue(slot, out weapon) && Exists(weapon))
            return true;

        // Клиент не знает записи — ищет орудие с этим местом среди орудий машины.
        var query = EntityQueryEnumerator<DHVehicleGunComponent>();
        while (query.MoveNext(out var uid, out var gun))
        {
            if (gun.Vehicle == vehicle.Owner && gun.Slot == slot)
            {
                weapon = uid;
                return true;
            }
        }

        weapon = default;
        return false;
    }

    #endregion

    #region Стрельба

    private void OnGunShotOrigin(Entity<DHVehicleGunComponent> ent, ref DHGunShotOriginEvent args)
    {
        if (ent.Comp.Vehicle is not { } vehicle || !TryComp<GunComponent>(ent, out var gun) ||
            gun.ShootCoordinates is not { } aim)
        {
            return;
        }

        var xform = Transform(vehicle);
        var center = _transform.GetMapCoordinates(vehicle, xform);
        var target = _transform.ToMapCoordinates(aim);
        if (target.MapId != center.MapId)
            return;

        var direction = target.Position - center.Position;
        if (direction.LengthSquared() < 0.01f)
            direction = _transform.GetWorldRotation(xform).ToWorldVec();

        // Снаряд появляется за краем корпуса в сторону цели (по диагонали корпус «длиннее», чем вылет ствола),
        // иначе он рвётся о собственную броню.
        direction = direction.Normalized();
        var distance = ent.Comp.MuzzleOffset;
        if (TryComp<DHVehicleComponent>(vehicle, out var vehicleComp))
        {
            var local = (-_transform.GetWorldRotation(xform)).RotateVec(direction);
            var half = vehicleComp.HullHalfSize;
            var exitX = MathF.Abs(local.X) > 0.001f ? half.X / MathF.Abs(local.X) : float.MaxValue;
            var exitY = MathF.Abs(local.Y) > 0.001f ? half.Y / MathF.Abs(local.Y) : float.MaxValue;
            distance = MathF.Max(distance, MathF.Min(exitX, exitY) + 0.6f);
        }

        var muzzle = center.Position + direction * distance;
        args.Origin = _transform.ToCoordinates(vehicle, new Robust.Shared.Map.MapCoordinates(muzzle, center.MapId));
    }

    /// <summary>Башня доворачивается к прицелу при выстреле.</summary>
    private void OnGunAttemptShoot(Entity<DHVehicleGunComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled || !ent.Comp.Turret || ent.Comp.Vehicle is not { } vehicle ||
            !TryComp<DHVehicleComponent>(vehicle, out var vehicleComp) ||
            !TryComp<GunComponent>(ent, out var gun) || gun.ShootCoordinates is not { } aim)
        {
            return;
        }

        if (TryGetTurretAngle(vehicle, _transform.ToMapCoordinates(aim), out var angle))
            SetTurretAngle((vehicle, vehicleComp), angle);
    }

    /// <summary>Угол башни к корпусу, при котором она смотрит на <paramref name="target"/>.</summary>
    public bool TryGetTurretAngle(EntityUid vehicle, MapCoordinates target, out Angle angle)
    {
        angle = Angle.Zero;
        var xform = Transform(vehicle);
        if (xform.MapID != target.MapId)
            return false;

        var (center, rotation) = _transform.GetWorldPositionRotation(xform);
        var direction = target.Position - center;
        if (direction.LengthSquared() < 0.01f)
            return false;

        // Угол 0 — «на юг», как и спрайт корпуса.
        angle = (Angle.FromWorldVec(direction) - rotation).Reduced();
        return true;
    }

    public void SetTurretAngle(Entity<DHVehicleComponent> vehicle, Angle angle)
    {
        angle = angle.Reduced();
        if (Math.Abs((angle - vehicle.Comp.TurretAngle).Reduced().Theta) < 0.02)
            return;

        vehicle.Comp.TurretAngle = angle;
        Dirty(vehicle);
    }

    /// <summary>Наводчик башенного орудия: за каким пультом какой машины он работает.</summary>
    public bool TryGetTurretGunner(EntityUid user, out Entity<DHVehicleComponent> vehicle)
    {
        vehicle = default;
        if (!TryComp<DHVehicleCrewComponent>(user, out var crew) || crew.Station is not { } stationUid ||
            !TryComp<DHVehicleStationComponent>(stationUid, out var station) || station.User != user ||
            station.Role != DHVehicleSeatRole.Gunner || station.Weapon is not { } slot ||
            station.Vehicle is not { } vehicleUid || !TryComp<DHVehicleComponent>(vehicleUid, out var vehicleComp) ||
            !TryGetWeapon((vehicleUid, vehicleComp), slot, out var weapon) ||
            !TryComp<DHVehicleGunComponent>(weapon, out var gun) || !gun.Turret)
        {
            return false;
        }

        vehicle = (vehicleUid, vehicleComp);
        return true;
    }

    private void OnTurretAim(DHVehicleTurretAimEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user || !TryTurretGunner(user, ev, out var vehicle))
            return;

        SetTurretAngle(vehicle, ev.Angle);
    }

    private bool TryTurretGunner(EntityUid user, DHVehicleTurretAimEvent ev, out Entity<DHVehicleComponent> vehicle)
    {
        return TryGetTurretGunner(user, out vehicle) && GetNetEntity(vehicle.Owner) == ev.Vehicle;
    }

    #endregion
}
