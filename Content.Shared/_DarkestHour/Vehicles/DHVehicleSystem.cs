using Content.Shared._DarkestHour.Weapons.Ranged.Emplacements;
using Content.Shared.Actions;
using System.Numerics;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared._DarkestHour.Vehicles;

/// <summary>
/// Кресла отсека (водитель ведёт машину, стрелок управляет орудием, оба видят то, что вокруг машины),
/// точка выстрела орудий и поворот башни за прицелом.
/// </summary>
public sealed partial class DHVehicleSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContentEyeSystem _contentEye = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private DHWeaponMountSystem _mount = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private Content.Shared.Vehicle.Systems.VehicleSystem _vehicle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHVehicleSeatComponent, StrappedEvent>(OnSeatStrapped);
        SubscribeLocalEvent<DHVehicleSeatComponent, UnstrappedEvent>(OnSeatUnstrapped);
        SubscribeLocalEvent<DHVehicleGunComponent, DHGunShotOriginEvent>(OnGunShotOrigin);
        SubscribeLocalEvent<DHVehicleGunComponent, AttemptShootEvent>(OnGunAttemptShoot);
        SubscribeNetworkEvent<DHVehicleTurretAimEvent>(OnTurretAim);
    }

    #region Кресла

    private void OnSeatStrapped(Entity<DHVehicleSeatComponent> ent, ref StrappedEvent args)
    {
        if (ent.Comp.Vehicle is not { } vehicle || !TryComp<DHVehicleComponent>(vehicle, out var vehicleComp))
            return;

        var user = args.Buckle.Owner;
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

        if (ent.Comp.Role is DHVehicleSeatRole.Driver or DHVehicleSeatRole.Gunner && _net.IsServer &&
            TryComp<ContentEyeComponent>(user, out var contentEye))
        {
            var zoom = new Vector2(vehicleComp.CrewZoom);
            ent.Comp.PreviousMaxZoom = contentEye.MaxZoom;
            _contentEye.SetMaxZoom(user, Vector2.Max(contentEye.MaxZoom, zoom), contentEye);
            _contentEye.SetZoom(user, zoom, eye: contentEye);
            _eye.SetPvsScale(user, vehicleComp.CrewZoom);
        }

        if (ent.Comp.SmokeControl && vehicleComp.Smoke && _net.IsServer)
            _actions.AddAction(user, ref ent.Comp.SmokeActionEntity, vehicleComp.SmokeAction, vehicle);
    }

    private void OnSeatUnstrapped(Entity<DHVehicleSeatComponent> ent, ref UnstrappedEvent args)
    {
        var user = args.Buckle.Owner;
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

        if (ent.Comp.PreviousMaxZoom is { } previousZoom && _net.IsServer)
        {
            ent.Comp.PreviousMaxZoom = null;
            if (TryComp<ContentEyeComponent>(user, out var contentEye))
            {
                _contentEye.SetMaxZoom(user, previousZoom, contentEye);
                _contentEye.ResetZoom(user, contentEye);
            }
        }

        if (ent.Comp.SmokeActionEntity is { } action)
        {
            _actions.RemoveAction(user, action);
            ent.Comp.SmokeActionEntity = null;
        }
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

        var muzzle = center.Position + direction.Normalized() * ent.Comp.MuzzleOffset;
        args.Origin = _transform.ToCoordinates(vehicle, new Robust.Shared.Map.MapCoordinates(muzzle, center.MapId));
    }

    /// <summary>Башня доворачивается к прицелу при выстреле (по четвертям оборота — у спрайтов четыре направления).</summary>
    private void OnGunAttemptShoot(Entity<DHVehicleGunComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled || !ent.Comp.Turret || ent.Comp.Vehicle is not { } vehicle ||
            !TryComp<DHVehicleComponent>(vehicle, out var vehicleComp) ||
            !TryComp<GunComponent>(ent, out var gun) || gun.ShootCoordinates is not { } aim)
        {
            return;
        }

        if (TryGetTurretQuarter(vehicle, _transform.ToMapCoordinates(aim), out var quarter))
            SetTurretQuarter((vehicle, vehicleComp), quarter);
    }

    /// <summary>Четверть оборота башни, при которой она смотрит на <paramref name="target"/>.</summary>
    public bool TryGetTurretQuarter(EntityUid vehicle, MapCoordinates target, out int quarter)
    {
        quarter = 0;
        var xform = Transform(vehicle);
        if (xform.MapID != target.MapId)
            return false;

        var (center, rotation) = _transform.GetWorldPositionRotation(xform);
        var direction = target.Position - center;
        if (direction.LengthSquared() < 0.01f)
            return false;

        var relative = Angle.FromWorldVec(direction) - rotation;
        // Угол 0 — «на юг», как и спрайт корпуса; четверть 1 — башня повёрнута на 90° против часовой.
        quarter = (int) Math.Round(relative.Reduced().Theta / (Math.PI / 2)) & 3;
        return true;
    }

    public void SetTurretQuarter(Entity<DHVehicleComponent> vehicle, int quarter)
    {
        quarter &= 3;
        if (quarter == vehicle.Comp.TurretQuarter)
            return;

        vehicle.Comp.TurretQuarter = quarter;
        Dirty(vehicle);
        _appearance.SetData(vehicle, DHVehicleVisuals.TurretQuarter, quarter);
    }

    /// <summary>Стрелок с башенным орудием, на какой машине он сидит.</summary>
    public bool TryGetTurretGunner(EntityUid user, out Entity<DHVehicleComponent> vehicle)
    {
        vehicle = default;
        if (!TryComp<BuckleComponent>(user, out var buckle) || buckle.BuckledTo is not { } seatUid ||
            !TryComp<DHVehicleSeatComponent>(seatUid, out var seat) || seat.Role != DHVehicleSeatRole.Gunner ||
            seat.Weapon is not { } slot || seat.Vehicle is not { } vehicleUid ||
            !TryComp<DHVehicleComponent>(vehicleUid, out var vehicleComp) ||
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

        SetTurretQuarter(vehicle, ev.Quarter);
    }

    private bool TryTurretGunner(EntityUid user, DHVehicleTurretAimEvent ev, out Entity<DHVehicleComponent> vehicle)
    {
        return TryGetTurretGunner(user, out vehicle) && GetNetEntity(vehicle.Owner) == ev.Vehicle;
    }

    #endregion
}
