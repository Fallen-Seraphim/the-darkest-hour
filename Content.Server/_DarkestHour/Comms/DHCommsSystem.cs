using Content.Server.Telephone;
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
public sealed class DHCommsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TelephoneSystem _telephone = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

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

        var source = Transform(args.RadioSource).MapID;
        var receiver = Transform(args.RadioReceiver).MapID;
        if (source == receiver)
            return;

        // Канал под ретранслятором (хоть один его знает) идёт между картами только при живом ретрансляторе.
        var governed = false;
        var relays = EntityQueryEnumerator<DHCommsRelayComponent>();
        while (relays.MoveNext(out _, out var relay))
        {
            if (!relay.Channels.Contains(args.Channel.ID))
                continue;

            if (relay.Active)
                return;

            governed = true;
        }

        if (governed)
            args.Cancelled = true;
    }

    private void OnRelayMapInit(Entity<DHCommsRelayComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent, DHCommsRelayVisuals.Active, ent.Comp.Active);
    }

    private void OnRelayDamaged(Entity<DHCommsRelayComponent> ent, ref DamageChangedEvent args)
    {
        var broken = args.Damageable.TotalDamage.Float() >= RelayBreakDamage;
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
        if (TryComp<DHFieldPhoneComponent>(uid, out var phone) && !string.IsNullOrEmpty(phone.PhoneName))
            return phone.PhoneName;

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
    }

    #endregion
}
