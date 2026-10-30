using System.Numerics;
using Content.Shared._DarkestHour.Fortifications;
using Content.Shared.Actions;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._DarkestHour.Weapons.Ranged.Emplacements;

/// <summary>
/// Станковое оружие: развернуть станок перед собой, встать за него (пристегнуться к ремню-страпу станка),
/// стрелять в секторе перед станком, перезаряжать лентой, сложить обратно.
/// По образцу SharedWeaponMountSystem / MountableWeaponSystem из RMC-14 (MIT), без навыков морпехов,
/// ксеноморфов, баррикад и транспорта.
/// </summary>
public sealed partial class DHWeaponMountSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private DHOverheatSystem _overheat = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private DHTrenchCoverSystem _trench = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    private const string MagazineSlot = "gun_magazine";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHWeaponMountComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DHWeaponMountComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<DHWeaponMountComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DHWeaponMountComponent, GetVerbsEvent<AlternativeVerb>>(OnAltVerbs);
        SubscribeLocalEvent<DHWeaponMountComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DHWeaponMountComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<DHWeaponMountComponent, StrapAttemptEvent>(OnStrapAttempt);
        SubscribeLocalEvent<DHWeaponMountComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<DHWeaponMountComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<DHWeaponMountComponent, DHMountDeployDoAfterEvent>(OnDeployDoAfter);
        SubscribeLocalEvent<DHWeaponMountComponent, DHMountUndeployDoAfterEvent>(OnUndeployDoAfter);

        SubscribeLocalEvent<DHMountableWeaponComponent, AttemptShootEvent>(OnWeaponAttemptShoot);
        SubscribeLocalEvent<DHMountableWeaponComponent, DHGunShotOriginEvent>(OnWeaponShotOrigin);
        SubscribeLocalEvent<DHMountableWeaponComponent, GunShotEvent>(OnWeaponShot);
        SubscribeLocalEvent<DHMountableWeaponComponent, EntInsertedIntoContainerMessage>(OnWeaponMagazineChanged);
        SubscribeLocalEvent<DHMountableWeaponComponent, EntRemovedFromContainerMessage>(OnWeaponMagazineChanged);
        SubscribeLocalEvent<DHMountableWeaponComponent, DHWeaponOverheatedEvent>(OnWeaponOverheated);
        SubscribeLocalEvent<DHMountableWeaponComponent, GunRefreshModifiersEvent>(OnWeaponRefreshModifiers);

        SubscribeLocalEvent<DHWeaponControllerComponent, DHDismountWeaponActionEvent>(OnDismountAction);
        SubscribeLocalEvent<DHWeaponControllerComponent, MobStateChangedEvent>(OnControllerMobState);
    }

    // ------------------------------------------------------------------ mount

    private void OnMapInit(Entity<DHWeaponMountComponent> ent, ref MapInitEvent args)
    {
        _buckle.StrapSetEnabled(ent, ent.Comp.Deployed);
        UpdateAppearance(ent);

        if (_net.IsClient)
            return;

        var container = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.WeaponSlotId);
        container.OccludesLight = false;
        if (container.ContainedEntity != null)
            return;

        var weapon = Spawn(ent.Comp.Weapon, Transform(ent).Coordinates);
        if (!_container.Insert(weapon, container))
        {
            Del(weapon);
            return;
        }

        ent.Comp.MountedWeapon = weapon;
        Dirty(ent);

        if (TryComp(weapon, out DHMountableWeaponComponent? mountable))
        {
            mountable.MountedTo = ent;
            Dirty(weapon, mountable);
        }

        UpdateAppearance(ent);
    }

    private void OnPickupAttempt(Entity<DHWeaponMountComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (ent.Comp.Deployed)
            args.Cancel();
    }

    private void OnUseInHand(Entity<DHWeaponMountComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Deployed)
            return;

        args.Handled = true;
        if (!CanDeploy(ent, args.User, out _, out _, popup: true))
            return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.DeployDelay,
            new DHMountDeployDoAfterEvent(), ent, ent, ent)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
        });
    }

    /// <summary>
    /// Станок ставится на соседний тайл в ту сторону, куда смотрит стрелок, и смотрит туда же.
    /// </summary>
    private bool CanDeploy(Entity<DHWeaponMountComponent> ent, EntityUid user, out EntityCoordinates coords, out Angle rotation, bool popup)
    {
        coords = default;
        var userXform = Transform(user);
        var dir = _transform.GetWorldRotation(userXform).GetCardinalDir();
        rotation = dir.ToAngle();

        if (userXform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
        {
            if (popup)
                _popup.PopupClient(Loc.GetString("dh-weapon-mount-no-floor"), user, user);
            return false;
        }

        var tile = _map.TileIndicesFor(gridUid, grid, userXform.Coordinates) + dir.ToIntVec();
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || _turf.IsSpace(tileRef) ||
            _turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable | CollisionGroup.HighImpassable, grid))
        {
            if (popup)
                _popup.PopupClient(Loc.GetString("dh-weapon-mount-blocked"), user, user);
            return false;
        }

        // Ориентация относительно грида: стрелок мог повернуться в мировых координатах.
        rotation = (rotation - _transform.GetWorldRotation(gridUid)).GetCardinalDir().ToAngle();
        coords = _map.GridTileToLocal(gridUid, grid, tile);
        return true;
    }

    private void OnDeployDoAfter(Entity<DHWeaponMountComponent> ent, ref DHMountDeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Deployed)
            return;

        args.Handled = true;
        if (!CanDeploy(ent, args.User, out var coords, out var rotation, popup: true))
            return;

        _hands.TryDrop(args.User, ent.Owner, checkActionBlocker: false, doDropInteraction: false);

        var xform = Transform(ent);
        _transform.SetCoordinates(ent, xform, coords, rotation: rotation);
        _transform.AnchorEntity((ent.Owner, xform));

        ent.Comp.Deployed = true;
        ent.Comp.Nested = IsNest(coords, args.User);
        Dirty(ent);
        RefreshWeapon(ent);
        _buckle.StrapSetEnabled(ent, true);
        UpdateAppearance(ent);
        _audio.PlayPredicted(ent.Comp.DeploySound, ent, args.User);

        // Стрелок стоит прямо за развёрнутым станком — сразу ставим его к пулемёту.
        _buckle.TryBuckle(args.User, args.User, ent, popup: false);
    }

    /// <summary>Гнездо: на клетке станка окоп или мешки с песком, или стрелок ставит станок из окопа.</summary>
    private bool IsNest(EntityCoordinates mountCoords, EntityUid user)
    {
        if (_trench.TrenchAt(mountCoords) != null || _trench.TrenchOf(user) != null)
            return true;

        if (_transform.GetGrid(mountCoords) is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, _map.TileIndicesFor(gridUid, grid, mountCoords));
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<DHSandbagBarricadeComponent>(uid))
                return true;
        }

        return false;
    }

    private void RefreshWeapon(Entity<DHWeaponMountComponent> ent)
    {
        if (ent.Comp.MountedWeapon is { } weapon)
            _gun.RefreshModifiers(weapon);
    }

    private void OnWeaponRefreshModifiers(Entity<DHMountableWeaponComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (ent.Comp.MountedTo is not { } mount || !TryComp(mount, out DHWeaponMountComponent? mountComp) ||
            !mountComp.Deployed || !mountComp.Nested)
        {
            return;
        }

        var k = mountComp.NestSpreadMultiplier;
        args.MinAngle *= k;
        args.MaxAngle *= k;
        args.AngleIncrease *= k;
        args.CameraRecoilScalar *= k;
    }

    private void TryUndeploy(Entity<DHWeaponMountComponent> ent, EntityUid user)
    {
        if (!ent.Comp.Deployed)
            return;

        if (ent.Comp.User != null)
        {
            _popup.PopupClient(Loc.GetString("dh-weapon-mount-occupied"), ent, user);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.UndeployDelay,
            new DHMountUndeployDoAfterEvent(), ent, ent)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
        });
    }

    private void OnUndeployDoAfter(Entity<DHWeaponMountComponent> ent, ref DHMountUndeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !ent.Comp.Deployed || ent.Comp.User != null)
            return;

        args.Handled = true;
        var xform = Transform(ent);
        _transform.Unanchor(ent, xform);
        _transform.SetLocalRotation(ent, Angle.Zero, xform);

        ent.Comp.Deployed = false;
        ent.Comp.Nested = false;
        Dirty(ent);
        RefreshWeapon(ent);
        _buckle.StrapSetEnabled(ent, false);
        UpdateAppearance(ent);
        _audio.PlayPredicted(ent.Comp.UndeploySound, ent, args.User);
        _hands.TryPickupAnyHand(args.User, ent);
    }

    private void RotateMount(Entity<DHWeaponMountComponent> ent, Angle localRotation, EntityUid? user)
    {
        var xform = Transform(ent);
        if (xform.LocalRotation.EqualsApprox(localRotation))
            return;

        _transform.SetLocalRotation(ent, localRotation, xform);
        _audio.PlayPredicted(ent.Comp.RotateSound, ent, user);
    }

    private void OnInteractUsing(Entity<DHWeaponMountComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.MountedWeapon is not { } weapon)
            return;

        // Перезарядка: лента или короб вставляется прямо в пулемёт на станке.
        if (!_slots.TryGetSlot(weapon, MagazineSlot, out var slot) || !_slots.CanInsert(weapon, slot, args.Used, args.User, swap: true))
            return;

        args.Handled = true;
        if (slot.Item != null)
            _slots.TryEjectToHands(weapon, slot, args.User, excludeUserAudio: true);

        _slots.TryInsert(weapon, slot, args.Used, args.User);
        UpdateAppearance(ent);
    }

    private void OnAltVerbs(Entity<DHWeaponMountComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        if (ent.Comp.Deployed && ent.Comp.User == null)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dh-weapon-mount-verb-undeploy"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/fold.svg.192dpi.png")),
                Act = () => TryUndeploy(ent, user),
                Priority = 2,
            });

            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dh-weapon-mount-verb-rotate"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/rotate_cw.svg.192dpi.png")),
                Act = () => RotateMount(ent, Transform(ent).LocalRotation - Angle.FromDegrees(90), user),
                Priority = 1,
            });
        }

        if (ent.Comp.MountedWeapon is { } weapon &&
            _slots.TryGetSlot(weapon, MagazineSlot, out var slot) &&
            slot.Item is { } magazine)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dh-weapon-mount-verb-eject"),
                IconEntity = GetNetEntity(magazine),
                Act = () =>
                {
                    _slots.TryEjectToHands(weapon, slot, user, excludeUserAudio: true);
                    UpdateAppearance(ent);
                },
                Priority = 0,
            });
        }
    }

    private void OnExamined(Entity<DHWeaponMountComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(DHWeaponMountComponent)))
        {
            if (ent.Comp.MountedWeapon is { } weapon)
            {
                var ammo = new GetAmmoCountEvent();
                RaiseLocalEvent(weapon, ref ammo);
                args.PushMarkup(Loc.GetString("dh-weapon-mount-examine-ammo",
                    ("count", ammo.Count), ("capacity", ammo.Capacity)));

                if (TryComp(weapon, out DHOverheatComponent? overheat))
                {
                    var percent = (int) MathF.Round(_overheat.GetHeat(overheat) / overheat.MaxHeat * 100f);
                    args.PushMarkup(Loc.GetString(_overheat.IsOverheated(overheat)
                        ? "dh-overheat-examine-locked"
                        : "dh-overheat-examine", ("percent", percent)));
                }
            }

            args.PushMarkup(Loc.GetString(ent.Comp.Deployed
                ? "dh-weapon-mount-examine-deployed"
                : "dh-weapon-mount-examine-folded"));

            if (ent.Comp.Deployed && ent.Comp.Nested)
                args.PushMarkup(Loc.GetString("dh-weapon-mount-examine-nest"));
        }
    }

    // ------------------------------------------------------------------ gunner

    private void OnStrapAttempt(Entity<DHWeaponMountComponent> ent, ref StrapAttemptEvent args)
    {
        var gunner = args.Buckle.Owner;
        if (!ent.Comp.Deployed || args.User != gunner)
        {
            args.Cancelled = true;
            return;
        }

        if (_hands.CountFreeHands(gunner) < _hands.GetHandCount(gunner))
        {
            if (args.Popup)
                _popup.PopupClient(Loc.GetString("dh-weapon-mount-need-hands"), ent, gunner);
            args.Cancelled = true;
            return;
        }

        // Встать к пулемёту можно только сзади.
        var mountPos = _transform.GetWorldPosition(ent);
        var toGunner = _transform.GetWorldPosition(gunner) - mountPos;
        var front = _transform.GetWorldRotation(ent).ToWorldVec();
        if (toGunner.Length() > 1.6f || Vector2.Dot(toGunner, front) > 0.25f)
        {
            if (args.Popup)
                _popup.PopupClient(Loc.GetString("dh-weapon-mount-stand-behind"), ent, gunner);
            args.Cancelled = true;
        }
    }

    private void OnStrapped(Entity<DHWeaponMountComponent> ent, ref StrappedEvent args)
    {
        var gunner = args.Buckle.Owner;
        ent.Comp.User = gunner;

        var controller = EnsureComp<DHWeaponControllerComponent>(gunner);
        controller.Weapon = ent.Comp.MountedWeapon;
        controller.Mount = ent;
        Dirty(gunner, controller);

        // Действие «отойти от пулемёта» живёт на станке и выдаётся каждому стрелку.
        _actions.AddAction(gunner, ref ent.Comp.DismountActionEntity, ent.Comp.DismountAction, ent);
        Dirty(ent);
    }

    private void OnUnstrapped(Entity<DHWeaponMountComponent> ent, ref UnstrappedEvent args)
    {
        ent.Comp.User = null;
        RemComp<DHWeaponControllerComponent>(args.Buckle);

        if (ent.Comp.DismountActionEntity is { } action)
            _actions.RemoveAction(args.Buckle.Owner, action);
        Dirty(ent);
    }

    private void OnDismountAction(Entity<DHWeaponControllerComponent> ent, ref DHDismountWeaponActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _buckle.TryUnbuckle(ent.Owner, ent.Owner, popup: false);
    }

    private void OnControllerMobState(Entity<DHWeaponControllerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            _buckle.Unbuckle(ent.Owner, null);
    }

    /// <summary>
    /// Пулемёт, из которого сейчас стреляет <paramref name="user"/>, если он стоит за станком.
    /// Используется SharedGunSystem.TryGetGun.
    /// </summary>
    public bool TryGetControlledGun(EntityUid user, out Entity<GunComponent> gun)
    {
        gun = default;
        if (!TryComp(user, out DHWeaponControllerComponent? controller) ||
            controller.Weapon is not { } weapon ||
            !TryComp(weapon, out GunComponent? gunComp))
        {
            return false;
        }

        gun = (weapon, gunComp);
        return true;
    }

    // ------------------------------------------------------------------ weapon

    private void OnWeaponAttemptShoot(Entity<DHMountableWeaponComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.MountedTo is not { } mount ||
            !TryComp(mount, out DHWeaponMountComponent? mountComp) ||
            !mountComp.Deployed)
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("dh-weapon-mount-needs-mount");
            return;
        }

        if (_hands.CountFreeHands(args.User) < ent.Comp.RequiredFreeHands)
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("dh-weapon-mount-need-hands");
            return;
        }

        if (!TryComp(ent, out GunComponent? gun) || gun.ShootCoordinates is not { } aim)
            return;

        var mountXform = Transform(mount);
        var mountPos = _transform.GetWorldPosition(mountXform);
        var aimPos = _transform.ToMapCoordinates(aim).Position;
        if ((aimPos - mountPos).LengthSquared() < 0.01f)
            return;

        var target = Angle.FromWorldVec(aimPos - mountPos);
        var front = _transform.GetWorldRotation(mountXform);
        if (Math.Abs(Angle.ShortestDistance(front, target).Degrees) <= ent.Comp.ShootArc / 2f)
            return;

        // Цель вне сектора: пулемёт не стреляет, а станок доворачивается в сторону прицела.
        args.Cancelled = true;
        if (!mountComp.RotateToAim || !_timing.IsFirstTimePredicted)
            return;

        var parentRotation = front - mountXform.LocalRotation;
        RotateMount((mount, mountComp), (target - parentRotation).GetCardinalDir().ToAngle(), args.User);
    }

    private void OnWeaponShotOrigin(Entity<DHMountableWeaponComponent> ent, ref DHGunShotOriginEvent args)
    {
        if (ent.Comp.MountedTo is not { } mount || !TryComp(mount, out DHWeaponMountComponent? mountComp))
            return;

        var xform = Transform(mount);
        args.Origin = xform.Coordinates.Offset(xform.LocalRotation.ToWorldVec() * mountComp.MuzzleOffset);
    }

    private void OnWeaponShot(Entity<DHMountableWeaponComponent> ent, ref GunShotEvent args)
    {
        if (ent.Comp.MountedTo is { } mount)
            UpdateAppearance(mount);
    }

    private void OnWeaponMagazineChanged<T>(Entity<DHMountableWeaponComponent> ent, ref T args) where T : ContainerModifiedMessage
    {
        if (ent.Comp.MountedTo is { } mount)
            UpdateAppearance(mount);
    }

    private void OnWeaponOverheated(Entity<DHMountableWeaponComponent> ent, ref DHWeaponOverheatedEvent args)
    {
        if (ent.Comp.MountedTo is not { } mount)
            return;

        if (args.Damage != null && _net.IsServer)
            _damageable.TryChangeDamage(mount, args.Damage);

        if (TryComp(mount, out DHWeaponMountComponent? mountComp) && mountComp.User is { } gunner)
            _popup.PopupClient(Loc.GetString("dh-weapon-mount-overheated", ("weapon", ent.Owner)), mount, gunner, PopupType.SmallCaution);
    }

    public void UpdateAppearance(EntityUid mount, DHWeaponMountComponent? comp = null)
    {
        if (!Resolve(mount, ref comp, false))
            return;

        var hasAmmo = false;
        if (comp.MountedWeapon is { } weapon)
        {
            var ev = new GetAmmoCountEvent();
            RaiseLocalEvent(weapon, ref ev);
            hasAmmo = ev.Count > 0;
        }

        _appearance.SetData(mount, DHWeaponMountVisuals.Deployed, comp.Deployed);
        _appearance.SetData(mount, DHWeaponMountVisuals.Ammo, hasAmmo);
    }
}

/// <summary>
/// Поднимается на оружии в SharedGunSystem.AttemptShoot: откуда летят пули. Станок переносит точку выстрела
/// к дулу, чтобы пули не цепляли сам станок и стрелка.
/// </summary>
[ByRefEvent]
public record struct DHGunShotOriginEvent(EntityCoordinates Origin);

[ByRefEvent]
public record struct DHWeaponOverheatedEvent(Content.Shared.Damage.DamageSpecifier? Damage);

public sealed partial class DHDismountWeaponActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class DHMountDeployDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class DHMountUndeployDoAfterEvent : SimpleDoAfterEvent;
