using System.Linq;
using System.Numerics;
using Content.Server._DarkestHour.Carrier;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Carrier;
using Content.Shared._DarkestHour.HeavyGuns;
using Content.Shared._DarkestHour.Paradrop;
using Content.Shared._DarkestHour.Supply;
using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Supply;

/// <summary>
/// Лифт техники (план осады п. 4, решения автора 2026-11-19 и 2026-11-26): платформа 5×5, у каждой стороны тяжёлый
/// (танк) и лёгкий (БТР, заказанные хамви); поддон сброса поднимается либо поддоном, либо распакованной машиной. В трюме — поддоны
/// сброса (со старта — танк и БТР) и опущенная туда техника; компьютер поднимает выбранное и опускает то, что стоит на
/// поднятой платформе (поддоны, машины без экипажа, орудия). Пока ангар повреждён (п. 19), лифт стоит.
/// </summary>
public sealed partial class DHVehicleLiftSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DHCarrierDamageSystem _carrierDamage = default!;
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHVehicleLiftComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DHVehicleLiftComponent, ComponentShutdown>(OnShutdown);

        Subs.BuiEvents<DHVehicleLiftConsoleComponent>(DHVehicleLiftUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<DHVehicleLiftRaiseMessage>(OnRaise);
            subs.Event<DHVehicleLiftLowerMessage>(OnLower);
        });
    }

    #region Лифт

    private void OnMapInit(Entity<DHVehicleLiftComponent> ent, ref MapInitEvent args)
    {
        foreach (var proto in ent.Comp.Starting)
        {
            ent.Comp.Stored.Add(Spawn(proto, MapCoordinates.Nullspace));
        }

        SetMode(ent, ent.Comp.Mode);
    }

    private void OnShutdown(Entity<DHVehicleLiftComponent> ent, ref ComponentShutdown args)
    {
        foreach (var stored in ent.Comp.Stored)
        {
            QueueDel(stored);
        }

        ent.Comp.Stored.Clear();
    }

    private void SetMode(Entity<DHVehicleLiftComponent> ent, DHSupplyElevatorMode mode)
    {
        ent.Comp.Mode = mode;
        Dirty(ent);
        _appearance.SetData(ent, DHSupplyElevatorVisuals.Mode, mode);

        // Пока платформа не наверху, в яму не въехать.
        if (_fixtures.GetFixtureOrNull(ent, ent.Comp.PitFixture) is { } pit)
            _physics.SetHard(ent, pit, mode != DHSupplyElevatorMode.Raised);
    }

    private IEnumerable<EntityUid> OnPlatform(Entity<DHVehicleLiftComponent> ent)
    {
        var xform = Transform(ent);
        var center = _transform.GetWorldPosition(xform);
        var half = ent.Comp.Radius + 0.45f;
        var box = new Box2(center - new Vector2(half, half), center + new Vector2(half, half));
        foreach (var uid in _lookup.GetEntitiesIntersecting(xform.MapID, box,
                     LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Static))
        {
            if (uid != ent.Owner && Transform(uid).ParentUid == xform.ParentUid)
                yield return uid;
        }
    }

    /// <summary>Что лифт может опустить в трюм: поддоны, техника, орудия.</summary>
    private bool Liftable(EntityUid uid)
    {
        return HasComp<DHVehicleComponent>(uid) || HasComp<DHDropPalletComponent>(uid) ||
               HasComp<DHParadroppableComponent>(uid);
    }

    /// <summary>В машине кто-то есть (экипаж в отсеке).</summary>
    private bool Crewed(EntityUid uid)
    {
        if (!TryComp<DHVehicleComponent>(uid, out var vehicle) || vehicle.InteriorMap is not { } interior)
            return false;

        var mobs = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobs.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapUid == interior)
                return true;
        }

        return false;
    }

    private string Blocked(EntityUid uid)
    {
        if (_carrierDamage.Damaged(Transform(uid).GridUid, DHCarrierCompartmentKind.Hangar))
            return Loc.GetString("dh-vehicle-lift-blocked-hangar");

        return string.Empty;
    }

    /// <summary>Название в трюме: у поддона — машина, которая в нём.</summary>
    private string StoredName(EntityUid uid)
    {
        if (TryComp<DHDropPalletComponent>(uid, out var pallet) &&
            _prototypes.TryIndex(pallet.Spawn, out var proto))
            return proto.Name;

        return Name(uid);
    }

    /// <summary>Лифт стороны нужного типа (для заказанной техники).</summary>
    public Entity<DHVehicleLiftComponent>? FindLift(DHAssaultFaction faction, DHVehicleLiftKind kind)
    {
        var query = EntityQueryEnumerator<DHVehicleLiftComponent>();
        while (query.MoveNext(out var uid, out var lift))
        {
            if (lift.Faction == faction && lift.Kind == kind)
                return (uid, lift);
        }

        return null;
    }

    /// <summary>Положить в трюм (сущность уходит с карты).</summary>
    public void Store(Entity<DHVehicleLiftComponent> lift, EntityUid thing)
    {
        _transform.DetachEntity(thing, Transform(thing));
        lift.Comp.Stored.Add(thing);
        UpdateAllConsoles();
    }

    #endregion

    #region Компьютер

    private Entity<DHVehicleLiftComponent>? FindLift(Entity<DHVehicleLiftConsoleComponent> console)
    {
        var xform = Transform(console);
        Entity<DHVehicleLiftComponent>? best = null;
        var bestDistance = console.Comp.LiftRange;
        var query = EntityQueryEnumerator<DHVehicleLiftComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var lift, out var liftXform))
        {
            if (liftXform.GridUid != xform.GridUid || liftXform.GridUid == null)
                continue;

            var distance = (liftXform.LocalPosition - xform.LocalPosition).Length();
            if (distance > bestDistance)
                continue;

            best = (uid, lift);
            bestDistance = distance;
        }

        return best;
    }

    private void OnUiOpened(Entity<DHVehicleLiftConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<DHVehicleLiftConsoleComponent> ent)
    {
        var state = new DHVehicleLiftState();
        if (FindLift(ent) is { } lift)
        {
            state.HasLift = true;
            state.Mode = lift.Comp.Mode;
            state.Kind = lift.Comp.Kind;
            state.AllowPallet = lift.Comp.AllowPallet;
            state.Blocked = Blocked(lift);
            state.Stored = lift.Comp.Stored
                .Where(uid => Exists(uid))
                .Select(uid => new DHVehicleLiftRow
                {
                    Entity = GetNetEntity(uid),
                    Name = StoredName(uid),
                    Pallet = HasComp<DHDropPalletComponent>(uid),
                })
                .ToList();

            if (lift.Comp.Mode == DHSupplyElevatorMode.Raised)
                state.OnPlatform = OnPlatform(lift).Where(Liftable).Select(uid => Name(uid)).ToList();
        }

        _ui.SetUiState(ent.Owner, DHVehicleLiftUiKey.Key, state);
    }

    private void UpdateAllConsoles()
    {
        var consoles = EntityQueryEnumerator<DHVehicleLiftConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            if (_ui.IsUiOpen(uid, DHVehicleLiftUiKey.Key))
                UpdateUi((uid, console));
        }
    }

    private void OnRaise(Entity<DHVehicleLiftConsoleComponent> ent, ref DHVehicleLiftRaiseMessage args)
    {
        if (FindLift(ent) is not { } lift || lift.Comp.Mode != DHSupplyElevatorMode.Lowered)
            return;

        if (Blocked(lift) is { Length: > 0 } blocked)
        {
            _popup.PopupEntity(blocked, ent, args.Actor);
            return;
        }

        if (!TryGetEntity(args.Cargo, out var cargo) || !lift.Comp.Stored.Contains(cargo.Value))
            return;

        // Поддон поднимается либо поддоном (дальше — сброс на маяк), либо уже распакованной машиной.
        var pallet = HasComp<DHDropPalletComponent>(cargo.Value);
        if (args.AsPallet && (!pallet || !lift.Comp.AllowPallet))
            return;

        lift.Comp.Stored.Remove(cargo.Value);
        lift.Comp.Cargo = cargo;
        lift.Comp.UnpackCargo = pallet && !args.AsPallet;
        lift.Comp.DoneAt = _timing.CurTime + lift.Comp.RaiseTime;
        SetMode(lift, DHSupplyElevatorMode.Raising);
        _audio.PlayPvs(lift.Comp.RaisingSound, lift);
        UpdateAllConsoles();
    }

    private void OnLower(Entity<DHVehicleLiftConsoleComponent> ent, ref DHVehicleLiftLowerMessage args)
    {
        if (FindLift(ent) is not { } lift || lift.Comp.Mode != DHSupplyElevatorMode.Raised)
            return;

        if (Blocked(lift) is { Length: > 0 } blocked)
        {
            _popup.PopupEntity(blocked, ent, args.Actor);
            return;
        }

        foreach (var uid in OnPlatform(lift))
        {
            if (HasComp<MobStateComponent>(uid))
            {
                _popup.PopupEntity(Loc.GetString("dh-vehicle-lift-people"), ent, args.Actor);
                return;
            }

            if (Liftable(uid) && Crewed(uid))
            {
                _popup.PopupEntity(Loc.GetString("dh-vehicle-lift-crewed", ("vehicle", uid)), ent, args.Actor);
                return;
            }

            // Погрузчик и прочее крупное, что в трюм не берут, — убрать с платформы.
            if (!Liftable(uid) && !Transform(uid).Anchored && !HasComp<ItemComponent>(uid))
            {
                _popup.PopupEntity(Loc.GetString("dh-vehicle-lift-clear", ("thing", uid)), ent, args.Actor);
                return;
            }
        }

        lift.Comp.DoneAt = _timing.CurTime + lift.Comp.LowerTime;
        SetMode(lift, DHSupplyElevatorMode.Lowering);
        _audio.PlayPvs(lift.Comp.LoweringSound, lift);
        UpdateAllConsoles();
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHVehicleLiftComponent>();
        while (query.MoveNext(out var uid, out var lift))
        {
            if (lift.DoneAt is not { } done || now < done)
                continue;

            lift.DoneAt = null;
            var ent = new Entity<DHVehicleLiftComponent>(uid, lift);
            if (lift.Mode == DHSupplyElevatorMode.Raising)
            {
                SetMode(ent, DHSupplyElevatorMode.Raised);
                if (lift.Cargo is { } cargo && Exists(cargo))
                {
                    var xform = Transform(uid);
                    var coordinates = new EntityCoordinates(xform.ParentUid, xform.LocalPosition);
                    if (lift.UnpackCargo && TryComp<DHDropPalletComponent>(cargo, out var pallet))
                    {
                        var vehicle = Spawn(pallet.Spawn, coordinates);
                        _transform.SetLocalRotation(vehicle, xform.LocalRotation);
                        Del(cargo);
                    }
                    else
                    {
                        _transform.SetCoordinates(cargo, coordinates);
                        _transform.SetLocalRotation(cargo, xform.LocalRotation);
                    }
                }

                lift.Cargo = null;
                lift.UnpackCargo = false;
            }
            else if (lift.Mode == DHSupplyElevatorMode.Lowering)
            {
                // Поддоны, техника и орудия уходят в трюм; мелочь на платформе (мусор, парашюты) — вниз насовсем.
                foreach (var thing in OnPlatform(ent).ToList())
                {
                    if (Liftable(thing))
                    {
                        var thingXform = Transform(thing);
                        if (thingXform.Anchored)
                            _transform.Unanchor(thing, thingXform);

                        _transform.DetachEntity(thing, thingXform);
                        lift.Stored.Add(thing);
                    }
                    else if (HasComp<ItemComponent>(thing) && !Transform(thing).Anchored)
                    {
                        QueueDel(thing);
                    }
                }

                SetMode(ent, DHSupplyElevatorMode.Lowered);
            }

            UpdateAllConsoles();
        }
    }
}
