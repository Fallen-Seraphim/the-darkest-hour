using Content.Server._DarkestHour.Assault;
using Content.Server._DarkestHour.Shuttles.Rift;
using Content.Server.Telephone;
using Content.Shared._DarkestHour.HeavyGuns;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Verbs;
using Robust.Shared.Map.Components;
using Content.Shared._DarkestHour.Comms;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Telephone;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Comms;

/// <summary>
/// Ретрансляторы (каналы стороны между картами только через живой ретранслятор) и полевые телефоны
/// (справочник своей сети, звонок, ответ, отбой поверх ванильной телефонии).
/// </summary>
public sealed partial class DHCommsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private DHLandingSystem _landing = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TelephoneSystem _telephone = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private Content.Shared._DarkestHour.Levels.DHZLevelSystem _zLevels = default!;

    /// <summary>Глубже этого уровня связь с поверхностью не проходит: нужен свой ретранслятор на том уровне.</summary>
    private const int DeepestLinkedLevel = -1;

    private readonly HashSet<EntityUid> _covered = new();

    /// <summary>Урон, после которого ретранслятор замолкает (сваркой чинится обратно).</summary>
    private const float RelayBreakDamage = 150f;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
        SubscribeLocalEvent<DHCommsRelayComponent, MapInitEvent>(OnRelayMapInit);
        SubscribeLocalEvent<DHCommsRelayComponent, DamageChangedEvent>(OnRelayDamaged);
        SubscribeLocalEvent<DHCommsRelayComponent, ExaminedEvent>(OnRelayExamined);
        SubscribeLocalEvent<DHPortableRelayFoldedComponent, UseInHandEvent>(OnPortableUse);
        SubscribeLocalEvent<DHPortableRelayFoldedComponent, DHPortableRelayDeployDoAfterEvent>(OnPortableDeployed);
        SubscribeLocalEvent<DHPortableRelayComponent, GetVerbsEvent<AlternativeVerb>>(OnPortableVerbs);
        SubscribeLocalEvent<DHPortableRelayComponent, DHPortableRelayFoldDoAfterEvent>(OnPortableFolded);

        Subs.BuiEvents<DHFieldPhoneComponent>(DHFieldPhoneUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnPhoneOpened);
            subs.Event<DHFieldPhoneCallMessage>(OnPhoneCall);
            subs.Event<DHFieldPhoneAnswerMessage>(OnPhoneAnswer);
            subs.Event<DHFieldPhoneHangUpMessage>(OnPhoneHangUp);
        });
    }

    #region Ретрансляторы

    private void OnRadioReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        // Канал под ретранслятором (хоть один ретранслятор его знает) работает только в зонах живых ретрансляторов:
        // и отправитель, и получатель должны быть в покрытой зоне.
        var governed = false;
        _covered.Clear();
        var relays = EntityQueryEnumerator<DHCommsRelayComponent>();
        while (relays.MoveNext(out var relayUid, out var relay))
        {
            if (!relay.Channels.Contains(args.Channel.ID))
                continue;

            governed = true;
            if (relay.Active && RelayWorking(relayUid) && Zone(relayUid) is { } zone)
                _covered.Add(zone);
        }

        if (!governed)
            return;

        if (Zone(args.RadioSource) is not { } source || !_covered.Contains(source) ||
            Zone(args.RadioReceiver) is not { } receiver || !_covered.Contains(receiver))
        {
            args.Cancelled = true;
        }
    }

    /// <summary>Переносной ретранслятор работает, только развёрнутый и закреплённый.</summary>
    private bool RelayWorking(EntityUid relay)
    {
        // Взламываемая антенна («Восстание») работает только взломанной.
        if (TryComp<DHHackableRelayComponent>(relay, out var hackable) && !hackable.Hacked)
            return false;

        return !HasComp<DHPortableRelayComponent>(relay) || Transform(relay).Anchored;
    }

    /// <summary>
    /// Зона связи: карта; отсек машины — там, где машина; бункер, этажи и небо планеты — её поверхность; уровни
    /// −2 и −3 под землёй — своя зона.
    /// </summary>
    private EntityUid? Zone(EntityUid uid)
    {
        var xform = Transform(uid);
        for (var i = 0; i < 3; i++)
        {
            if (xform.GridUid is not { } grid || !TryComp<DHVehicleInteriorComponent>(grid, out var interior) ||
                !Exists(interior.Vehicle))
            {
                break;
            }

            xform = Transform(interior.Vehicle);
        }

        if (xform.MapUid is not { } map)
            return null;

        if (TryComp<DHPlanetAtmosphereMapComponent>(map, out var atmosphere))
            return _landing.GetSurface(atmosphere.System, atmosphere.Body) ?? map;

        // Z-уровни: этажи и первый подземный — зона земли; глубже под землёй своя зона (нужен свой ретранслятор).
        if (_zLevels.TryGetLevel(map, out var level))
        {
            if (level.Level < DeepestLinkedLevel)
                return map;

            return _zLevels.LevelMap(level.Stack, 0) ?? map;
        }

        if (HasComp<DHUndergroundMapComponent>(map))
        {
            var bases = EntityQueryEnumerator<DHPlanetBaseComponent>();
            while (bases.MoveNext(out var planetBase))
            {
                if (planetBase.UndergroundGrid is { } underground && Transform(underground).MapUid == map &&
                    _landing.GetSurface(planetBase.System, planetBase.Body) is { } surface)
                {
                    return surface;
                }
            }
        }

        return map;
    }

    #region Переносной ретранслятор

    private void OnPortableUse(Entity<DHPortableRelayFoldedComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.DeployTime,
            new DHPortableRelayDeployDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnPortableDeployed(Entity<DHPortableRelayFoldedComponent> ent, ref DHPortableRelayDeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        var userXform = Transform(args.User);
        if (userXform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
        {
            _popup.PopupEntity(Loc.GetString("dh-relay-portable-no-ground"), args.User, args.User);
            return;
        }

        args.Handled = true;
        var coordinates = _map.GridTileToLocal(grid, gridComp, _map.TileIndicesFor(grid, gridComp, userXform.Coordinates));
        var relay = Spawn(ent.Comp.Deployed, coordinates);
        _transform.AnchorEntity(relay, Transform(relay));
        _popup.PopupEntity(Loc.GetString("dh-relay-portable-deployed", ("user", args.User)), relay, PopupType.Medium);
        QueueDel(ent);
    }

    private void OnPortableVerbs(Entity<DHPortableRelayComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dh-relay-portable-fold-verb"),
            Act = () => _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.FoldTime,
                new DHPortableRelayFoldDoAfterEvent(), ent, target: ent)
            {
                BreakOnMove = true,
                NeedHand = true,
            }),
        });
    }

    private void OnPortableFolded(Entity<DHPortableRelayComponent> ent, ref DHPortableRelayFoldDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var folded = Spawn(ent.Comp.Folded, _transform.GetMapCoordinates(ent));
        _hands.PickupOrDrop(args.User, folded);
        QueueDel(ent);
    }

    #endregion

    private void OnRelayMapInit(Entity<DHCommsRelayComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent, DHCommsRelayVisuals.Active, ent.Comp.Active);
    }

    private void OnRelayDamaged(Entity<DHCommsRelayComponent> ent, ref DamageChangedEvent args)
    {
        var broken = _damageable.GetTotalDamage((ent.Owner, args.Damageable)).Float() >= RelayBreakDamage;
        if (broken == !ent.Comp.Active)
            return;

        ent.Comp.Active = !broken;
        _appearance.SetData(ent, DHCommsRelayVisuals.Active, ent.Comp.Active);
        _popup.PopupEntity(Loc.GetString(broken ? "dh-relay-broken" : "dh-relay-repaired"), ent,
            broken ? PopupType.LargeCaution : PopupType.Medium);
    }

    private void OnRelayExamined(Entity<DHCommsRelayComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Active ? "dh-relay-examine-active" : "dh-relay-examine-broken"));
    }

    #endregion

    #region Полевые телефоны

    private string PhoneName(EntityUid uid)
    {
        if (TryComp<DHFieldPhoneComponent>(uid, out var phone))
        {
            if (!string.IsNullOrEmpty(phone.PhoneName))
                return phone.PhoneName;

            if (phone.UseEntityName)
                return Name(uid);
        }

        // Без имени — по кораблю или постройке, где стоит (у челноков — их позывной).
        return Transform(uid).GridUid is { } grid ? Name(grid) : Name(uid);
    }

    private void OnPhoneOpened(Entity<DHFieldPhoneComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdatePhoneUi(ent);
    }

    private void OnPhoneCall(Entity<DHFieldPhoneComponent> ent, ref DHFieldPhoneCallMessage args)
    {
        if (!TryComp<TelephoneComponent>(ent, out var source) ||
            !TryGetEntity(args.Phone, out var target) || target == ent.Owner ||
            !TryComp<DHFieldPhoneComponent>(target, out var targetPhone) || targetPhone.Network != ent.Comp.Network ||
            !TryComp<TelephoneComponent>(target, out var receiver))
        {
            return;
        }

        if (source.CurrentState != TelephoneState.Idle)
        {
            _popup.PopupEntity(Loc.GetString("dh-phone-busy-self"), ent, args.Actor);
            return;
        }

        if (receiver.CurrentState != TelephoneState.Idle)
        {
            _popup.PopupEntity(Loc.GetString("dh-phone-busy", ("phone", PhoneName(target.Value))), ent, args.Actor);
            return;
        }

        _telephone.CallTelephone((ent, source), (target.Value, receiver), args.Actor);
        UpdatePhoneUi(ent);
    }

    private void OnPhoneAnswer(Entity<DHFieldPhoneComponent> ent, ref DHFieldPhoneAnswerMessage args)
    {
        if (!TryComp<TelephoneComponent>(ent, out var phone) || phone.CurrentState != TelephoneState.Ringing)
            return;

        _telephone.AnswerTelephone((ent, phone), args.Actor);
        UpdatePhoneUi(ent);
    }

    private void OnPhoneHangUp(Entity<DHFieldPhoneComponent> ent, ref DHFieldPhoneHangUpMessage args)
    {
        if (!TryComp<TelephoneComponent>(ent, out var phone))
            return;

        _telephone.EndTelephoneCalls((ent, phone));
        UpdatePhoneUi(ent);
    }

    private void UpdatePhoneUi(Entity<DHFieldPhoneComponent> ent)
    {
        if (!TryComp<TelephoneComponent>(ent, out var phone))
            return;

        var status = phone.CurrentState switch
        {
            TelephoneState.Calling => DHFieldPhoneStatus.Calling,
            TelephoneState.Ringing => DHFieldPhoneStatus.Ringing,
            TelephoneState.InCall => DHFieldPhoneStatus.InCall,
            _ => DHFieldPhoneStatus.Idle,
        };

        string? other = null;
        foreach (var linked in phone.LinkedTelephones)
        {
            other = PhoneName(linked);
            break;
        }

        var book = new List<DHFieldPhoneEntry>();
        var query = EntityQueryEnumerator<DHFieldPhoneComponent, TelephoneComponent>();
        while (query.MoveNext(out var uid, out var field, out var telephone))
        {
            if (uid == ent.Owner || field.Network != ent.Comp.Network)
                continue;

            book.Add(new DHFieldPhoneEntry(GetNetEntity(uid), PhoneName(uid),
                telephone.CurrentState != TelephoneState.Idle));
        }

        book.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        _ui.SetUiState(ent.Owner, DHFieldPhoneUiKey.Key, new DHFieldPhoneState(PhoneName(ent), status, other, book));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;
        var query = EntityQueryEnumerator<DHFieldPhoneComponent>();
        while (query.MoveNext(out var uid, out var phone))
        {
            if (_ui.IsUiOpen(uid, DHFieldPhoneUiKey.Key))
                UpdatePhoneUi((uid, phone));
        }

        UpdateSignalAlerts();
    }

    #endregion
}
