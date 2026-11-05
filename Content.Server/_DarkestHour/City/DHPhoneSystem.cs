using Content.Server.Telephone;
using Content.Shared._DarkestHour.City;
using Content.Shared.CartridgeLoader;
using Content.Shared.Popups;
using Content.Shared.Telephone;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Content.Server.GameTicking;

namespace Content.Server._DarkestHour.City;

/// <summary>
/// Телефоны и терминалы Лост-Энджелса: номера, справочник, приложения «Сообщения», «Банк LA-V» и «Телефон» (звонки
/// через ванильный <see cref="TelephoneSystem"/>, голос — как у голопадов).
/// </summary>
public sealed partial class DHPhoneSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DHBankSystem _bank = default!;
    [Dependency] private CartridgeLoaderSystem _cartridges = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TelephoneSystem _telephone = default!;
    [Dependency] private ServerGameTicker _ticker = default!;

    private static readonly SoundSpecifier SmsSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHPhoneComponent, MapInitEvent>(OnPhoneMapInit);
        SubscribeLocalEvent<DHPhoneComponent, BoundUIOpenedEvent>(OnPhoneOpened);
        SubscribeLocalEvent<DHPhoneComponent, TelephoneStateChangeEvent>(OnTelephoneState);

        SubscribeLocalEvent<DHMessengerCartridgeComponent, CartridgeUiReadyEvent>(OnMessengerReady);
        SubscribeLocalEvent<DHMessengerCartridgeComponent, CartridgeMessageEvent>(OnMessengerMessage);
        SubscribeLocalEvent<DHBankAppCartridgeComponent, CartridgeUiReadyEvent>(OnBankReady);
        SubscribeLocalEvent<DHBankAppCartridgeComponent, CartridgeMessageEvent>(OnBankMessage);
        SubscribeLocalEvent<DHCallCartridgeComponent, CartridgeUiReadyEvent>(OnCallReady);
        SubscribeLocalEvent<DHCallCartridgeComponent, CartridgeMessageEvent>(OnCallMessage);
    }

    #region Телефон

    private void OnPhoneMapInit(Entity<DHPhoneComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Number.Length > 0)
            return;

        string number;
        do
        {
            number = $"555-{_random.Next(1000, 10000)}";
        } while (FindPhone(number) != null);

        ent.Comp.Number = number;
        if (ent.Comp.Stationary && ent.Comp.OwnerName.Length == 0)
            ent.Comp.OwnerName = Name(ent);

        Dirty(ent);
    }

    public void SetOwner(Entity<DHPhoneComponent> ent, string name)
    {
        ent.Comp.OwnerName = name;
        Dirty(ent);
    }

    private void OnPhoneOpened(Entity<DHPhoneComponent> ent, ref BoundUIOpenedEvent args)
    {
        _cartridges.UpdateUiState(ent.Owner);
    }

    public EntityUid? FindPhone(string number)
    {
        var query = EntityQueryEnumerator<DHPhoneComponent>();
        while (query.MoveNext(out var uid, out var phone))
        {
            if (phone.Number == number)
                return uid;
        }

        return null;
    }

    private string PhoneName(EntityUid uid, DHPhoneComponent phone)
    {
        return phone.OwnerName.Length > 0 ? phone.OwnerName : Name(uid);
    }

    /// <summary>Справочник: все телефоны и терминалы, кроме этого.</summary>
    private List<DHPhoneContact> Contacts(EntityUid self, Func<string, int>? unread = null, bool callableOnly = false)
    {
        var contacts = new List<DHPhoneContact>();
        var query = EntityQueryEnumerator<DHPhoneComponent>();
        while (query.MoveNext(out var uid, out var phone))
        {
            if (uid == self || phone.Number.Length == 0 || callableOnly && phone.Stationary)
                continue;

            var busy = TryComp<TelephoneComponent>(uid, out var telephone) && telephone.CurrentState != TelephoneState.Idle;
            contacts.Add(new DHPhoneContact(phone.Number, PhoneName(uid, phone), unread?.Invoke(phone.Number) ?? 0, busy));
        }

        contacts.Sort((a, b) =>
        {
            var byUnread = b.Unread.CompareTo(a.Unread);
            return byUnread != 0 ? byUnread : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture);
        });
        return contacts;
    }

    /// <summary>Кто держит телефон (в руке, кармане или сумке на нём).</summary>
    private EntityUid? Holder(EntityUid phone)
    {
        var current = phone;
        for (var i = 0; i < 4; i++)
        {
            if (!_container.TryGetContainingContainer((current, null, null), out var container))
                return null;

            current = container.Owner;
            if (HasComp<ActorComponent>(current))
                return current;
        }

        return null;
    }

    private Entity<T>? Program<T>(EntityUid loader) where T : IComponent
    {
        foreach (var program in _cartridges.GetAllPrograms(loader))
        {
            if (TryComp<T>(program, out var comp))
                return (program, comp);
        }

        return null;
    }

    private bool IsActive(EntityUid loader, EntityUid program)
    {
        return TryComp<CartridgeLoaderComponent>(loader, out var comp) && comp.ActiveProgram == program;
    }

    private string Clock()
    {
        var time = _ticker.RoundDuration();
        return $"{(int) time.TotalHours:D2}:{time.Minutes:D2}";
    }

    #endregion

    #region Сообщения

    private void OnMessengerReady(Entity<DHMessengerCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateMessenger(ent, args.Loader);
    }

    private void OnMessengerMessage(Entity<DHMessengerCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        if (args is not DHMessengerUiMessageEvent message)
            return;

        var loader = GetEntity(args.LoaderUid);
        switch (message.Action)
        {
            case DHMessengerAction.Open:
                ent.Comp.Open = message.Number;
                ent.Comp.Unread.Remove(message.Number);
                break;
            case DHMessengerAction.Close:
                ent.Comp.Open = null;
                break;
            case DHMessengerAction.Send:
                Send(loader, ent, message.Number, message.Text, args.Actor);
                break;
        }

        UpdateMessenger(ent, loader);
    }

    private void Send(EntityUid loader, Entity<DHMessengerCartridgeComponent> sender, string number, string text,
        EntityUid user)
    {
        text = text.Trim();
        if (text.Length == 0 || !TryComp<DHPhoneComponent>(loader, out var from))
            return;

        if (text.Length > sender.Comp.MaxLength)
            text = text[..sender.Comp.MaxLength];

        if (FindPhone(number) is not { } target || target == loader ||
            Program<DHMessengerCartridgeComponent>(target) is not { } inbox)
        {
            _popup.PopupEntity(Loc.GetString("dh-phone-sms-no-number"), user, user);
            return;
        }

        var time = Clock();
        AddMessage(sender.Comp, number, new DHSmsEntry(true, text, time));
        AddMessage(inbox.Comp, from.Number, new DHSmsEntry(false, text, time));

        if (inbox.Comp.Open != from.Number || !IsActive(target, inbox))
            inbox.Comp.Unread[from.Number] = inbox.Comp.Unread.GetValueOrDefault(from.Number) + 1;

        _audio.PlayPvs(SmsSound, target);
        if (Holder(target) is { } holder)
        {
            _popup.PopupEntity(Loc.GetString("dh-phone-sms-received", ("from", PhoneName(loader, from))),
                holder, holder);
        }

        if (IsActive(target, inbox))
            UpdateMessenger(inbox, target);
    }

    private static void AddMessage(DHMessengerCartridgeComponent comp, string number, DHSmsEntry entry)
    {
        if (!comp.Chats.TryGetValue(number, out var chat))
        {
            chat = new List<DHSmsEntry>();
            comp.Chats[number] = chat;
        }

        chat.Add(entry);
        if (chat.Count > 50)
            chat.RemoveAt(0);
    }

    private void UpdateMessenger(Entity<DHMessengerCartridgeComponent> ent, EntityUid loader)
    {
        if (!TryComp<DHPhoneComponent>(loader, out var phone))
            return;

        var contacts = Contacts(loader, number => ent.Comp.Unread.GetValueOrDefault(number));
        string? openName = null;
        var messages = new List<DHSmsEntry>();
        if (ent.Comp.Open is { } open)
        {
            openName = open;
            foreach (var contact in contacts)
            {
                if (contact.Number == open)
                    openName = contact.Name;
            }

            if (ent.Comp.Chats.TryGetValue(open, out var chat))
                messages.AddRange(chat);
        }

        _cartridges.UpdateCartridgeUiState(loader,
            new DHMessengerUiState(phone.Number, contacts, ent.Comp.Open, openName, messages));
    }

    #endregion

    #region Банк LA-V

    private void OnBankReady(Entity<DHBankAppCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateBank(ent, args.Loader);
    }

    private void OnBankMessage(Entity<DHBankAppCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        if (args is not DHBankAppUiMessageEvent message)
            return;

        var loader = GetEntity(args.LoaderUid);
        switch (message.Action)
        {
            case DHBankAppAction.Login:
                var account = _bank.TryLogin(message.First, message.Second);
                ent.Comp.LoggedIn = account?.Number;
                ent.Comp.Status = account == null ? Loc.GetString("dh-bank-error-login") : null;
                break;
            case DHBankAppAction.Logout:
                ent.Comp.LoggedIn = null;
                ent.Comp.Status = null;
                break;
            case DHBankAppAction.Transfer:
                if (_bank.Get(ent.Comp.LoggedIn) is not { } from)
                    break;

                ent.Comp.Status = _bank.Transfer(from, message.First, message.Amount, out var error)
                    ? Loc.GetString("dh-bank-status-sent", ("amount", message.Amount))
                    : error;
                break;
        }

        UpdateBank(ent, loader);
    }

    private void UpdateBank(Entity<DHBankAppCartridgeComponent> ent, EntityUid loader)
    {
        var account = _bank.Get(ent.Comp.LoggedIn);
        var state = account == null
            ? new DHBankAppUiState(false, string.Empty, string.Empty, 0, new List<string>(), ent.Comp.Status)
            : new DHBankAppUiState(true, account.Number, account.Holder, account.Balance, _bank.RecentLog(account),
                ent.Comp.Status);
        _cartridges.UpdateCartridgeUiState(loader, state);
    }

    #endregion

    #region Звонки

    private void OnCallReady(Entity<DHCallCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateCall(args.Loader);
    }

    private void OnCallMessage(Entity<DHCallCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        if (args is not DHCallUiMessageEvent message)
            return;

        var loader = GetEntity(args.LoaderUid);
        if (!TryComp<TelephoneComponent>(loader, out var source))
            return;

        switch (message.Action)
        {
            case DHCallAction.Call:
                if (source.CurrentState != TelephoneState.Idle)
                {
                    _popup.PopupEntity(Loc.GetString("dh-phone-busy-self"), args.Actor, args.Actor);
                    break;
                }

                if (FindPhone(message.Number) is not { } target || target == loader ||
                    !TryComp<DHPhoneComponent>(target, out var targetPhone) || targetPhone.Stationary ||
                    !TryComp<TelephoneComponent>(target, out var receiver))
                {
                    _popup.PopupEntity(Loc.GetString("dh-phone-sms-no-number"), args.Actor, args.Actor);
                    break;
                }

                if (receiver.CurrentState != TelephoneState.Idle)
                {
                    _popup.PopupEntity(Loc.GetString("dh-phone-busy", ("phone", PhoneName(target, targetPhone))),
                        args.Actor, args.Actor);
                    break;
                }

                _telephone.CallTelephone((loader, source), (target, receiver), args.Actor);
                break;
            case DHCallAction.Answer:
                if (source.CurrentState == TelephoneState.Ringing)
                    _telephone.AnswerTelephone((loader, source), args.Actor);
                break;
            case DHCallAction.HangUp:
                _telephone.EndTelephoneCalls((loader, source));
                break;
        }

        UpdateCall(loader);
    }

    private void OnTelephoneState(Entity<DHPhoneComponent> ent, ref TelephoneStateChangeEvent args)
    {
        if (args.NewState == TelephoneState.Ringing && Holder(ent) is { } holder &&
            TryComp<TelephoneComponent>(ent, out var telephone))
        {
            var caller = Loc.GetString("dh-phone-unknown");
            foreach (var linked in telephone.LinkedTelephones)
            {
                if (TryComp<DHPhoneComponent>(linked, out var other))
                    caller = PhoneName(linked, other);
            }

            _popup.PopupEntity(Loc.GetString("dh-phone-incoming", ("caller", caller)), holder, holder,
                PopupType.Medium);
        }

        if (Program<DHCallCartridgeComponent>(ent) is { } app && IsActive(ent, app))
            UpdateCall(ent);
    }

    private void UpdateCall(EntityUid loader)
    {
        if (!TryComp<TelephoneComponent>(loader, out var telephone))
            return;

        var status = telephone.CurrentState switch
        {
            TelephoneState.Calling => DHCallStatus.Calling,
            TelephoneState.Ringing => DHCallStatus.Ringing,
            TelephoneState.InCall => DHCallStatus.InCall,
            _ => DHCallStatus.Idle,
        };

        string? other = null;
        foreach (var linked in telephone.LinkedTelephones)
        {
            if (TryComp<DHPhoneComponent>(linked, out var phone))
                other = PhoneName(linked, phone);
        }

        _cartridges.UpdateCartridgeUiState(loader, new DHCallUiState(status, other, Contacts(loader, callableOnly: true)));
    }

    #endregion
}
