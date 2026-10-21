using System.Numerics;
using Content.Server.Mining;
using Content.Server.Power.EntitySystems;
using Content.Server.Shuttles.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._DarkestHour.Shuttles.Weapons;
using Content.Shared.Damage.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Shuttles.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Shuttles.Weapons;

/// <summary>
/// Корабельные и станционные орудия: консоль наведения (радар, огонь по клику), поворот башен, снаряды сквозь свой
/// грид, автоматическая противометеорная защита с упреждением и неконтактным подрывом.
/// </summary>
public sealed partial class DHShipWeaponSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private GunSystem _gun = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private ShuttleConsoleSystem _shuttleConsole = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly TimeSpan UiInterval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan AutoBurst = TimeSpan.FromSeconds(0.4);

    private readonly HashSet<Entity<MeteorComponent>> _meteors = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHShipGunComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<DHShipProjectileComponent, PreventCollideEvent>(OnProjectilePreventCollide);

        Subs.BuiEvents<DHGunneryConsoleComponent>(DHGunneryUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnConsoleOpened);
            subs.Event<DHGunneryFireMessage>(OnFire);
            subs.Event<DHGunneryCeaseFireMessage>(OnCeaseFire);
            subs.Event<DHGunneryAutoDefenseMessage>(OnAutoDefense);
        });
    }

    #region Projectiles

    private void OnAmmoShot(Entity<DHShipGunComponent> ent, ref AmmoShotEvent args)
    {
        var grid = Transform(ent).GridUid;
        foreach (var projectile in args.FiredProjectiles)
        {
            var comp = EnsureComp<DHShipProjectileComponent>(projectile);
            comp.SourceGrid = grid;
            comp.ProximityRange = ent.Comp.PointDefense ? ent.Comp.ProximityRange : 0f;
        }
    }

    /// <summary>Снаряд не задевает свой грид и всё, что на нём стоит: орудие стреляет изнутри корпуса.</summary>
    private void OnProjectilePreventCollide(Entity<DHShipProjectileComponent> ent, ref PreventCollideEvent args)
    {
        if (ent.Comp.SourceGrid is not { } grid)
            return;

        if (args.OtherEntity == grid || Transform(args.OtherEntity).GridUid == grid)
            args.Cancelled = true;
    }

    /// <summary>Неконтактный подрыв по метеорам: снаряд наносит свой урон ближайшему метеору в радиусе и исчезает.</summary>
    private void UpdateProximity()
    {
        var query = EntityQueryEnumerator<DHShipProjectileComponent, ProjectileComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var ship, out var projectile, out var xform))
        {
            if (ship.ProximityRange <= 0f || projectile.ProjectileSpent || xform.MapID == MapId.Nullspace)
                continue;

            _meteors.Clear();
            _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(uid, xform), ship.ProximityRange, _meteors);
            foreach (var meteor in _meteors)
            {
                _damageable.TryChangeDamage(meteor.Owner, projectile.Damage, ignoreResistances: true);
                projectile.ProjectileSpent = true;
                QueueDel(uid);
                break;
            }
        }
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        UpdatePointDefense(now);
        UpdateFiring(now);
        UpdateProximity();
        UpdateConsoles(now);
    }

    #region Firing

    private void UpdateFiring(TimeSpan now)
    {
        var query = EntityQueryEnumerator<DHShipGunComponent, GunComponent>();
        while (query.MoveNext(out var uid, out var ship, out var gun))
        {
            if (ship.FireTarget is not { } target || now >= ship.FireUntil)
            {
                ship.FireTarget = null;
                ship.AutoTarget = null;
                continue;
            }

            if (!CanOperate(uid) || target.MapId != Transform(uid).MapID)
                continue;

            Aim(uid, target.Position);
            _gun.AttemptShoot(uid, (uid, gun), _transform.ToCoordinates(target));
        }
    }

    /// <summary>Орудие закреплено и запитано.</summary>
    private bool CanOperate(EntityUid gun)
    {
        var xform = Transform(gun);
        return xform.Anchored && xform.GridUid != null && _power.IsPowered(gun);
    }

    private void Aim(EntityUid gun, Vector2 target)
    {
        var diff = target - _transform.GetWorldPosition(gun);
        if (diff.LengthSquared() > 0.01f)
            _transform.SetWorldRotation(gun, Angle.FromWorldVec(diff));
    }

    #endregion

    #region Point defense

    private void UpdatePointDefense(TimeSpan now)
    {
        var grids = EntityQueryEnumerator<DHPointDefenseGridComponent>();
        while (grids.MoveNext(out var grid, out var defense))
        {
            if (!defense.Enabled || now < defense.NextScan)
                continue;

            defense.NextScan = now + ScanInterval;

            var guns = EntityQueryEnumerator<DHShipGunComponent, GunComponent, TransformComponent>();
            while (guns.MoveNext(out var uid, out var ship, out var gun, out var xform))
            {
                if (!ship.PointDefense || xform.GridUid != grid || !CanOperate(uid))
                    continue;

                // Ручной огонь с консоли важнее автоматики.
                if (ship.FireTarget != null && ship.AutoTarget == null && now < ship.FireUntil)
                    continue;

                if (FindMeteor(uid, ship, gun) is not { } aim)
                    continue;

                ship.AutoTarget = aim.Meteor;
                ship.FireTarget = aim.Point;
                ship.FireUntil = now + AutoBurst;
            }
        }
    }

    /// <summary>Ближайший метеор в радиусе и точка упреждения (снаряд и метеор встречаются).</summary>
    private (EntityUid Meteor, MapCoordinates Point)? FindMeteor(EntityUid gunUid, DHShipGunComponent ship, GunComponent gun)
    {
        var origin = _transform.GetMapCoordinates(gunUid);
        _meteors.Clear();
        _lookup.GetEntitiesInRange(origin, ship.PointDefenseRange, _meteors);

        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        foreach (var meteor in _meteors)
        {
            var distance = (_transform.GetWorldPosition(meteor) - origin.Position).Length();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = meteor;
            }
        }

        if (best is not { } target)
            return null;

        var position = _transform.GetWorldPosition(target);
        var velocity = TryComp<PhysicsComponent>(target, out var body) ? _physics.GetMapLinearVelocity(target, body) : Vector2.Zero;
        var speed = MathF.Max(1f, gun.ProjectileSpeedModified);

        // Упреждение: несколько итераций «время полёта → новая точка».
        var aim = position;
        for (var i = 0; i < 3; i++)
        {
            var time = (aim - origin.Position).Length() / speed;
            aim = position + velocity * time;
        }

        return (target, new MapCoordinates(aim, origin.MapId));
    }

    #endregion

    #region Console

    private void OnConsoleOpened(Entity<DHGunneryConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateConsole(ent);
    }

    private void OnFire(Entity<DHGunneryConsoleComponent> ent, ref DHGunneryFireMessage args)
    {
        var grid = Transform(ent).GridUid;
        if (grid == null || !_power.IsPowered(ent.Owner))
            return;

        var target = _transform.ToMapCoordinates(GetCoordinates(args.Target));
        var until = args.Continuous ? TimeSpan.MaxValue : _timing.CurTime + ent.Comp.Burst;

        foreach (var netGun in args.Guns)
        {
            if (GetEntity(netGun) is not { Valid: true } gun ||
                !TryComp<DHShipGunComponent>(gun, out var ship) ||
                Transform(gun).GridUid != grid)
            {
                continue;
            }

            ship.FireTarget = target;
            ship.FireUntil = until;
            ship.AutoTarget = null;
        }

        UpdateConsole(ent);
    }

    private void OnCeaseFire(Entity<DHGunneryConsoleComponent> ent, ref DHGunneryCeaseFireMessage args)
    {
        var grid = Transform(ent).GridUid;
        var query = EntityQueryEnumerator<DHShipGunComponent, TransformComponent>();
        while (query.MoveNext(out _, out var ship, out var xform))
        {
            if (xform.GridUid != grid || ship.AutoTarget != null)
                continue;

            ship.FireTarget = null;
            ship.FireUntil = TimeSpan.Zero;
        }

        UpdateConsole(ent);
    }

    private void OnAutoDefense(Entity<DHGunneryConsoleComponent> ent, ref DHGunneryAutoDefenseMessage args)
    {
        if (!ent.Comp.AutoDefense || Transform(ent).GridUid is not { } grid)
            return;

        EnsureComp<DHPointDefenseGridComponent>(grid).Enabled = args.Enabled;
        UpdateConsole(ent);
    }

    private void UpdateConsoles(TimeSpan now)
    {
        var query = EntityQueryEnumerator<DHGunneryConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (now < console.NextUiUpdate)
                continue;

            console.NextUiUpdate = now + UiInterval;
            if (_ui.IsUiOpen(uid, DHGunneryUiKey.Key))
                UpdateConsole((uid, console));
        }
    }

    private void UpdateConsole(Entity<DHGunneryConsoleComponent> ent)
    {
        var xform = Transform(ent);
        var docks = _shuttleConsole.GetAllDocks();
        var nav = xform.GridUid != null && xform.ParentUid == xform.GridUid
            ? _shuttleConsole.GetNavState(ent.Owner, docks, xform.Coordinates, xform.LocalRotation)
            : _shuttleConsole.GetNavState(ent.Owner, docks);

        var guns = new List<DHGunneryGunEntry>();
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHShipGunComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var ship, out var gunXform))
        {
            if (xform.GridUid == null || gunXform.GridUid != xform.GridUid || !gunXform.Anchored)
                continue;

            var ammo = new GetAmmoCountEvent();
            RaiseLocalEvent(uid, ref ammo);

            guns.Add(new DHGunneryGunEntry(GetNetEntity(uid), Name(uid), Loc.GetString(ship.Caliber), ammo.Count, ammo.Capacity,
                _power.IsPowered(uid), ship.PointDefense, ship.FireTarget != null && now < ship.FireUntil));
        }

        guns.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));

        var autoDefense = xform.GridUid is { } grid &&
                          TryComp<DHPointDefenseGridComponent>(grid, out var defense) && defense.Enabled;

        _ui.SetUiState(ent.Owner, DHGunneryUiKey.Key,
            new DHGunneryBoundUserInterfaceState(nav, guns, ent.Comp.AutoDefense, autoDefense));
    }

    #endregion
}
