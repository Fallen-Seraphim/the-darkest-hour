using System.Numerics;
using Content.Server._DarkestHour.ControlPoints;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Aviation;
using Content.Shared._DarkestHour.Overwatch;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Actions;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Aviation;

/// <summary>
/// Штурмовик: лифт, катапульта, полёт в слое атмосферы, автопилот посадки, ЛТЦ, ПВО, крушение и катапультирование.
/// См. <see cref="DHAircraftComponent"/>.
/// </summary>
public sealed partial class DHAircraftSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private DHTacticalMapSystem _tacmap = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly ProtoId<ToolQualityPrototype> WeldingQuality = "Welding";
    private static readonly ProtoId<DamageTypePrototype> BluntType = "Blunt";
    private static readonly TimeSpan UiInterval = TimeSpan.FromSeconds(0.5);

    /// <summary>Ниже этой прочности катапульта не выпускает самолёт.</summary>
    private const float MinLaunchIntegrity = 30f;

    private TimeSpan _nextUi;

    /// <summary>Катапультирование и крушение отстёгивают пилота сами, в обход запрета «в воздухе не отстегнуться».</summary>
    private bool _forceUnbuckle;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHAircraftComponent, MapInitEvent>(OnAircraftMapInit);
        SubscribeLocalEvent<DHAircraftComponent, ComponentShutdown>(OnAircraftShutdown);
        SubscribeLocalEvent<DHAircraftComponent, ExaminedEvent>(OnAircraftExamined);
        SubscribeLocalEvent<DHAircraftComponent, InteractUsingEvent>(OnAircraftInteractUsing);
        SubscribeLocalEvent<DHAircraftComponent, DHAircraftRepairDoAfterEvent>(OnRepairDoAfter);
        SubscribeLocalEvent<DHAircraftComponent, DHAircraftLoadDoAfterEvent>(OnLoadDoAfter);
        SubscribeLocalEvent<DHAircraftComponent, GetVerbsEvent<AlternativeVerb>>(OnAircraftVerbs);

        SubscribeLocalEvent<DHAircraftSeatComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<DHAircraftSeatComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<DHAircraftSeatComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);

        SubscribeLocalEvent<DHAircraftPilotComponent, DHAircraftFlightActionEvent>(OnFlightAction);
        SubscribeLocalEvent<DHAircraftPilotComponent, DHAircraftControlActionEvent>(OnControlAction);
        SubscribeLocalEvent<DHAircraftPilotComponent, DHAircraftFlaresActionEvent>(OnFlaresAction);
        SubscribeLocalEvent<DHAircraftPilotComponent, DHAircraftEjectActionEvent>(OnEjectAction);
        SubscribeLocalEvent<DHAircraftPilotComponent, DHAircraftCannonActionEvent>(OnCannonAction);
        SubscribeLocalEvent<DHAircraftPilotComponent, DHAircraftDropActionEvent>(OnDropAction);

        Subs.BuiEvents<DHAircraftComponent>(DHAircraftUiKey.Flight, subs =>
        {
            subs.Event<DHAircraftHookMessage>(OnHookMessage);
            subs.Event<DHAircraftLaunchMessage>(OnLaunchMessage);
            subs.Event<DHAircraftReturnMessage>(OnReturnMessage);
            subs.Event<DHAircraftSpeedMessage>(OnSpeedMessage);
            subs.Event<DHAircraftControlMessage>(OnControlMessage);
            subs.Event<DHTacticalMapRequestMessage>(OnTerrainRequest);
            subs.Event<DHAircraftPassPointMessage>(OnPassPoint);
            subs.Event<DHAircraftClearPassMessage>(OnClearPass);
            subs.Event<DHAircraftStartPassMessage>(OnStartPass);
            subs.Event<DHAircraftAltitudeMessage>(OnAltitude);
            subs.Event<DHAircraftCannonMessage>(OnCannonMessage);
            subs.Event<DHAircraftDropMessage>(OnDropMessage);
        });

        SubscribeLocalEvent<DHAircraftLiftComponent, MapInitEvent>(OnLiftMapInit);
        SubscribeLocalEvent<DHAircraftLiftComponent, GetVerbsEvent<AlternativeVerb>>(OnLiftVerbs);
        SubscribeLocalEvent<DHAircraftLiftComponent, ExaminedEvent>(OnLiftExamined);
    }

    #region Корпус и кресло

    private void OnAircraftMapInit(Entity<DHAircraftComponent> ent, ref MapInitEvent args)
    {
        var seat = SpawnAttachedTo(ent.Comp.SeatPrototype, new EntityCoordinates(ent, ent.Comp.SeatOffset));
        ent.Comp.Seat = seat;
        EnsureComp<DHAircraftSeatComponent>(seat).Aircraft = ent;

        // Фонарь кабины — поверх пилота, стекло полупрозрачное.
        ent.Comp.Canopy = SpawnAttachedTo(ent.Comp.CanopyPrototype, new EntityCoordinates(ent, Vector2.Zero));
    }

    private void OnAircraftShutdown(Entity<DHAircraftComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Seat is { } seat && !TerminatingOrDeleted(seat))
            QueueDel(seat);
        if (ent.Comp.Canopy is { } canopy && !TerminatingOrDeleted(canopy))
            QueueDel(canopy);
    }

    private void OnStrapped(Entity<DHAircraftSeatComponent> ent, ref StrappedEvent args)
    {
        if (ent.Comp.Aircraft is not { } aircraft)
            return;

        var pilot = args.Buckle.Owner;
        var comp = EnsureComp<DHAircraftPilotComponent>(pilot);
        comp.Aircraft = aircraft;
        Dirty(pilot, comp);

        ent.Comp.ActionEntities.Clear();
        foreach (var action in ent.Comp.Actions)
        {
            if (_actions.AddAction(pilot, action, ent) is { } added)
                ent.Comp.ActionEntities.Add(added);
        }

        if (TryComp<DHAircraftComponent>(aircraft, out var plane) && plane.Control)
            _mover.SetRelay(pilot, aircraft);
    }

    private void OnUnstrapped(Entity<DHAircraftSeatComponent> ent, ref UnstrappedEvent args)
    {
        var pilot = args.Buckle.Owner;
        foreach (var action in ent.Comp.ActionEntities)
        {
            _actions.RemoveAction(pilot, action);
        }

        ent.Comp.ActionEntities.Clear();
        RemComp<DHAircraftPilotComponent>(pilot);
        RemComp<RelayInputMoverComponent>(pilot);

        if (ent.Comp.Aircraft is { } aircraft)
            _ui.CloseUi(aircraft, DHAircraftUiKey.Flight, pilot);
    }

    /// <summary>В воздухе и на разгоне из кресла не выйти — только катапультироваться.</summary>
    private void OnUnstrapAttempt(Entity<DHAircraftSeatComponent> ent, ref UnstrapAttemptEvent args)
    {
        if (_forceUnbuckle || ent.Comp.Aircraft is not { } aircraft || !TryComp<DHAircraftComponent>(aircraft, out var comp))
            return;

        if (comp.State is DHAircraftState.Parked or DHAircraftState.Stowed)
            return;

        args.Cancelled = true;
        if (args.Popup && args.User is { } user)
            _popup.PopupEntity(Loc.GetString("dh-aircraft-cannot-unbuckle"), user, user);
    }

    private EntityUid? Pilot(DHAircraftComponent comp)
    {
        if (comp.Seat is not { } seat || !TryComp<StrapComponent>(seat, out var strap))
            return null;

        foreach (var buckled in strap.BuckledEntities)
        {
            return buckled;
        }

        return null;
    }

    private void Notify(Entity<DHAircraftComponent> ent, string message, bool warning = false)
    {
        if (Pilot(ent.Comp) is not { } pilot)
            return;

        _popup.PopupEntity(message, pilot, pilot, warning ? PopupType.LargeCaution : PopupType.Medium);
        if (warning)
            _audio.PlayEntity(ent.Comp.WarningSound, pilot, pilot);
    }

    #endregion

    #region Топливо, ремонт, ЛТЦ на палубе

    public float GetFuel(Entity<DHAircraftComponent> ent, out float max)
    {
        max = 0f;
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.FuelSolution, out _, out var solution))
            return 0f;

        max = solution.MaxVolume.Float();
        return solution.GetTotalPrototypeQuantity(ent.Comp.FuelReagent).Float();
    }

    private bool BurnFuel(Entity<DHAircraftComponent> ent, float amount)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.FuelSolution, out var soln, out var solution))
            return false;

        if (solution.GetTotalPrototypeQuantity(ent.Comp.FuelReagent) <= FixedPoint2.Zero)
            return false;

        _solutions.RemoveReagent(soln.Value, ent.Comp.FuelReagent, FixedPoint2.New(amount));
        return solution.GetTotalPrototypeQuantity(ent.Comp.FuelReagent) > FixedPoint2.Zero;
    }

    private void OnAircraftExamined(Entity<DHAircraftComponent> ent, ref ExaminedEvent args)
    {
        var fuel = GetFuel(ent, out var max);
        using (args.PushGroup(nameof(DHAircraftComponent)))
        {
            args.PushMarkup(Loc.GetString("dh-aircraft-examine",
                ("fuel", (int) fuel), ("max", (int) max),
                ("integrity", (int) ent.Comp.Integrity), ("maxIntegrity", (int) ent.Comp.MaxIntegrity),
                ("flares", ent.Comp.Flares), ("maxFlares", ent.Comp.MaxFlares)));
        }
    }

    private void OnAircraftInteractUsing(Entity<DHAircraftComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<DHAircraftFlareCassetteComponent>(args.Used, out var cassette))
        {
            args.Handled = true;
            if (ent.Comp.State != DHAircraftState.Parked)
                return;

            if (ent.Comp.Flares >= ent.Comp.MaxFlares)
            {
                _popup.PopupEntity(Loc.GetString("dh-aircraft-flares-full"), ent, args.User);
                return;
            }

            ent.Comp.Flares = Math.Min(ent.Comp.MaxFlares, ent.Comp.Flares + cassette.Flares);
            Dirty(ent);
            QueueDel(args.Used);
            _popup.PopupEntity(Loc.GetString("dh-aircraft-flares-loaded", ("flares", ent.Comp.Flares)), ent, args.User);
            return;
        }

        if (TryComp<DHAircraftCannonAmmoComponent>(args.Used, out var ammo))
        {
            args.Handled = true;
            if (ent.Comp.State != DHAircraftState.Parked)
                return;

            if (ent.Comp.CannonAmmo >= ent.Comp.MaxCannonAmmo)
            {
                _popup.PopupEntity(Loc.GetString("dh-aircraft-cannon-full"), ent, args.User);
                return;
            }

            ent.Comp.CannonAmmo = Math.Min(ent.Comp.MaxCannonAmmo, ent.Comp.CannonAmmo + ammo.Rounds);
            Dirty(ent);
            QueueDel(args.Used);
            _popup.PopupEntity(Loc.GetString("dh-aircraft-cannon-loaded", ("rounds", ent.Comp.CannonAmmo)), ent, args.User);
            return;
        }

        if (HasComp<DHAircraftMunitionComponent>(args.Used))
        {
            args.Handled = true;
            if (ent.Comp.State != DHAircraftState.Parked)
                return;

            if (!ent.Comp.Hardpoints.Contains(DHAircraftMunition.None))
            {
                _popup.PopupEntity(Loc.GetString("dh-aircraft-hardpoints-full"), ent, args.User);
                return;
            }

            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.LoadTime, new DHAircraftLoadDoAfterEvent(),
                ent.Owner, ent.Owner, args.Used)
            {
                BreakOnMove = true,
                NeedHand = true,
            });
            return;
        }

        if (!_tool.HasQuality(args.Used, WeldingQuality) || ent.Comp.State != DHAircraftState.Parked)
            return;

        args.Handled = true;
        if (ent.Comp.Integrity >= ent.Comp.MaxIntegrity)
        {
            _popup.PopupEntity(Loc.GetString("dh-aircraft-repair-full"), ent, args.User);
            return;
        }

        _tool.UseTool(args.Used, args.User, ent, 4f, WeldingQuality, new DHAircraftRepairDoAfterEvent(), fuel: 5);
    }

    private void OnRepairDoAfter(Entity<DHAircraftComponent> ent, ref DHAircraftRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Integrity = Math.Min(ent.Comp.MaxIntegrity, ent.Comp.Integrity + ent.Comp.RepairAmount);
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("dh-aircraft-repaired", ("integrity", (int) ent.Comp.Integrity)), ent, args.User);
    }

    private void OnLoadDoAfter(Entity<DHAircraftComponent> ent, ref DHAircraftLoadDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used ||
            !TryComp<DHAircraftMunitionComponent>(used, out var munition))
        {
            return;
        }

        var slot = ent.Comp.Hardpoints.IndexOf(DHAircraftMunition.None);
        if (slot < 0)
            return;

        args.Handled = true;
        ent.Comp.Hardpoints[slot] = munition.Kind;
        Dirty(ent);
        QueueDel(used);
        _popup.PopupEntity(Loc.GetString("dh-aircraft-munition-loaded", ("slot", slot + 1)), ent, args.User);
    }

    /// <summary>Заправка из бака с горючим поблизости (бак подкатывают к самолёту).</summary>
    private void OnAircraftVerbs(Entity<DHAircraftComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.State != DHAircraftState.Parked)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dh-aircraft-verb-refuel"),
            Act = () => Refuel(ent, user),
        });
    }

    private void Refuel(Entity<DHAircraftComponent> ent, EntityUid user)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.FuelSolution, out var target, out var targetSolution))
            return;

        var space = targetSolution.AvailableVolume;
        if (space <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("dh-aircraft-refuel-full"), ent, user);
            return;
        }

        foreach (var tank in _lookup.GetEntitiesInRange(Transform(ent).Coordinates, 3f))
        {
            if (tank == ent.Owner || !HasComp<ReagentTankComponent>(tank) ||
                !_solutions.TryGetSolution(tank, "tank", out var source, out var sourceSolution))
            {
                continue;
            }

            var available = sourceSolution.GetTotalPrototypeQuantity(ent.Comp.FuelReagent);
            if (available <= FixedPoint2.Zero)
                continue;

            var amount = FixedPoint2.Min(available, space);
            _solutions.RemoveReagent(source.Value, ent.Comp.FuelReagent, amount);
            _solutions.TryAddReagent(target.Value, ent.Comp.FuelReagent, amount, out _);
            _popup.PopupEntity(Loc.GetString("dh-aircraft-refueled", ("amount", (int) amount.Float())), ent, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString("dh-aircraft-refuel-no-tank"), ent, user);
    }

    #endregion

    #region Лифт

    private void OnLiftMapInit(Entity<DHAircraftLiftComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.StartingAircraft is not { } proto)
            return;

        var aircraft = Spawn(proto, Transform(ent).Coordinates);
        if (TryComp<DHAircraftComponent>(aircraft, out var comp))
            comp.Lift = ent;

        ent.Comp.Aircraft = aircraft;
        Stow((aircraft, comp!));
        SetLiftMode(ent, DHAircraftLiftMode.Lowered);
    }

    private void Stow(Entity<DHAircraftComponent?> aircraft)
    {
        _transform.DetachEntity(aircraft, Transform(aircraft));
        if (Resolve(aircraft, ref aircraft.Comp, false))
        {
            aircraft.Comp.State = DHAircraftState.Stowed;
            aircraft.Comp.Hooked = false;
            Dirty(aircraft.Owner, aircraft.Comp);
        }
    }

    private void SetLiftMode(Entity<DHAircraftLiftComponent> ent, DHAircraftLiftMode mode)
    {
        ent.Comp.Mode = mode;
        Dirty(ent);
        _appearance.SetData(ent, DHAircraftLiftVisuals.Mode, mode);
    }

    private void OnLiftExamined(Entity<DHAircraftLiftComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("dh-aircraft-lift-examine-" + ent.Comp.Mode.ToString().ToLowerInvariant()));
    }

    private void OnLiftVerbs(Entity<DHAircraftLiftComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        if (ent.Comp.Mode == DHAircraftLiftMode.Lowered)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dh-aircraft-lift-verb-raise"),
                Act = () => TryRaise(ent, user),
            });
        }
        else if (ent.Comp.Mode == DHAircraftLiftMode.Raised)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dh-aircraft-lift-verb-lower"),
                Act = () => TryLower(ent, user),
            });
        }
    }

    private void TryRaise(Entity<DHAircraftLiftComponent> ent, EntityUid user)
    {
        if (ent.Comp.Mode != DHAircraftLiftMode.Lowered)
            return;

        if (ent.Comp.Aircraft is not { } aircraft || !Exists(aircraft))
        {
            _popup.PopupEntity(Loc.GetString("dh-aircraft-lift-empty"), ent, user);
            return;
        }

        SetLiftMode(ent, DHAircraftLiftMode.Raising);
        ent.Comp.MoveEnd = _timing.CurTime + ent.Comp.MoveTime;
        _audio.PlayPvs(ent.Comp.MoveSound, ent);
    }

    private void TryLower(Entity<DHAircraftLiftComponent> ent, EntityUid user)
    {
        if (ent.Comp.Mode != DHAircraftLiftMode.Raised)
            return;

        if (ent.Comp.Aircraft is not { } aircraft || !TryComp<DHAircraftComponent>(aircraft, out var comp) ||
            comp.State != DHAircraftState.Parked || !OnLift((aircraft, comp), ent))
        {
            _popup.PopupEntity(Loc.GetString("dh-aircraft-lift-not-parked"), ent, user);
            return;
        }

        if (Pilot(comp) != null)
        {
            _popup.PopupEntity(Loc.GetString("dh-aircraft-lift-occupied"), ent, user);
            return;
        }

        SetLiftMode(ent, DHAircraftLiftMode.Lowering);
        ent.Comp.MoveEnd = _timing.CurTime + ent.Comp.MoveTime;
        _audio.PlayPvs(ent.Comp.MoveSound, ent);
    }

    private bool OnLift(Entity<DHAircraftComponent> aircraft, EntityUid lift)
    {
        var a = Transform(aircraft);
        var l = Transform(lift);
        return a.ParentUid == l.ParentUid && Vector2.Distance(a.LocalPosition, l.LocalPosition) < 1f;
    }

    private void UpdateLift(Entity<DHAircraftLiftComponent> ent, TimeSpan now)
    {
        if (now < ent.Comp.MoveEnd)
            return;

        switch (ent.Comp.Mode)
        {
            case DHAircraftLiftMode.Raising:
                SetLiftMode(ent, DHAircraftLiftMode.Raised);
                if (ent.Comp.Aircraft is { } aircraft && TryComp<DHAircraftComponent>(aircraft, out var comp))
                    Park((aircraft, comp), ent);
                break;
            case DHAircraftLiftMode.Lowering:
                SetLiftMode(ent, DHAircraftLiftMode.Lowered);
                if (ent.Comp.Aircraft is { } lowered)
                    Stow(lowered);
                break;
        }
    }

    /// <summary>Поставить самолёт на лифт носом по ВПП.</summary>
    private void Park(Entity<DHAircraftComponent> ent, EntityUid lift)
    {
        var liftXform = Transform(lift);
        _transform.SetCoordinates(ent, new EntityCoordinates(liftXform.ParentUid, liftXform.LocalPosition));
        _transform.SetLocalRotation(ent, liftXform.LocalRotation);
        _physics.SetCanCollide(ent, true);
        ent.Comp.State = DHAircraftState.Parked;
        ent.Comp.Hooked = false;
        ent.Comp.Control = false;
        Dirty(ent);
        if (Pilot(ent.Comp) is { } pilot)
            RemComp<RelayInputMoverComponent>(pilot);
    }

    #endregion

    #region Интерфейс полёта и способности

    private void OnFlightAction(Entity<DHAircraftPilotComponent> ent, ref DHAircraftFlightActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _ui.TryToggleUi(ent.Comp.Aircraft, DHAircraftUiKey.Flight, ent);
        if (TryComp<DHAircraftComponent>(ent.Comp.Aircraft, out var aircraft))
            UpdateUi((ent.Comp.Aircraft, aircraft));
    }

    private void OnControlAction(Entity<DHAircraftPilotComponent> ent, ref DHAircraftControlActionEvent args)
    {
        if (args.Handled || !TryComp<DHAircraftComponent>(ent.Comp.Aircraft, out var aircraft))
            return;

        args.Handled = true;
        SetControl((ent.Comp.Aircraft, aircraft), !aircraft.Control);
    }

    private void OnControlMessage(Entity<DHAircraftComponent> ent, ref DHAircraftControlMessage args)
    {
        if (IsPilot(ent, args.Actor))
            SetControl(ent, args.Control);
    }

    private void SetControl(Entity<DHAircraftComponent> ent, bool control)
    {
        ent.Comp.Control = control;
        Dirty(ent);
        if (Pilot(ent.Comp) is { } pilot)
        {
            if (control)
                _mover.SetRelay(pilot, ent);
            else
                RemComp<RelayInputMoverComponent>(pilot);

            _popup.PopupEntity(Loc.GetString(control ? "dh-aircraft-control-on" : "dh-aircraft-control-off"), pilot, pilot);
        }

        UpdateUi(ent);
    }

    private bool IsPilot(Entity<DHAircraftComponent> ent, EntityUid user)
    {
        return Pilot(ent.Comp) == user;
    }

    private void OnSpeedMessage(Entity<DHAircraftComponent> ent, ref DHAircraftSpeedMessage args)
    {
        if (!IsPilot(ent, args.Actor))
            return;

        ent.Comp.Speed = Math.Clamp(args.Speed, 0f, 1f);
        Dirty(ent);
        UpdateUi(ent);
    }

    private void OnHookMessage(Entity<DHAircraftComponent> ent, ref DHAircraftHookMessage args)
    {
        if (!IsPilot(ent, args.Actor) || ent.Comp.State != DHAircraftState.Parked)
            return;

        if (args.Hook && (ent.Comp.Lift is not { } lift || !TryComp<DHAircraftLiftComponent>(lift, out var liftComp) ||
                          liftComp.Mode != DHAircraftLiftMode.Raised || !OnLift(ent, lift)))
        {
            Notify(ent, Loc.GetString("dh-aircraft-blocker-lift"));
            return;
        }

        ent.Comp.Hooked = args.Hook;
        Dirty(ent);
        _audio.PlayPvs(ent.Comp.HookSound, ent);
        Notify(ent, Loc.GetString(args.Hook ? "dh-aircraft-hooked" : "dh-aircraft-unhooked"));
        UpdateUi(ent);
    }

    /// <summary>Почему катапульта не выпустит самолёт (ключ локализации) или null.</summary>
    private string? LaunchBlocker(Entity<DHAircraftComponent> ent)
    {
        if (ent.Comp.State != DHAircraftState.Parked)
            return "dh-aircraft-blocker-state";
        if (!ent.Comp.Hooked)
            return "dh-aircraft-blocker-hook";
        if (GetFuel(ent, out _) < ent.Comp.TakeoffFuel)
            return "dh-aircraft-blocker-fuel";
        if (ent.Comp.Integrity < MinLaunchIntegrity)
            return "dh-aircraft-blocker-damage";
        if (!HasComp<DHPlanetAtmosphereMapComponent>(Transform(ent).MapUid))
            return "dh-aircraft-blocker-orbit";
        return null;
    }

    private void OnLaunchMessage(Entity<DHAircraftComponent> ent, ref DHAircraftLaunchMessage args)
    {
        if (!IsPilot(ent, args.Actor))
            return;

        if (LaunchBlocker(ent) is { } blocker)
        {
            Notify(ent, Loc.GetString(blocker));
            return;
        }

        var xform = Transform(ent);
        var direction = xform.LocalRotation.RotateVec(Vector2.UnitY);
        var length = TryComp<DHAircraftLiftComponent>(ent.Comp.Lift, out var lift) ? lift.RunwayLength : 50f;
        ent.Comp.PathStart = xform.LocalPosition;
        ent.Comp.PathEnd = xform.LocalPosition + direction * length;
        ent.Comp.Progress = 0f;
        ent.Comp.Hooked = false;
        ent.Comp.State = DHAircraftState.Launching;
        Dirty(ent);
        _audio.PlayPvs(ent.Comp.LaunchSound, ent);
        UpdateUi(ent);
    }

    private void OnReturnMessage(Entity<DHAircraftComponent> ent, ref DHAircraftReturnMessage args)
    {
        if (!IsPilot(ent, args.Actor))
            return;

        if (ent.Comp.State == DHAircraftState.Airborne)
        {
            if (ApproachPoint(ent) == null)
            {
                Notify(ent, Loc.GetString("dh-aircraft-no-carrier"));
                return;
            }

            ent.Comp.State = DHAircraftState.Returning;
            Notify(ent, Loc.GetString("dh-aircraft-returning"));
        }
        else if (ent.Comp.State == DHAircraftState.Returning)
        {
            ent.Comp.State = DHAircraftState.Airborne;
            Notify(ent, Loc.GetString("dh-aircraft-return-cancelled"));
        }

        Dirty(ent);
        UpdateUi(ent);
    }

    private void OnFlaresAction(Entity<DHAircraftPilotComponent> ent, ref DHAircraftFlaresActionEvent args)
    {
        if (args.Handled || !TryComp<DHAircraftComponent>(ent.Comp.Aircraft, out var aircraft))
            return;

        args.Handled = true;
        var plane = (ent.Comp.Aircraft, aircraft);
        if (aircraft.State is not (DHAircraftState.Airborne or DHAircraftState.Returning or DHAircraftState.Pass))
        {
            Notify(plane, Loc.GetString("dh-aircraft-flares-ground"));
            return;
        }

        if (aircraft.Flares <= 0)
        {
            Notify(plane, Loc.GetString("dh-aircraft-flares-empty"), true);
            return;
        }

        var now = _timing.CurTime;
        aircraft.Flares--;
        aircraft.FlaresUntil = now + aircraft.FlareDuration;
        Dirty(ent.Comp.Aircraft, aircraft);
        _audio.PlayPvs(aircraft.FlareSound, ent.Comp.Aircraft);
        Notify(plane, Loc.GetString("dh-aircraft-flares-fired", ("left", aircraft.Flares)));
        UpdateUi(plane);
    }

    private void OnEjectAction(Entity<DHAircraftPilotComponent> ent, ref DHAircraftEjectActionEvent args)
    {
        if (args.Handled || !TryComp<DHAircraftComponent>(ent.Comp.Aircraft, out var aircraft))
            return;

        args.Handled = true;
        if (aircraft.State is DHAircraftState.Airborne or DHAircraftState.Returning or DHAircraftState.Pass)
            Crash((ent.Comp.Aircraft, aircraft), ejected: true);
        else if (aircraft.State == DHAircraftState.Parked)
            _buckle.TryUnbuckle(ent.Owner, ent.Owner, popup: false);
    }

    private void UpdateUi(Entity<DHAircraftComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, DHAircraftUiKey.Flight))
            return;

        var fuel = GetFuel(ent, out var max);
        var blocker = LaunchBlocker(ent);
        var airborne = ent.Comp.State is DHAircraftState.Airborne or DHAircraftState.Returning or DHAircraftState.Pass;
        var passBlocker = PassBlocker(ent);
        var xform = Transform(ent);
        var state = new DHAircraftBuiState
        {
            State = ent.Comp.State,
            Fuel = fuel,
            FuelMax = max,
            TakeoffFuel = ent.Comp.TakeoffFuel,
            Speed = ent.Comp.Speed,
            Integrity = ent.Comp.Integrity,
            MaxIntegrity = ent.Comp.MaxIntegrity,
            Flares = ent.Comp.Flares,
            MaxFlares = ent.Comp.MaxFlares,
            FlaresActive = _timing.CurTime < ent.Comp.FlaresUntil,
            Hooked = ent.Comp.Hooked,
            Control = ent.Comp.Control,
            CanLaunch = blocker == null,
            LaunchBlocker = blocker,
            Position = airborne ? _transform.GetWorldPosition(xform) : null,
            HeadingDegrees = (float) ent.Comp.Heading.Degrees,
            Map = MapState(),
            PassStart = ent.Comp.PassStart,
            PassEnd = ent.Comp.PassEnd,
            CanPass = passBlocker == null,
            PassBlocker = passBlocker,
            Altitude = ent.Comp.Altitude,
            CannonAmmo = ent.Comp.CannonAmmo,
            MaxCannonAmmo = ent.Comp.MaxCannonAmmo,
            CannonOn = ent.Comp.CannonOn,
            Hardpoints = new List<DHAircraftMunition>(ent.Comp.Hardpoints),
        };

        _ui.SetUiState(ent.Owner, DHAircraftUiKey.Flight, state);
    }

    private void OnTerrainRequest(Entity<DHAircraftComponent> ent, ref DHTacticalMapRequestMessage args)
    {
        if (_tacmap.GetSurfaceMap() is { } surface && TryComp<ActorComponent>(args.Actor, out var actor))
            _tacmap.SendTerrain(surface, actor.PlayerSession);
    }

    private DHOverwatchState? MapState()
    {
        if (_tacmap.GetSurfaceMap() is not { } surface)
            return null;

        return new DHOverwatchState(DHAssaultFaction.Empire, new List<DHOverwatchEntry>(), new Dictionary<NetEntity, NetEntity>(),
            GetNetEntity(surface), _tacmap.TerrainVersion(surface), _tacmap.Markers(DHAssaultFaction.Empire, surface));
    }

    #endregion

    #region Полёт

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var lifts = EntityQueryEnumerator<DHAircraftLiftComponent>();
        while (lifts.MoveNext(out var uid, out var lift))
        {
            if (lift.Mode is DHAircraftLiftMode.Raising or DHAircraftLiftMode.Lowering)
                UpdateLift((uid, lift), now);
        }

        UpdateBombs(now);

        var refreshUi = now >= _nextUi;
        if (refreshUi)
            _nextUi = now + UiInterval;

        var query = EntityQueryEnumerator<DHAircraftComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);
            switch (comp.State)
            {
                case DHAircraftState.Launching:
                    UpdateLaunch(ent, frameTime);
                    break;
                case DHAircraftState.Airborne:
                case DHAircraftState.Returning:
                    UpdateFlight(ent, frameTime, now);
                    break;
                case DHAircraftState.Landing:
                    UpdateLanding(ent, frameTime);
                    break;
                case DHAircraftState.Pass:
                    UpdatePass(ent, frameTime, now);
                    break;
            }

            if (refreshUi && !TerminatingOrDeleted(uid))
                UpdateUi(ent);
        }
    }

    private void UpdateLaunch(Entity<DHAircraftComponent> ent, float frameTime)
    {
        ent.Comp.Progress = Math.Min(1f, ent.Comp.Progress + frameTime / ent.Comp.LaunchTime);
        var t = ent.Comp.Progress * ent.Comp.Progress; // разгон
        var xform = Transform(ent);
        _transform.SetLocalPosition(ent, Vector2.Lerp(ent.Comp.PathStart, ent.Comp.PathEnd, t), xform);
        RunOver(ent);

        if (ent.Comp.Progress < 1f)
            return;

        // Конец ВПП: самолёт уходит с палубы в слой атмосферы над полем боя.
        if (xform.MapUid is not { } map)
            return;

        var (position, rotation) = _transform.GetWorldPositionRotation(xform);
        _transform.SetCoordinates(ent, new EntityCoordinates(map, position));
        _transform.SetWorldRotation(ent, rotation);
        _physics.SetCanCollide(ent, false);
        ent.Comp.Heading = rotation;
        ent.Comp.State = DHAircraftState.Airborne;
        Dirty(ent);
        SetControl(ent, true);
        Notify(ent, Loc.GetString("dh-aircraft-airborne"));
    }

    /// <summary>Кто стоит на ВПП перед разгоняющимся самолётом — сбит с ног и ранен.</summary>
    private void RunOver(Entity<DHAircraftComponent> ent)
    {
        var pilot = Pilot(ent.Comp);
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(ent).Coordinates, 1.6f))
        {
            if (mob.Owner == pilot || Transform(mob).ParentUid == ent.Comp.Seat)
                continue;

            var damage = new DamageSpecifier(_proto.Index(BluntType), FixedPoint2.New(8));
            _damage.TryChangeDamage(mob.Owner, damage);
        }
    }

    private void UpdateFlight(Entity<DHAircraftComponent> ent, float frameTime, TimeSpan now)
    {
        var comp = ent.Comp;
        var xform = Transform(ent);

        // Топливо: на максимальной скорости вдвое больше, чем на минимальной.
        if (!BurnFuel(ent, comp.FuelPerSecond * (1f + comp.Speed) * frameTime))
        {
            Notify(ent, Loc.GetString("dh-aircraft-no-fuel"), true);
            Crash(ent, ejected: false);
            return;
        }

        var fuel = GetFuel(ent, out var max);
        if (max > 0f && fuel / max < 0.2f && now >= comp.NextWarning)
        {
            comp.NextWarning = now + TimeSpan.FromSeconds(15);
            Notify(ent, Loc.GetString("dh-aircraft-low-fuel"), true);
        }

        var position = _transform.GetWorldPosition(xform);
        Vector2? desired = null;
        float speed;

        if (comp.State == DHAircraftState.Returning)
        {
            if (ApproachPoint(ent) is not { } approach)
            {
                comp.State = DHAircraftState.Airborne;
                Dirty(ent);
                Notify(ent, Loc.GetString("dh-aircraft-no-carrier"), true);
                return;
            }

            var toApproach = approach.Point - position;
            if (toApproach.Length() < 2f)
            {
                BeginLanding(ent, approach.Lift);
                return;
            }

            desired = toApproach;
            speed = comp.MaxSpeed;
        }
        else
        {
            if (comp.Control && TryComp<InputMoverComponent>(ent, out var mover))
            {
                var buttons = mover.HeldMoveButtons;
                var input = Vector2.Zero;
                if ((buttons & MoveButtons.Up) != 0)
                    input.Y += 1f;
                if ((buttons & MoveButtons.Down) != 0)
                    input.Y -= 1f;
                if ((buttons & MoveButtons.Right) != 0)
                    input.X += 1f;
                if ((buttons & MoveButtons.Left) != 0)
                    input.X -= 1f;

                if (input != Vector2.Zero)
                    desired = input;
            }

            speed = comp.MinSpeed + (comp.MaxSpeed - comp.MinSpeed) * comp.Speed;
        }

        if (desired is { } want)
        {
            // Нос = Heading.RotateVec((0, 1)) = (-sin, cos).
            var target = new Angle(MathF.Atan2(-want.X, want.Y));
            var diff = Angle.ShortestDistance(comp.Heading, target).Theta;
            var maxTurn = MathHelper.DegreesToRadians(comp.TurnRate) * frameTime;
            comp.Heading = new Angle(comp.Heading.Theta + Math.Clamp(diff, -maxTurn, maxTurn));
        }

        var nose = comp.Heading.RotateVec(Vector2.UnitY);
        _transform.SetWorldPosition(ent, position + nose * speed * frameTime);
        _transform.SetWorldRotation(ent, comp.Heading);
    }

    /// <summary>Точка захода на посадку: торец ВПП своего лифта (в мире) — если авианосец в том же небе.</summary>
    private (Vector2 Point, EntityUid Lift)? ApproachPoint(Entity<DHAircraftComponent> ent)
    {
        if (ent.Comp.Lift is not { } lift || !TryComp<DHAircraftLiftComponent>(lift, out var liftComp))
            return null;

        var liftXform = Transform(lift);
        if (liftXform.MapUid != Transform(ent).MapUid)
            return null;

        var (position, rotation) = _transform.GetWorldPositionRotation(liftXform);
        return (position + rotation.RotateVec(Vector2.UnitY) * liftComp.RunwayLength, lift);
    }

    /// <summary>Касание у торца ВПП: самолёт снова на палубе и катится к лифту.</summary>
    private void BeginLanding(Entity<DHAircraftComponent> ent, EntityUid lift)
    {
        var liftXform = Transform(lift);
        if (!TryComp<DHAircraftLiftComponent>(lift, out var liftComp))
            return;

        var direction = liftXform.LocalRotation.RotateVec(Vector2.UnitY);
        ent.Comp.PathStart = liftXform.LocalPosition + direction * liftComp.RunwayLength;
        ent.Comp.PathEnd = liftXform.LocalPosition;
        ent.Comp.Progress = 0f;
        _transform.SetCoordinates(ent, new EntityCoordinates(liftXform.ParentUid, ent.Comp.PathStart));
        _transform.SetLocalRotation(ent, liftXform.LocalRotation + Angle.FromDegrees(180));
        ent.Comp.State = DHAircraftState.Landing;
        Dirty(ent);
        SetControl(ent, false);
        Notify(ent, Loc.GetString("dh-aircraft-touchdown"));
    }

    private void UpdateLanding(Entity<DHAircraftComponent> ent, float frameTime)
    {
        ent.Comp.Progress = Math.Min(1f, ent.Comp.Progress + frameTime / ent.Comp.LandingRollTime);
        var t = 1f - (1f - ent.Comp.Progress) * (1f - ent.Comp.Progress); // торможение
        _transform.SetLocalPosition(ent, Vector2.Lerp(ent.Comp.PathStart, ent.Comp.PathEnd, t));
        if (ent.Comp.Progress < 1f)
            return;

        if (ent.Comp.Lift is { } lift)
            Park(ent, lift);
        Notify(ent, Loc.GetString("dh-aircraft-parked"));
    }

    #endregion

    #region ПВО, крушение

    /// <summary>Самолёт над точкой <paramref name="position"/> в небе <paramref name="sky"/> в пределах дальности и без ЛТЦ.</summary>
    public EntityUid? FindAirTarget(EntityUid sky, Vector2 position, float range)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHAircraftComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.State is not (DHAircraftState.Airborne or DHAircraftState.Returning) || xform.MapUid != sky ||
                now < comp.FlaresUntil || Vector2.Distance(_transform.GetWorldPosition(xform), position) > range)
            {
                continue;
            }

            return uid;
        }

        return null;
    }

    /// <summary>Предупреждение пилоту: его захватила ПВО (выстрел ещё может промахнуться).</summary>
    public void WarnLocked(EntityUid aircraft)
    {
        if (TryComp<DHAircraftComponent>(aircraft, out var comp))
            Notify((aircraft, comp), Loc.GetString("dh-aircraft-aa-lock"), true);
    }

    public void Hit(EntityUid aircraft, float damage)
    {
        if (!TryComp<DHAircraftComponent>(aircraft, out var comp))
            return;

        comp.Integrity -= damage;
        Dirty(aircraft, comp);
        var ent = (aircraft, comp);
        if (comp.Integrity <= 0f)
        {
            Crash(ent, ejected: false);
            return;
        }

        Notify(ent, Loc.GetString("dh-aircraft-hit", ("integrity", (int) comp.Integrity)), true);
        if (Pilot(comp) is { } pilot)
            _audio.PlayEntity(comp.HitSound, pilot, pilot);
    }

    /// <summary>Самолёт потерян: пилот катапультируется (или падает с ним) на поверхность под точкой полёта.</summary>
    private void Crash(Entity<DHAircraftComponent> ent, bool ejected)
    {
        var xform = Transform(ent);
        var position = _transform.GetWorldPosition(xform);
        var pilot = Pilot(ent.Comp);
        var surface = HasComp<DHPlanetSurfaceMapComponent>(xform.MapUid) ? xform.MapUid : SurfaceBelow(xform.MapUid);

        if (pilot is { } p)
        {
            _forceUnbuckle = true;
            _buckle.TryUnbuckle(p, p, popup: false);
            _forceUnbuckle = false;
            RemComp<RelayInputMoverComponent>(p);
            if (surface is { } map)
            {
                var landing = position + new Angle(_random.NextFloat() * MathF.Tau).RotateVec(new Vector2(3f + _random.NextFloat() * 5f, 0f));
                _transform.SetCoordinates(p, new EntityCoordinates(map, landing));
                _transform.AttachToGridOrMap(p);
            }

            var damage = new DamageSpecifier(_proto.Index(BluntType), FixedPoint2.New(ejected ? 15 : 45));
            _damage.TryChangeDamage(p, damage);
            _popup.PopupEntity(Loc.GetString(ejected ? "dh-aircraft-ejected" : "dh-aircraft-crashed"), p, p, PopupType.LargeCaution);
            if (ejected)
                _audio.PlayEntity(ent.Comp.EjectSound, p, p);
        }

        if (surface is { } surfaceMap && TryComp<MapComponent>(surfaceMap, out var mapComp))
            _explosion.QueueExplosion(new MapCoordinates(position, mapComp.MapId), "Default", 160f, 5f, 25f, null);

        if (ent.Comp.Lift is { } lift && TryComp<DHAircraftLiftComponent>(lift, out var liftComp))
            liftComp.Aircraft = null;

        QueueDel(ent);
    }

    /// <summary>Карта поверхности под слоем атмосферы (уровень земли).</summary>
    private EntityUid? SurfaceBelow(EntityUid? sky)
    {
        if (!TryComp<DHPlanetAtmosphereMapComponent>(sky, out var atmosphere))
            return null;

        var query = EntityQueryEnumerator<DHPlanetSurfaceMapComponent>();
        while (query.MoveNext(out var uid, out var surface))
        {
            if (surface.System == atmosphere.System && surface.Body == atmosphere.Body)
                return uid;
        }

        return null;
    }

    #endregion
}
