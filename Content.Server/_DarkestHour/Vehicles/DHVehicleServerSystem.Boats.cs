using System.Numerics;
using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Vehicle.Components;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Vehicles;

/// <summary>
/// Открытые машины и лодки (план осады п. 10, решения автора 2026-11-19), как джип КОФ в CMU: отсека нет, сиденья —
/// прямо на корпусе, сел на место водителя — рулишь, за пулемёт — стреляешь, сидящих видно и в них можно попасть.
/// <list type="bullet">
/// <item>лодка ходит только по воде; сойти — у берега или на мелководье (иначе «сначала причальте»);</item>
/// <item>потопили — сидящих выносит на ближайший берег с уроном, а дальше 5 клеток от берега они тонут;</item>
/// <item>надувная складывается у берега (5 с, если в ней никого) и раскладывается у воды;</item>
/// <item>тяжёлые лодки ничьи: своя для той стороны, чей боец сел за руль.</item>
/// </list>
/// </summary>
public sealed partial class DHVehicleServerSystem
{
    [Dependency] private DHBoatSystem _boats = default!;

    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";

    private void InitializeBoats()
    {
        SubscribeLocalEvent<DHVehicleSeatComponent, UnstrappedEvent>(OnOpenSeatUnstrapped);
        SubscribeLocalEvent<DHBoatComponent, GetVerbsEvent<AlternativeVerb>>(OnBoatVerbs);
        SubscribeLocalEvent<DHBoatComponent, DHBoatFoldDoAfterEvent>(OnBoatFolded);
        SubscribeLocalEvent<DHBoatFoldedComponent, UseInHandEvent>(OnFoldedUse);
        SubscribeLocalEvent<DHBoatFoldedComponent, DHBoatDeployDoAfterEvent>(OnBoatDeployed);
    }

    /// <summary>Сиденья открытой машины — дочерние сущности корпуса.</summary>
    private void CreateOpenSeats(Entity<DHVehicleComponent> ent)
    {
        foreach (var seat in ent.Comp.Seats)
        {
            if (!_proto.HasIndex(seat.Proto))
            {
                Log.Error($"{ToPrettyString(ent)}: unknown seat prototype {seat.Proto}, skipped");
                continue;
            }

            var uid = Spawn(seat.Proto, new EntityCoordinates(ent, seat.Offset));
            _transform.SetLocalRotation(uid, Angle.FromDegrees(seat.Rot));
            if (TryComp<DHVehicleStationComponent>(uid, out var station))
            {
                station.Vehicle = ent;
                Dirty(uid, station);
            }

            // У пулемётчика открытой машины — и заряжание: ящик патронов кладут прямо в его место.
            if (TryComp<DHVehicleAmmoRackComponent>(uid, out var rack))
            {
                rack.Vehicle = ent;
                Dirty(uid, rack);
            }

            ent.Comp.SeatEntities.Add(uid);
        }

        ent.Comp.Capacity = Math.Max(ent.Comp.Capacity, ent.Comp.SeatEntities.Count);
    }

    /// <summary>Сошёл с открытой машины — на ближайшее место, где можно стоять (у лодки — берег или мелководье).</summary>
    private void OnOpenSeatUnstrapped(Entity<DHVehicleSeatComponent> ent, ref UnstrappedEvent args)
    {
        var vehicle = Transform(ent).ParentUid;
        if (!HasComp<DHBoatComponent>(vehicle) || TerminatingOrDeleted(args.Buckle.Owner))
            return;

        if (_boats.NearestDry(vehicle, Transform(ent).Coordinates, 3f) is { } dry)
            _transform.SetCoordinates(args.Buckle.Owner, dry);
    }

    /// <summary>Ничья машина — своя для стороны водителя.</summary>
    private void UpdateNeutralFaction(EntityUid uid, DHVehicleComponent vehicle)
    {
        if (!vehicle.Neutral || !TryComp<VehicleComponent>(uid, out var driven) || driven.Operator is not { } driver ||
            !TryComp<DHAssaultFighterComponent>(driver, out var fighter) || fighter.Faction == vehicle.Faction)
        {
            return;
        }

        vehicle.Faction = fighter.Faction;
        Dirty(uid, vehicle);
    }

