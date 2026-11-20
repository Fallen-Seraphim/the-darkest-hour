using System.Numerics;
using Content.Server._DarkestHour.Assault;
using Content.Server._DarkestHour.Recon;
using Content.Shared._DarkestHour.AntiTank;
using Content.Shared._DarkestHour.Assault;
using Content.Shared.Actions;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.AntiTank;

/// <summary>Ракета ПТУР в полёте: пока цел провод и наводчик за ПТУР — доворачивает на его курсор.</summary>
[RegisterComponent]
public sealed partial class DHGuidedMissileComponent : Component
{
    [DataField]
    public EntityUid Launcher;

    [DataField]
    public Vector2 Origin;

    [DataField]
    public float Speed;

    [DataField]
    public float TurnRate;

    [DataField]
    public float WireRange;

    /// <summary>Провод оборван (далеко, наводчик встал, ПТУР разбит) — дальше ракета летит прямо.</summary>
    [DataField]
    public bool WireCut;
}

/// <summary>
/// Противотанковое оружие пехоты (план осады п. 13, решения автора 2026-11-19): одноразовая труба и ПТУР на треноге,
/// которым наводчик ведёт ракету по курсору. См. <see cref="DHATGMComponent"/>.
/// </summary>
public sealed partial class DHAntiTankSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGunSystem _guns = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<MobStateComponent>> _blasted = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHDisposableLauncherComponent, AmmoShotEvent>(OnTubeShot);

        SubscribeLocalEvent<DHATGMFoldedComponent, UseInHandEvent>(OnFoldedUse);
        SubscribeLocalEvent<DHATGMFoldedComponent, DHATGMDeployDoAfterEvent>(OnDeployed);

        SubscribeLocalEvent<DHATGMComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DHATGMComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DHATGMComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DHATGMComponent, DHATGMReloadDoAfterEvent>(OnReloaded);
        SubscribeLocalEvent<DHATGMComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeLocalEvent<DHATGMComponent, DHATGMFoldDoAfterEvent>(OnFolded);
        SubscribeLocalEvent<DHATGMComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<DHATGMComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<DHATGMComponent, DHATGMFireActionEvent>(OnFire);

        SubscribeNetworkEvent<DHATGMAimEvent>(OnAim);
    }

    #region Реактивная струя

    /// <summary>Струя назад от точки пуска: обжигает и роняет стоящих позади, облачко дыма.</summary>
    private void Backblast(DHBackblastComponent blast, EntityUid shooter, MapCoordinates origin, Vector2 direction)
    {
        if (direction.LengthSquared() < 0.0001f)
            return;

        var back = -Vector2.Normalize(direction);
        var cos = MathF.Cos(MathHelper.DegreesToRadians(blast.HalfAngle));
        _blasted.Clear();
        _lookup.GetEntitiesInRange(origin, blast.Range, _blasted);
        foreach (var mob in _blasted)
        {
            if (mob.Owner == shooter)
                continue;

            var offset = _transform.GetWorldPosition(mob) - origin.Position;
            if (offset.LengthSquared() < 0.04f || Vector2.Dot(Vector2.Normalize(offset), back) < cos)
                continue;

            _damageable.TryChangeDamage(mob.Owner, blast.Damage, true);
            _stun.TryKnockdown(mob.Owner, blast.Knockdown, true);
            _popup.PopupEntity(Loc.GetString("dh-antitank-backblast"), mob, mob, PopupType.MediumCaution);
        }

        if (blast.Smoke is { } smoke)
            Spawn(smoke, new MapCoordinates(origin.Position + back * 0.7f, origin.MapId));
    }

    #endregion

    #region Одноразовая труба

    private void OnTubeShot(Entity<DHDisposableLauncherComponent> ent, ref AmmoShotEvent args)
    {
        var holder = Transform(ent).ParentUid;
        var coordinates = _transform.GetMapCoordinates(holder);
        var direction = Vector2.Zero;
        foreach (var projectile in args.FiredProjectiles)
        {
            if (TryComp<PhysicsComponent>(projectile, out var physics))
                direction = physics.LinearVelocity;
        }

        if (TryComp<DHBackblastComponent>(ent, out var blast))
            Backblast(blast, holder, coordinates, direction);

        // Выстрелил — бросил: пустой тубус падает под ноги.
        Spawn(ent.Comp.Spent, coordinates);
        QueueDel(ent);
    }

    #endregion

    #region ПТУР: разложить, сложить, зарядить

    private void OnFoldedUse(Entity<DHATGMFoldedComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.DeployTime,
            new DHATGMDeployDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
        _popup.PopupEntity(Loc.GetString("dh-atgm-deploying"), args.User, args.User);
    }

    private void OnDeployed(Entity<DHATGMFoldedComponent> ent, ref DHATGMDeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var userXform = Transform(args.User);
        // Тренога смотрит туда же, куда боец, и встаёт на его клетку.
        var facing = _transform.GetWorldRotation(userXform).GetCardinalDir();
        var deployed = Spawn(ent.Comp.Deployed, _transform.GetMapCoordinates(args.User, userXform));
        _transform.SetWorldRotation(deployed, facing.ToAngle());
        _transform.AnchorEntity(deployed);
        QueueDel(ent);
    }

    private void OnMapInit(Entity<DHATGMComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent, DHATGMVisuals.Loaded, ent.Comp.Loaded);
    }

    private void OnExamined(Entity<DHATGMComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Loaded ? "dh-atgm-examine-loaded" : "dh-atgm-examine-empty"));
    }

    private void OnInteractUsing(Entity<DHATGMComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<DHATGMMissileItemComponent>(args.Used))
            return;

        args.Handled = true;
        if (ent.Comp.Loaded)
        {
            _popup.PopupEntity(Loc.GetString("dh-atgm-already-loaded"), ent, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.ReloadTime,
            new DHATGMReloadDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnReloaded(Entity<DHATGMComponent> ent, ref DHATGMReloadDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Loaded || args.Used is not { } missile ||
            TerminatingOrDeleted(missile))
        {
            return;
        }

        args.Handled = true;
        QueueDel(missile);
        SetLoaded(ent, true);
        _audio.PlayPvs(ent.Comp.ReloadSound, ent);
    }

    private void SetLoaded(Entity<DHATGMComponent> ent, bool loaded)
    {
        ent.Comp.Loaded = loaded;
        Dirty(ent);
        _appearance.SetData(ent, DHATGMVisuals.Loaded, loaded);
    }

    private void OnVerbs(Entity<DHATGMComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Operator != null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dh-atgm-verb-fold"),
            Act = () => _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.FoldTime,
                new DHATGMFoldDoAfterEvent(), ent, ent)
            {
                BreakOnMove = true,
                NeedHand = true,
            }),
        });
    }

    private void OnFolded(Entity<DHATGMComponent> ent, ref DHATGMFoldDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Operator != null)
            return;

        args.Handled = true;
        var coordinates = _transform.GetMapCoordinates(args.User);
        // Ракету из трубы вынимают: сложенный ПТУР всегда пустой.
        if (ent.Comp.Loaded)
            Spawn(ent.Comp.MissileItem, coordinates);

        var folded = Spawn(ent.Comp.Folded, coordinates);
        _hands.TryPickupAnyHand(args.User, folded);
        QueueDel(ent);
    }

    #endregion

    #region ПТУР: наводчик и пуск

    private void OnStrapped(Entity<DHATGMComponent> ent, ref StrappedEvent args)
    {
        var user = args.Buckle.Owner;
        ent.Comp.Operator = user;
        ent.Comp.Aim = null;
        Dirty(ent);
        _actions.AddAction(user, ref ent.Comp.FireActionEntity, ent.Comp.FireAction, ent);
        _popup.PopupEntity(Loc.GetString("dh-atgm-seated"), user, user);
    }

    private void OnUnstrapped(Entity<DHATGMComponent> ent, ref UnstrappedEvent args)
    {
        var user = args.Buckle.Owner;
        if (ent.Comp.FireActionEntity is { } action)
            _actions.RemoveAction(user, action);

        ent.Comp.FireActionEntity = null;
        ent.Comp.Operator = null;
        ent.Comp.Aim = null;
        Dirty(ent);
    }

    private void OnAim(DHATGMAimEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user ||
            !TryGetEntity(ev.Launcher, out var launcher) ||
            !TryComp<DHATGMComponent>(launcher, out var atgm) ||
            atgm.Operator != user)
        {
            return;
        }

        atgm.Aim = ev.Position;
    }

    private void OnFire(Entity<DHATGMComponent> ent, ref DHATGMFireActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.Performer;
        if (ent.Comp.Operator != user)
            return;

        if (!ent.Comp.Loaded)
        {
            _popup.PopupEntity(Loc.GetString("dh-atgm-empty"), user, user);
            return;
        }

        var xform = Transform(ent);
        var origin = _transform.GetMapCoordinates(ent, xform);
        var facing = _transform.GetWorldRotation(xform).ToWorldVec();
        var direction = ent.Comp.Aim is { } aim && (aim - origin.Position).LengthSquared() > 0.25f
            ? Vector2.Normalize(aim - origin.Position)
            : facing;

        // Пуск — только в секторе треноги: дальше ракета доворачивает сама.
        var offAxis = Angle.ShortestDistance(facing.ToWorldAngle(), direction.ToWorldAngle());
        var arc = Angle.FromDegrees(ent.Comp.Arc);
        if (Math.Abs(offAxis.Theta) > arc.Theta)
            direction = (facing.ToWorldAngle() + (offAxis.Theta > 0 ? arc : -arc)).ToWorldVec();

        SetLoaded(ent, false);
        var missile = Spawn(ent.Comp.Missile, new MapCoordinates(origin.Position + direction * 0.7f, origin.MapId));
        _guns.ShootProjectile(missile, direction, Vector2.Zero, ent, user, ent.Comp.MissileSpeed);
        var guided = EnsureComp<DHGuidedMissileComponent>(missile);
        guided.Launcher = ent;
        guided.Origin = origin.Position;
        guided.Speed = ent.Comp.MissileSpeed;
        guided.TurnRate = ent.Comp.TurnRate;
        guided.WireRange = ent.Comp.WireRange;

        _audio.PlayPvs(ent.Comp.LaunchSound, ent, AudioParams.Default.WithMaxDistance(40f));
        if (TryComp<DHBackblastComponent>(ent, out var blast))
            Backblast(blast, user, origin, direction);

        // Пуск засекается контрбатарейной разведкой противника, как выстрел миномёта (под сетью — со второго).
        DHAssaultFaction? faction = TryComp<DHAssaultFighterComponent>(user, out var fighter) ? fighter.Faction : null;
        RaiseLocalEvent(new DHArtilleryFiredEvent(ent, faction));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DHGuidedMissileComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var missile, out var physics, out var xform))
        {
            if (missile.WireCut)
                continue;

            var position = _transform.GetWorldPosition(xform);
            if (TerminatingOrDeleted(missile.Launcher) ||
                !TryComp<DHATGMComponent>(missile.Launcher, out var atgm) ||
                atgm.Operator == null ||
                Vector2.Distance(position, missile.Origin) > missile.WireRange)
            {
                missile.WireCut = true;
                continue;
            }

            if (atgm.Aim is not { } aim || (aim - position).LengthSquared() < 0.25f ||
                physics.LinearVelocity.LengthSquared() < 0.01f)
            {
                continue;
            }

            var current = physics.LinearVelocity.ToWorldAngle();
            var wanted = (aim - position).ToWorldAngle();
            var delta = Angle.ShortestDistance(current, wanted);
            var step = MathHelper.DegreesToRadians(missile.TurnRate) * frameTime;
            var turn = Math.Clamp(delta.Theta, -step, step);
            var heading = new Angle(current.Theta + turn);
            var velocity = heading.ToWorldVec() * missile.Speed;
            _physics.SetLinearVelocity(uid, velocity, body: physics);

            var spriteAngle = TryComp<ProjectileComponent>(uid, out var projectile) ? projectile.Angle : Angle.Zero;
            _transform.SetWorldRotation(uid, heading + spriteAngle);
        }
    }

    #endregion
}
