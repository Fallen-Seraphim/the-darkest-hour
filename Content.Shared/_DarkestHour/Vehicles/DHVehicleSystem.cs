using Content.Shared._DarkestHour.Weapons.Ranged.Emplacements;
using Content.Shared.Actions;
using Content.Shared.Buckle.Components;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
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

    /// <summary>Башня доворачивается к прицелу (по четвертям оборота — у спрайтов четыре направления).</summary>
    private void OnGunAttemptShoot(Entity<DHVehicleGunComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled || !ent.Comp.Turret || ent.Comp.Vehicle is not { } vehicle ||
            !TryComp<DHVehicleComponent>(vehicle, out var vehicleComp) ||
            !TryComp<GunComponent>(ent, out var gun) || gun.ShootCoordinates is not { } aim)
        {
            return;
        }

        var xform = Transform(vehicle);
        var center = _transform.GetWorldPosition(xform);
        var target = _transform.ToMapCoordinates(aim).Position;
        if ((target - center).LengthSquared() < 0.01f)
            return;

        var relative = Angle.FromWorldVec(target - center) - _transform.GetWorldRotation(xform);
        // Угол 0 — «на юг», как и спрайт корпуса; четверть 1 — башня повёрнута на 90° против часовой.
        var quarter = (int) Math.Round(relative.Reduced().Theta / (Math.PI / 2)) & 3;
        if (quarter == vehicleComp.TurretQuarter)
            return;

        vehicleComp.TurretQuarter = quarter;
        Dirty(vehicle, vehicleComp);
        _appearance.SetData(vehicle, DHVehicleVisuals.TurretQuarter, quarter);
    }

    #endregion
}