    /// <summary>
    /// Лодку потопили: сидящих снимает с сидений и выносит на ближайший берег или мелководье с уроном; если берега
    /// ближе <see cref="DHBoatComponent.DrownRange"/> нет — тонут. С сухопутной открытой машины — просто сбрасывает.
    /// </summary>
    private void EjectRiders(Entity<DHVehicleComponent> ent)
    {
        TryComp<DHBoatComponent>(ent, out var boat);
        foreach (var seat in ent.Comp.SeatEntities)
        {
            if (!TryComp<StrapComponent>(seat, out var strap))
                continue;

            foreach (var rider in new List<EntityUid>(strap.BuckledEntities))
            {
                var from = Transform(seat).Coordinates;
                _boats.Forced.Add(rider);
                _buckle.Unbuckle(rider, null);
                _boats.Forced.Remove(rider);

                if (boat == null)
                {
                    var offset = _random.NextAngle().ToWorldVec() * _random.NextFloat(1f, 2f);
                    _transform.SetCoordinates(rider, from.Offset(offset));
                    continue;
                }

                if (_boats.NearestDry(ent, from, boat.DrownRange) is { } dry)
                {
                    _transform.SetCoordinates(rider, dry);
                    _damageable.TryChangeDamage(rider, new DamageSpecifier(_proto.Index(Asphyxiation), 25), true);
                    _stun.TryKnockdown(rider, TimeSpan.FromSeconds(4), true);
                    _popup.PopupEntity(Loc.GetString("dh-boat-washed-ashore"), rider, rider, PopupType.LargeCaution);
                }
                else
                {
                    // Утонул: тело остаётся там, где была лодка.
                    _damageable.TryChangeDamage(rider, new DamageSpecifier(_proto.Index(Asphyxiation), 300), true);
                    _popup.PopupEntity(Loc.GetString("dh-boat-drowned"), rider, rider, PopupType.LargeCaution);
                }
            }
        }
    }

    #region Сложить и разложить надувную

    private void OnBoatVerbs(Entity<DHBoatComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Folded == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dh-boat-verb-fold"),
            Act = () => TryFold(ent, user),
        });
    }

    private void TryFold(Entity<DHBoatComponent> ent, EntityUid user)
    {
        if (TryComp<DHVehicleComponent>(ent, out var vehicle))
        {
            foreach (var seat in vehicle.SeatEntities)
            {
                if (TryComp<StrapComponent>(seat, out var strap) && strap.BuckledEntities.Count > 0)
                {
                    _popup.PopupEntity(Loc.GetString("dh-boat-fold-occupied"), ent, user);
                    return;
                }
            }
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.FoldTime, new DHBoatFoldDoAfterEvent(),
            ent, ent)
        {
            BreakOnMove = true,
            NeedHand = true,
            DistanceThreshold = 2.5f,
        });
    }

    private void OnBoatFolded(Entity<DHBoatComponent> ent, ref DHBoatFoldDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Folded is not { } folded)
            return;

        args.Handled = true;
        var item = Spawn(folded, Transform(args.User).Coordinates);
        _hands.TryPickupAnyHand(args.User, item);
        QueueDel(ent);
    }

    private void OnFoldedUse(Entity<DHBoatFoldedComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (FindLaunch(ent, args.User) == null)
        {
            _popup.PopupEntity(Loc.GetString("dh-boat-no-water"), args.User, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.DeployTime,
            new DHBoatDeployDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnBoatDeployed(Entity<DHBoatFoldedComponent> ent, ref DHBoatDeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (FindLaunch(ent, args.User) is not { } launch)
        {
            _popup.PopupEntity(Loc.GetString("dh-boat-no-water"), args.User, args.User);
            return;
        }

        var boat = Spawn(ent.Comp.Boat, launch.Coordinates);
        _transform.SetWorldRotation(boat, launch.Rotation);
        QueueDel(ent);
    }

    /// <summary>Где у воды рядом с бойцом поместится лодка: ближайшая точка в 3 клетках, корпус носом от берега.</summary>
    private (MapCoordinates Coordinates, Angle Rotation)? FindLaunch(Entity<DHBoatFoldedComponent> ent, EntityUid user)
    {
        if (!_proto.TryIndex(ent.Comp.Boat, out var boatProto) ||
            !boatProto.TryGetComponent<DHVehicleComponent>(out var vehicle, EntityManager.ComponentFactory))
        {
            return null;
        }

        var origin = _transform.GetMapCoordinates(user);
        (MapCoordinates, Angle)? best = null;
        var bestDistance = float.MaxValue;
        for (var dx = -3; dx <= 3; dx++)
        {
            for (var dy = -3; dy <= 3; dy++)
            {
                var center = new Vector2(MathF.Floor(origin.Position.X) + dx + 0.5f,
                    MathF.Floor(origin.Position.Y) + dy + 0.5f);
                var distance = Vector2.Distance(center, origin.Position);
                if (distance < 1f || distance > 3.5f || distance >= bestDistance)
                    continue;

                // Корма — к бойцу, нос — от берега (нос машины смотрит на юг в её системе координат).
                var away = center - origin.Position;
                var rotation = new Angle(MathF.Atan2(away.X, -away.Y));
                foreach (var candidate in new[] { rotation, Angle.Zero, new Angle(Math.PI / 2) })
                {
                    if (!_boats.OnWater(origin.MapId, center, candidate, vehicle.HullHalfSize))
                        continue;

                    best = (new MapCoordinates(center, origin.MapId), candidate);
                    bestDistance = distance;
                    break;
                }
            }
        }

        return best;
    }

    #endregion
}
