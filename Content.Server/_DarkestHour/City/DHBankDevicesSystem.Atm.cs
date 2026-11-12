using Content.Shared._DarkestHour.City;
using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.City;

/// <summary>
/// Банкомат (решение автора 2026-11-12, как банкомат CMU14, с маркой LA-V): карта в приёмник щелчком по нему,
/// PIN-код из 4 цифр с клавиатуры, меню цифрами. Снятые наличные ждут в лотке, пока их не заберут; внесение — щелчком
/// купюрами по банкомату. Три неверных PIN-кода подряд — карта выдаётся обратно и на минуту блокируется на этом банкомате.
/// </summary>
public sealed partial class DHBankDevicesSystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly SoundSpecifier CardInSound = new SoundPathSpecifier("/Audio/_DarkestHour/City/Atm/atm_card_in.wav");
    private static readonly SoundSpecifier CardOutSound = new SoundPathSpecifier("/Audio/_DarkestHour/City/Atm/atm_card_out.wav");
    private static readonly SoundSpecifier AtmCashSound = new SoundPathSpecifier("/Audio/_DarkestHour/City/Atm/atm_cash.wav");

    private static readonly TimeSpan LockTime = TimeSpan.FromMinutes(1);
    private const int PinLength = 4;
    private const int HistoryPage = 8;

    private void InitializeAtm()
    {
        SubscribeLocalEvent<DHAtmComponent, InteractUsingEvent>(OnAtmInteractUsing);
        SubscribeLocalEvent<DHAtmComponent, EntInsertedIntoContainerMessage>(OnAtmCardInserted);
        SubscribeLocalEvent<DHAtmComponent, EntRemovedFromContainerMessage>(OnAtmCardRemoved);
        Subs.BuiEvents<DHAtmComponent>(DHAtmUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnAtmOpened);
            subs.Event<DHAtmKeyMessage>(OnAtmKey);
            subs.Event<DHAtmReaderMessage>(OnAtmReader);
            subs.Event<DHAtmTakeCashMessage>(OnAtmTakeCash);
        });
    }

    private DHBankAccount? AtmCardAccount(EntityUid atm, out DHBankCardComponent? card)
    {
        card = null;
        if (_itemSlots.GetItemOrNull(atm, DHAtmComponent.CardSlotId) is not { } item ||
            !TryComp(item, out card))
        {
            return null;
        }

        return _bank.Get(card.Number);
    }

    private void OnAtmOpened(Entity<DHAtmComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateAtm(ent);
    }

    private void OnAtmCardInserted(Entity<DHAtmComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != DHAtmComponent.CardSlotId)
            return;

        _audio.PlayPvs(CardInSound, ent);
        ResetSession(ent.Comp);
        var account = AtmCardAccount(ent, out _);
        if (account == null)
        {
            ShowMessage(ent.Comp, Loc.GetString("dh-bank-error-card"), true, DHAtmScreen.Insert);
        }
        else if (ent.Comp.Locked.TryGetValue(account.Number, out var until) && _timing.CurTime < until)
        {
            ShowMessage(ent.Comp, Loc.GetString("dh-atm-screen-locked",
                ("seconds", (int) Math.Ceiling((until - _timing.CurTime).TotalSeconds))), true, DHAtmScreen.Pin);
        }
        else
        {
            ent.Comp.Screen = DHAtmScreen.Pin;
        }

        UpdateAtm(ent);
    }

    private void OnAtmCardRemoved(Entity<DHAtmComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != DHAtmComponent.CardSlotId)
            return;

        _audio.PlayPvs(CardOutSound, ent);
        ResetSession(ent.Comp);
        ent.Comp.Screen = DHAtmScreen.Insert;
        UpdateAtm(ent);
    }

    private static void ResetSession(DHAtmComponent comp)
    {
        comp.Authorized = null;
        comp.Input = string.Empty;
        comp.Attempts = 0;
        comp.TransferTo = string.Empty;
        comp.HistoryOffset = 0;
        comp.Message = string.Empty;
        comp.MessageIsError = false;
    }

    private static void ShowMessage(DHAtmComponent comp, string message, bool error, DHAtmScreen after)
    {
        comp.Message = message;
        comp.MessageIsError = error;
        comp.AfterMessage = after;
        comp.Screen = DHAtmScreen.Message;
        comp.Input = string.Empty;
    }

    /// <summary>Карта, которую пользователь вставит щелчком по приёмнику: в руке, иначе в снаряжении (кошелёк и т. п.).</summary>
    private EntityUid? FindUserCard(EntityUid user)
    {
        if (_hands.GetActiveItem(user) is { } held && HasComp<DHBankCardComponent>(held))
        {
            return held;
        }

        return FindCardIn(user, 0);
    }

    private EntityUid? FindCardIn(EntityUid root, int depth)
    {
        if (depth > 3 || !TryComp<ContainerManagerComponent>(root, out var manager))
            return null;

        foreach (var container in _container.GetAllContainers(root, manager))
        {
            foreach (var child in container.ContainedEntities)
            {
                if (HasComp<DHBankCardComponent>(child))
                    return child;

                if (FindCardIn(child, depth + 1) is { } found)
                    return found;
            }
        }

        return null;
    }

    private void OnAtmReader(Entity<DHAtmComponent> ent, ref DHAtmReaderMessage args)
    {
        if (!_itemSlots.TryGetSlot(ent.Owner, DHAtmComponent.CardSlotId, out var slot))
            return;

        if (slot.HasItem)
        {
            _itemSlots.TryEjectToHands(ent, slot, args.Actor);
            return;
        }

        if (FindUserCard(args.Actor) is not { } card)
        {
            ShowMessage(ent.Comp, Loc.GetString("dh-atm-screen-no-card"), true, DHAtmScreen.Insert);
            _audio.PlayPvs(DenySound, ent);
            UpdateAtm(ent);
            return;
        }

        // Из руки или из кошелька — сначала вынуть, потом в приёмник; не влезла — обратно в руки.
        _container.TryRemoveFromContainer(card, force: true);
        if (!_itemSlots.TryInsert(ent, slot, card, args.Actor))
            _hands.PickupOrDrop(args.Actor, card);
    }

    private void OnAtmTakeCash(Entity<DHAtmComponent> ent, ref DHAtmTakeCashMessage args)
    {
        if (ent.Comp.CashPending <= 0)
            return;

        var amount = ent.Comp.CashPending;
        ent.Comp.CashPending = 0;
        GiveCash(ent, args.Actor, amount);
        UpdateAtm(ent);
    }

    private void OnAtmKey(Entity<DHAtmComponent> ent, ref DHAtmKeyMessage args)
    {
        var comp = ent.Comp;
        var key = args.Key;
        var account = AtmCardAccount(ent, out _);

        // Без карты или после её потери — только экран «вставьте карту».
        if (account == null && comp.Screen is not (DHAtmScreen.Insert or DHAtmScreen.Message))
        {
            comp.Screen = DHAtmScreen.Insert;
            UpdateAtm(ent);
            return;
        }

        switch (comp.Screen)
        {
            case DHAtmScreen.Insert:
                break;

            case DHAtmScreen.Message:
                if (key is "enter" or "cancel")
                {
                    comp.Screen = comp.AfterMessage;
                    if (comp.Screen == DHAtmScreen.Pin && account != null &&
                        comp.Locked.TryGetValue(account.Number, out var until) && _timing.CurTime < until)
                    {
                        Eject(ent, args.Actor);
                    }
                }
                break;

            case DHAtmScreen.Pin:
                if (key == "cancel")
                {
                    Eject(ent, args.Actor);
                    break;
                }

                if (!Type(comp, key, PinLength) && key == "enter" && account != null)
                    CheckPin(ent, account, args.Actor);
                break;

            case DHAtmScreen.Menu:
                switch (key)
                {
                    case "1":
                        ShowMessage(comp, Loc.GetString("dh-atm-screen-balance", ("balance", account!.Balance)), false,
                            DHAtmScreen.Menu);
                        break;
                    case "2":
                        comp.Screen = DHAtmScreen.Withdraw;
                        comp.Input = string.Empty;
                        break;
                    case "3":
                        comp.Screen = DHAtmScreen.TransferAccount;
                        comp.Input = string.Empty;
                        break;
                    case "4":
                        comp.Screen = DHAtmScreen.History;
                        comp.HistoryOffset = 0;
                        break;
                    case "cancel":
                        Eject(ent, args.Actor);
                        break;
                }
                break;

            case DHAtmScreen.Withdraw:
                if (key == "cancel")
                {
                    comp.Screen = DHAtmScreen.Menu;
                    break;
                }

                if (!Type(comp, key, 6) && key == "enter")
                    Withdraw(ent, account!);
                break;

            case DHAtmScreen.TransferAccount:
                if (key == "cancel")
                {
                    comp.Screen = DHAtmScreen.Menu;
                    break;
                }

                if (!Type(comp, key, 8) && key == "enter")
                {
                    var number = comp.Input.Length == 8 ? $"{comp.Input[..4]}-{comp.Input[4..]}" : comp.Input;
                    var target = _bank.Get(number);
                    if (target == null || target == account)
                    {
                        ShowMessage(comp, Loc.GetString(target == null ? "dh-bank-error-no-account" : "dh-bank-error-self"),
                            true, DHAtmScreen.TransferAccount);
                    }
                    else
                    {
                        comp.TransferTo = target.Number;
                        comp.Screen = DHAtmScreen.TransferAmount;
                        comp.Input = string.Empty;
                    }
                }
                break;

            case DHAtmScreen.TransferAmount:
                if (key == "cancel")
                {
                    comp.Screen = DHAtmScreen.Menu;
                    break;
                }

                if (!Type(comp, key, 6) && key == "enter")
                {
                    string? error = null;
                    if (int.TryParse(comp.Input, out var amount) && _bank.Transfer(account!, comp.TransferTo, amount, out error))
                    {
                        _audio.PlayPvs(OkSound, ent);
                        ShowMessage(comp, Loc.GetString("dh-atm-screen-sent", ("amount", amount), ("number", comp.TransferTo)),
                            false, DHAtmScreen.Menu);
                    }
                    else
                    {
                        ShowMessage(comp, int.TryParse(comp.Input, out _) && error != null ? error : Loc.GetString("dh-bank-error-amount"),
                            true, DHAtmScreen.TransferAmount);
                    }
                }
                break;

            case DHAtmScreen.History:
                switch (key)
                {
                    case "2":
                        comp.HistoryOffset += HistoryPage;
                        break;
                    case "8":
                        comp.HistoryOffset = Math.Max(0, comp.HistoryOffset - HistoryPage);
                        break;
                    case "cancel" or "enter":
                        comp.Screen = DHAtmScreen.Menu;
                        break;
                }
                break;
        }

        UpdateAtm(ent);
    }

    /// <summary>Набор с клавиатуры: цифры (до <paramref name="max"/>) и стирание. True — клавиша съедена.</summary>
    private static bool Type(DHAtmComponent comp, string key, int max)
    {
        switch (key)
        {
            case "clear":
                if (comp.Input.Length > 0)
                    comp.Input = comp.Input[..^1];
                return true;
            case "enter" or "cancel":
                return false;
        }

        foreach (var c in key)
        {
            if (!char.IsDigit(c) || comp.Input.Length >= max)
                break;

            comp.Input += c;
        }

        return true;
    }

    private void CheckPin(Entity<DHAtmComponent> ent, DHBankAccount account, EntityUid user)
    {
        var comp = ent.Comp;
        if (comp.Input == account.Password)
        {
            comp.Authorized = account.Number;
            comp.Attempts = 0;
            comp.Input = string.Empty;
            comp.Screen = DHAtmScreen.Menu;
            _audio.PlayPvs(OkSound, ent);
            return;
        }

        comp.Attempts++;
        _audio.PlayPvs(DenySound, ent);
        if (comp.Attempts >= DHAtmComponent.MaxAttempts)
        {
            comp.Locked[account.Number] = _timing.CurTime + LockTime;
            Eject(ent, user);
            ShowMessage(comp, Loc.GetString("dh-atm-screen-locked", ("seconds", (int) LockTime.TotalSeconds)), true,
                DHAtmScreen.Insert);
            return;
        }

        ShowMessage(comp, Loc.GetString("dh-atm-screen-wrong-pin", ("left", DHAtmComponent.MaxAttempts - comp.Attempts)),
            true, DHAtmScreen.Pin);
    }

    private void Withdraw(Entity<DHAtmComponent> ent, DHBankAccount account)
    {
        var comp = ent.Comp;
        if (!int.TryParse(comp.Input, out var amount) || amount <= 0 || amount > MaxWithdraw)
        {
            ShowMessage(comp, Loc.GetString("dh-bank-error-amount"), true, DHAtmScreen.Withdraw);
            return;
        }

        if (!_bank.Withdraw(account, amount, Loc.GetString("dh-bank-note-atm")))
        {
            _audio.PlayPvs(DenySound, ent);
            ShowMessage(comp, Loc.GetString("dh-bank-error-funds"), true, DHAtmScreen.Withdraw);
            return;
        }

        comp.CashPending += amount;
        _audio.PlayPvs(AtmCashSound, ent);
        ShowMessage(comp, Loc.GetString("dh-atm-screen-take-cash", ("amount", amount)), false, DHAtmScreen.Menu);
    }

    private void Eject(Entity<DHAtmComponent> ent, EntityUid user)
    {
        if (_itemSlots.TryGetSlot(ent.Owner, DHAtmComponent.CardSlotId, out var slot) && slot.HasItem)
            _itemSlots.TryEjectToHands(ent, slot, user);
    }

    /// <summary>Внесение: щёлкнуть республиканскими кредитами по банкомату с картой и введённым PIN-кодом.</summary>
    private void OnAtmInteractUsing(Entity<DHAtmComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || _vendors.RepublicCash(args.Used) is not { } amount)
            return;

        args.Handled = true;
        var account = AtmCardAccount(ent, out _);
        if (account == null || ent.Comp.Authorized != account.Number)
        {
            _popup.PopupEntity(Loc.GetString("dh-atm-need-login"), ent, args.User);
            _audio.PlayPvs(DenySound, ent);
            return;
        }

        _bank.Deposit(account, amount, Loc.GetString("dh-bank-note-atm"));
        QueueDel(args.Used);
        _audio.PlayPvs(AtmCashSound, ent);
        ShowMessage(ent.Comp, Loc.GetString("dh-atm-deposited", ("amount", amount)), false, DHAtmScreen.Menu);
        UpdateAtm(ent);
    }

    private void UpdateAtm(Entity<DHAtmComponent> ent)
    {
        var comp = ent.Comp;
        var account = AtmCardAccount(ent, out var card);
        var lines = new List<string> { Loc.GetString("dh-atm-screen-header"), string.Empty };
        var light = DHAtmReaderLight.Idle;
        var error = false;

        switch (comp.Screen)
        {
            case DHAtmScreen.Insert:
                lines.Add(Loc.GetString("dh-atm-screen-welcome"));
                lines.Add(string.Empty);
                lines.Add(Loc.GetString("dh-atm-screen-insert"));
                light = DHAtmReaderLight.Ready;
                break;

            case DHAtmScreen.Pin:
                lines.Add(Loc.GetString("dh-atm-screen-card", ("holder", card?.Holder ?? string.Empty)));
                lines.Add(card?.Number ?? string.Empty);
                lines.Add(string.Empty);
                lines.Add(Loc.GetString("dh-atm-screen-pin"));
                lines.Add("  " + new string('*', comp.Input.Length) + new string('_', PinLength - comp.Input.Length));
                lines.Add(string.Empty);
                lines.Add(Loc.GetString("dh-atm-screen-pin-hint"));
                light = DHAtmReaderLight.Reading;
                break;

            case DHAtmScreen.Menu:
                lines.Add(Loc.GetString("dh-atm-screen-card", ("holder", account?.Holder ?? string.Empty)));
                lines.Add(Loc.GetString("dh-atm-screen-balance-short", ("balance", account?.Balance ?? 0)));
                lines.Add(string.Empty);
                lines.Add(Loc.GetString("dh-atm-screen-menu-1"));
                lines.Add(Loc.GetString("dh-atm-screen-menu-2"));
                lines.Add(Loc.GetString("dh-atm-screen-menu-3"));
                lines.Add(Loc.GetString("dh-atm-screen-menu-4"));
                lines.Add(string.Empty);
                lines.Add(Loc.GetString("dh-atm-screen-menu-cancel"));
                light = DHAtmReaderLight.Ok;
                break;

            case DHAtmScreen.Withdraw:
                lines.Add(Loc.GetString("dh-atm-screen-withdraw"));
                lines.Add(Loc.GetString("dh-atm-screen-balance-short", ("balance", account?.Balance ?? 0)));
                lines.Add(string.Empty);
                lines.Add("  " + (comp.Input.Length > 0 ? comp.Input : "_") + " " + Loc.GetString("dh-atm-screen-cr"));
                lines.Add(string.Empty);
                lines.Add(Loc.GetString("dh-atm-screen-enter-cancel"));
                light = DHAtmReaderLight.Ok;
                break;

            case DHAtmScreen.TransferAccount:
                lines.Add(Loc.GetString("dh-atm-screen-transfer-account"));
                lines.Add(string.Empty);
                var typed = comp.Input.PadRight(8, '_');
                lines.Add("  " + typed[..4] + "-" + typed[4..]);
                lines.Add(string.Empty);
                lines.Add(Loc.GetString("dh-atm-screen-enter-cancel"));
                light = DHAtmReaderLight.Ok;
                break;

            case DHAtmScreen.TransferAmount:
                var to = _bank.Get(comp.TransferTo);
                lines.Add(Loc.GetString("dh-atm-screen-transfer-to", ("holder", to?.Holder ?? "?"), ("number", comp.TransferTo)));
                lines.Add(Loc.GetString("dh-atm-screen-balance-short", ("balance", account?.Balance ?? 0)));
                lines.Add(string.Empty);
                lines.Add(Loc.GetString("dh-atm-screen-transfer-amount"));
                lines.Add("  " + (comp.Input.Length > 0 ? comp.Input : "_") + " " + Loc.GetString("dh-atm-screen-cr"));
                lines.Add(string.Empty);
                lines.Add(Loc.GetString("dh-atm-screen-enter-cancel"));
                light = DHAtmReaderLight.Ok;
                break;

            case DHAtmScreen.History:
                lines.Add(Loc.GetString("dh-atm-screen-history"));
                if (account != null)
                {
                    var log = _bank.RecentLog(account, 30);
                    comp.HistoryOffset = Math.Clamp(comp.HistoryOffset, 0, Math.Max(0, log.Count - 1));
                    for (var i = comp.HistoryOffset; i < Math.Min(log.Count, comp.HistoryOffset + HistoryPage); i++)
                    {
                        lines.Add(log[i]);
                    }
                }

                lines.Add(string.Empty);
                lines.Add(Loc.GetString("dh-atm-screen-history-hint"));
                light = DHAtmReaderLight.Ok;
                break;

            case DHAtmScreen.Message:
                lines.Add(comp.Message);
                lines.Add(string.Empty);
                lines.Add(Loc.GetString("dh-atm-screen-continue"));
                error = comp.MessageIsError;
                light = comp.MessageIsError ? DHAtmReaderLight.Locked : card != null ? DHAtmReaderLight.Ok : DHAtmReaderLight.Ready;
                break;
        }

        if (comp.CashPending > 0)
        {
            lines.Add(string.Empty);
            lines.Add(Loc.GetString("dh-atm-screen-cash-waiting", ("amount", comp.CashPending)));
        }

        _ui.SetUiState(ent.Owner, DHAtmUiKey.Key, new DHAtmState(comp.Screen, lines, card != null, light, comp.CashPending, error));
    }
}
