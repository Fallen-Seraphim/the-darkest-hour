using System.Numerics;
using Content.Server.Construction;
using Content.Server.Stack;
using Content.Shared._DarkestHour.Fortifications;
using Content.Shared.Burial.Components;
using Content.Shared.Climbing.Events;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Fortifications;

/// <summary>
/// Мешки с песком (наполнить лопатой на грунте планеты, построить баррикаду лицом туда, куда смотришь, разобрать
/// лопатой) и палатки (развернуть из свёрнутой, свернуть альтернативным действием; медицинская лечит тех, кто внутри).
/// </summary>
public sealed partial class DHFortificationSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly ProtoId<ToolQualityPrototype> CuttingQuality = "Cutting";
    private static readonly SoundSpecifier WireSound = new SoundPathSpecifier("/Audio/_RMC14/Items/barbed_wire_movement.ogg",
        AudioParams.Default.WithVariation(0.35f));

    /// <summary>Внутренность палатки относительно её центра (по стенкам палаток RMC).</summary>
    private static readonly Box2 TentInterior = new(-1.4f, -2.5f, 0.4f, 0.4f);

    /// <summary>Вся палатка со стенками — место для развёртывания.</summary>
    private static readonly Box2 TentBounds = new(-1.5f, -2.5f, 0.5f, 1.5f);

    private static readonly TimeSpan HealInterval = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHSandbagsFullComponent, UseInHandEvent>(OnSandbagsUse);
        SubscribeLocalEvent<DHSandbagsFullComponent, DHSandbagBuildDoAfterEvent>(OnSandbagsBuilt);
        SubscribeLocalEvent<DHSandbagsEmptyComponent, InteractUsingEvent>(OnEmptyInteractUsing);
        SubscribeLocalEvent<DHSandbagsEmptyComponent, DHSandbagFillDoAfterEvent>(OnEmptyFilled);
        SubscribeLocalEvent<DHSandbagBarricadeComponent, InteractUsingEvent>(OnBarricadeInteractUsing);
        SubscribeLocalEvent<DHSandbagBarricadeComponent, DHSandbagDismantleDoAfterEvent>(OnBarricadeDismantled);
        SubscribeLocalEvent<DHSandbagBarricadeComponent, DHSandbagWireDoAfterEvent>(OnBarricadeWired);
        SubscribeLocalEvent<DHSandbagBarricadeComponent, DHSandbagUnwireDoAfterEvent>(OnBarricadeUnwired);
        SubscribeLocalEvent<DHSandbagBarricadeComponent, ClimbedOnEvent>(OnBarricadeClimbed);

        // Колючка на сетке-рабице: раньше конструкции (иначе кусачки режут сам забор).
        SubscribeLocalEvent<DHWireableComponent, InteractUsingEvent>(OnWireableInteractUsing,
            before: new[] { typeof(ConstructionSystem) });
        SubscribeLocalEvent<DHWireableComponent, DHWireOnDoAfterEvent>(OnWireableWired);
        SubscribeLocalEvent<DHWireableComponent, DHWireOffDoAfterEvent>(OnWireableUnwired);
        SubscribeLocalEvent<DHWireableComponent, ClimbedOnEvent>(OnWireableClimbed);
        SubscribeLocalEvent<DHWireableComponent, ConstructionChangeEntityEvent>(OnWireableChanged);

        SubscribeLocalEvent<DHFoldedTentComponent, UseInHandEvent>(OnFoldedTentUse);
        SubscribeLocalEvent<DHFoldedTentComponent, DHTentDeployDoAfterEvent>(OnTentDeployed);
        SubscribeLocalEvent<DHTentComponent, GetVerbsEvent<AlternativeVerb>>(OnTentVerbs);
        SubscribeLocalEvent<DHTentComponent, DHTentFoldDoAfterEvent>(OnTentFolded);
    }

    #region Мешки с песком

    /// <summary>Клетка перед пользователем и направление его взгляда (по сторонам света).</summary>
    private (EntityCoordinates Coordinates, Angle Facing)? FrontTile(EntityUid user)
    {
        var xform = Transform(user);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return null;

        var dir = xform.LocalRotation.GetCardinalDir();
        var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        return (_map.GridTileToLocal(grid, gridComp, tile), dir.ToAngle());
    }

    /// <summary>В клетке уже стоит стена или баррикада, смотрящая туда же.</summary>
    private bool TileBlocked(EntityCoordinates coordinates, Angle facing)
    {
        if (coordinates.EntityId is not { Valid: true } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return true;

        var anchored = _map.GetAnchoredEntities(grid, gridComp, _map.TileIndicesFor(grid, gridComp, coordinates));
        while (anchored.MoveNext(out var uid))
        {
            if (_tag.HasTag(uid.Value, WallTag))
                return true;

            if (HasComp<DHSandbagBarricadeComponent>(uid) &&
                (Transform(uid.Value).LocalRotation - facing).Reduced().EqualsApprox(Angle.Zero, 0.1))
            {
                return true;
            }
        }

        return false;
    }

    private void OnSandbagsUse(Entity<DHSandbagsFullComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (_stack.GetCount(ent.Owner) < ent.Comp.BuildCost)
        {
            _popup.PopupEntity(Loc.GetString("dh-sandbags-not-enough", ("count", ent.Comp.BuildCost)), args.User, args.User);
            return;
        }

        if (FrontTile(args.User) is not { } front || TileBlocked(front.Coordinates, front.Facing))
        {
            _popup.PopupEntity(Loc.GetString("dh-sandbags-blocked"), args.User, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.BuildTime,
            new DHSandbagBuildDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnSandbagsBuilt(Entity<DHSandbagsFullComponent> ent, ref DHSandbagBuildDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || FrontTile(args.User) is not { } front ||
            TileBlocked(front.Coordinates, front.Facing) || !_stack.TryUse(ent.Owner, ent.Comp.BuildCost))
        {
            return;
        }

        args.Handled = true;
        var barricade = Spawn(ent.Comp.Barricade, front.Coordinates);
        _transform.SetLocalRotation(barricade, front.Facing);
        _transform.AnchorEntity(barricade, front.Coordinates);
        _popup.PopupEntity(Loc.GetString("dh-sandbags-built"), args.User, args.User);
    }

    private void OnEmptyInteractUsing(Entity<DHSandbagsEmptyComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<ShovelComponent>(args.Used))
            return;

        args.Handled = true;
        if (!OnGround(args.User))
        {
            _popup.PopupEntity(Loc.GetString("dh-sandbags-need-ground"), args.User, args.User);
            return;
        }

        var count = _stack.GetCount(ent.Owner);
        var delay = TimeSpan.FromSeconds(Math.Min(10.0, count * ent.Comp.FillTimePerBag.TotalSeconds));
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, delay, new DHSandbagFillDoAfterEvent(), ent,
            ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    /// <summary>Стоит на грунте планеты (сетка карты с биомом), а не на полу постройки.</summary>
    private bool OnGround(EntityUid user)
    {
        var xform = Transform(user);
        return xform.GridUid is { } grid && HasComp<BiomeComponent>(grid);
    }

    private void OnEmptyFilled(Entity<DHSandbagsEmptyComponent> ent, ref DHSandbagFillDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var count = _stack.GetCount(ent.Owner);
        var coordinates = Transform(ent).Coordinates;
        var wasHeld = _hands.IsHolding(args.User, ent.Owner);
        QueueDel(ent);

        foreach (var full in _stack.SpawnMultipleAtPosition(ent.Comp.Full, count, coordinates))
        {
            if (wasHeld)
                _hands.PickupOrDrop(args.User, full);
        }

        _popup.PopupEntity(Loc.GetString("dh-sandbags-filled", ("count", count)), args.User, args.User);
    }

    private void OnBarricadeInteractUsing(Entity<DHSandbagBarricadeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Колючка: натянуть моток поверх мешков.
        if (TryComp<DHBarbedWireComponent>(args.Used, out var wire))
        {
            args.Handled = true;
            if (ent.Comp.Wired)
            {
                _popup.PopupEntity(Loc.GetString("dh-sandbag-already-wired"), ent, args.User);
                return;
            }

            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, wire.WireTime,
                new DHSandbagWireDoAfterEvent(), ent, ent, args.Used)
            {
                BreakOnMove = true,
                NeedHand = true,
            });
            return;
        }

        // Срезать колючку кусачками.
        if (ent.Comp.Wired && _tool.HasQuality(args.Used, CuttingQuality.Id))
        {
            args.Handled = _tool.UseTool(args.Used, args.User, ent, 2f, CuttingQuality, new DHSandbagUnwireDoAfterEvent());
            return;
        }

        if (!HasComp<ShovelComponent>(args.Used))
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(5),
            new DHSandbagDismantleDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnBarricadeDismantled(Entity<DHSandbagBarricadeComponent> ent, ref DHSandbagDismantleDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        // Порванные мешки не вернуть: чем сильнее побита баррикада, тем меньше.
        var refund = ent.Comp.Refund;
        if (TryComp<DamageableComponent>(ent, out var damageable))
            refund = Math.Max(1, refund - (int) (_damageable.GetTotalDamage((ent.Owner, damageable)).Float() / 100f));

        _stack.SpawnMultipleAtPosition(ent.Comp.Sandbags, refund, Transform(ent).Coordinates);
        if (ent.Comp.Wired)
            Spawn(ent.Comp.Wire, Transform(ent).Coordinates);
        QueueDel(ent);
    }

    private void OnBarricadeWired(Entity<DHSandbagBarricadeComponent> ent, ref DHSandbagWireDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Wired || args.Used is not { } wire || !_stack.TryUse(wire, 1))
            return;

        args.Handled = true;
        SetWired(ent, true);
        _audio.PlayPvs(WireSound, ent);
        _popup.PopupEntity(Loc.GetString("dh-sandbag-wired", ("user", args.User)), ent, PopupType.Small);
    }

    private void OnBarricadeUnwired(Entity<DHSandbagBarricadeComponent> ent, ref DHSandbagUnwireDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !ent.Comp.Wired)
            return;

        args.Handled = true;
        SetWired(ent, false);
        _hands.PickupOrDrop(args.User, Spawn(ent.Comp.Wire, Transform(ent).Coordinates));
    }

    private void SetWired(Entity<DHSandbagBarricadeComponent> ent, bool wired)
    {
        ent.Comp.Wired = wired;
        _appearance.SetData(ent, DHSandbagVisuals.Wired, wired);
    }

    /// <summary>Перелез через колючку — порезался.</summary>
    #region Колючка на сетке-рабице

    private void OnWireableInteractUsing(Entity<DHWireableComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<DHBarbedWireComponent>(args.Used, out var wire))
        {
            args.Handled = true;
            if (ent.Comp.Wired)
            {
                _popup.PopupEntity(Loc.GetString("dh-wireable-already-wired"), ent, args.User);
                return;
            }

            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, wire.WireTime,
                new DHWireOnDoAfterEvent(), ent, ent, args.Used)
            {
                BreakOnMove = true,
                NeedHand = true,
            });
            return;
        }

        // С колючкой кусачки сначала срезают её, а не сам забор.
        if (ent.Comp.Wired && _tool.HasQuality(args.Used, CuttingQuality.Id))
            args.Handled = _tool.UseTool(args.Used, args.User, ent, 2f, CuttingQuality, new DHWireOffDoAfterEvent());
    }

    private void OnWireableWired(Entity<DHWireableComponent> ent, ref DHWireOnDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Wired || args.Used is not { } wire || !_stack.TryUse(wire, 1))
            return;

        args.Handled = true;
        SetWireableWired(ent, true);
        _audio.PlayPvs(WireSound, ent);
        _popup.PopupEntity(Loc.GetString("dh-wireable-wired", ("user", args.User)), ent, PopupType.Small);
    }

    private void OnWireableUnwired(Entity<DHWireableComponent> ent, ref DHWireOffDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !ent.Comp.Wired)
            return;

        args.Handled = true;
        SetWireableWired(ent, false);
        _hands.PickupOrDrop(args.User, Spawn(ent.Comp.Wire, Transform(ent).Coordinates));
    }

    private void SetWireableWired(Entity<DHWireableComponent> ent, bool wired)
    {
        ent.Comp.Wired = wired;
        _appearance.SetData(ent, DHWireVisuals.Wired, wired);
    }

    private void OnWireableClimbed(Entity<DHWireableComponent> ent, ref ClimbedOnEvent args)
    {
        if (!ent.Comp.Wired)
            return;

        _damageable.TryChangeDamage(args.Climber, ent.Comp.WireDamage, origin: ent.Owner);
        _audio.PlayPvs(WireSound, ent);
        _popup.PopupEntity(Loc.GetString("dh-sandbag-wire-cut"), args.Climber, args.Climber, PopupType.SmallCaution);
    }

    /// <summary>Забор сломали (стал обломком) или перестроили: колючка переходит на новый или падает мотком.</summary>
    private void OnWireableChanged(Entity<DHWireableComponent> ent, ConstructionChangeEntityEvent args)
    {
        if (!ent.Comp.Wired)
            return;

        if (TryComp<DHWireableComponent>(args.New, out var next))
            SetWireableWired((args.New, next), true);
        else
            Spawn(ent.Comp.Wire, Transform(ent).Coordinates);
    }

    #endregion

    private void OnBarricadeClimbed(Entity<DHSandbagBarricadeComponent> ent, ref ClimbedOnEvent args)
    {
        if (!ent.Comp.Wired)
            return;

        _damageable.TryChangeDamage(args.Climber, ent.Comp.WireDamage, origin: ent.Owner);
        _audio.PlayPvs(WireSound, ent);
        _popup.PopupEntity(Loc.GetString("dh-sandbag-wire-cut"), args.Climber, args.Climber, PopupType.SmallCaution);
    }

    #endregion

    #region Палатки

    private EntityCoordinates? TentSite(EntityUid user)
    {
        // Палатка встаёт входом на юг; пользователь стоит у входа, сама палатка — севернее.
        var xform = Transform(user);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return null;

        var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates) + new Vector2i(1, 2);
        return _map.GridTileToLocal(grid, gridComp, tile);
    }

    private bool TentSiteFree(EntityCoordinates site)
    {
        var center = _transform.ToMapCoordinates(site);
        var box = TentBounds.Translated(center.Position);
        foreach (var uid in _lookup.GetEntitiesIntersecting(center.MapId, box, LookupFlags.Static))
        {
            if (TryComp<PhysicsComponent>(uid, out var physics) && physics.BodyType == BodyType.Static &&
                physics.CanCollide && !HasComp<MapGridComponent>(uid))
            {
                return false;
            }
        }

        return true;
    }

    private void OnFoldedTentUse(Entity<DHFoldedTentComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (TentSite(args.User) is not { } site || !TentSiteFree(site))
        {
            _popup.PopupEntity(Loc.GetString("dh-tent-no-room"), args.User, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.DeployTime,
            new DHTentDeployDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnTentDeployed(Entity<DHFoldedTentComponent> ent, ref DHTentDeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || TentSite(args.User) is not { } site || !TentSiteFree(site))
            return;

        args.Handled = true;
        var tent = Spawn(ent.Comp.Tent, site);
        _transform.AnchorEntity(tent, site);
        QueueDel(ent);
        _popup.PopupEntity(Loc.GetString("dh-tent-deployed"), args.User, args.User);
    }

    private void OnTentVerbs(Entity<DHTentComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dh-tent-fold-verb"),
            Act = () => _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.FoldTime,
                new DHTentFoldDoAfterEvent(), ent, ent)
            {
                BreakOnMove = true,
                NeedHand = true,
            }),
        });
    }

    private void OnTentFolded(Entity<DHTentComponent> ent, ref DHTentFoldDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var folded = Spawn(ent.Comp.Folded, Transform(args.User).Coordinates);
        _hands.PickupOrDrop(args.User, folded);
        QueueDel(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var tents = EntityQueryEnumerator<DHTentComponent, TransformComponent>();
        while (tents.MoveNext(out _, out var tent, out var xform))
        {
            if (tent.Heal == null || now < tent.NextHeal)
                continue;

            tent.NextHeal = now + HealInterval;
            var center = _transform.GetMapCoordinates(xform);
            var box = TentInterior.Translated(center.Position);
            var inside = new HashSet<Entity<MobStateComponent>>();
            _lookup.GetEntitiesIntersecting(center.MapId, box, inside);
            foreach (var mob in inside)
            {
                if (_mobState.IsDead(mob))
                    continue;

                _damageable.TryChangeDamage(mob.Owner, tent.Heal, true, false);
            }
        }
    }

    #endregion
}
