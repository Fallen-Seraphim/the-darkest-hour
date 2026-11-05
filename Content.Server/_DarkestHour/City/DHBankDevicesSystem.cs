using Content.Server._DarkestHour.Uprising;
using Content.Server.Stack;
using Content.Shared._DarkestHour.City;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._DarkestHour.City;

/// <summary>
/// Банкоматы (карта в слоте → пароль → снятие наличных; внесение — щелчком купюрами по банкомату) и компьютеры
/// бюджета организаций (баланс, история, переводы, снятие; у LA-V — вызов корпоративного борта за 40 000 кр.).
/// </summary>
public sealed partial class DHBankDevicesSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DHBankSystem _bank = default!;
    [Dependency] private DHCityVendorSystem _vendors = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private DHUprisingRuleSystem _uprising = default!;

    /// <summary>Стоимость корпоративного эвакуационного борта (решение автора 2026-11-05).</summary>
    public const int EvacCost = 40000;

    private const int MaxWithdraw = 50000;

    private static readonly SoundSpecifier CashSound = new SoundPathSpecifier("/Audio/Machines/tray_eject.ogg");
    private static readonly SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");
    private static readonly SoundSpecifier OkSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHAtmComponent, InteractUsingEvent>(OnAtmInteractUsing);
        SubscribeLocalEvent<DHAtmComponent, EntInsertedIntoContainerMessage>(OnAtmCardChanged);
        SubscribeLocalEvent<DHAtmComponent, EntRemovedFromContainerMessage>(OnAtmCardChanged);
        Subs.BuiEvents<DHAtmComponent>(DHAtmUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnAtmOpened);
            subs.Event<DHAtmLoginMessage>(OnAtmLogin);
            subs.Event<DHAtmWithdrawMessage>(OnAtmWithdraw);
        });

        Subs.BuiEvents<DHBudgetComputerComponent>(DHBudgetUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnBudgetOpened);
            subs.Event<DHBudgetTransferMessage>(OnBudgetTransfer);
            subs.Event<DHBudgetWithdrawMessage>(OnBudgetWithdraw);
            subs.Event<DHBudgetEvacMessage>(OnBudgetEvac);
        });
    }

    /// <summary>Наличные — в руки (или под ноги).</summary>
    private void GiveCash(EntityUid source, EntityUid user, int amount)
    {
        foreach (var cash in _stack.SpawnMultipleNextToOrDrop(DHCityCurrency.Cash, amount, source))
        {
            _hands.PickupOrDrop(user, cash);
        }

        _audio.PlayPvs(CashSound, source);
    }

    #region Банкомат

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

    private void OnAtmCardChanged(Entity<DHAtmComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != DHAtmComponent.CardSlotId)
            return;

        ent.Comp.Authorized = null;
        ent.Comp.Error = null;
        UpdateAtm(ent);
    }

    private void OnAtmCardChanged(Entity<DHAtmComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != DHAtmComponent.CardSlotId)
            return;

        ent.Comp.Authorized = null;
        ent.Comp.Error = null;
        UpdateAtm(ent);
    }

    private void OnAtmOpened(Entity<DHAtmComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateAtm(ent);
    }

    private void OnAtmLogin(Entity<DHAtmComponent> ent, ref DHAtmLoginMessage args)
    {
        var account = AtmCardAccount(ent, out _);
        if (account == null)
        {
            ent.Comp.Error = Loc.GetString("dh-bank-error-card");
        }
        else if (account.Password != args.Password.Trim())
        {
            ent.Comp.Error = Loc.GetString("dh-bank-error-password");
            _audio.PlayPvs(DenySound, ent);
        }
        else
        {
            ent.Comp.Authorized = account.Number;
            ent.Comp.Error = null;
            _audio.PlayPvs(OkSound, ent);
        }

        UpdateAtm(ent);
    }

    private void OnAtmWithdraw(Entity<DHAtmComponent> ent, ref DHAtmWithdrawMessage args)
    {
        var account = AtmCardAccount(ent, out _);
        if (account == null || ent.Comp.Authorized != account.Number)
            return;

        var amount = Math.Clamp(args.Amount, 0, MaxWithdraw);
        if (amount <= 0 || !_bank.Withdraw(account, amount, Loc.GetString("dh-bank-note-atm")))
        {
            ent.Comp.Error = Loc.GetString("dh-bank-error-funds");
            _audio.PlayPvs(DenySound, ent);
            UpdateAtm(ent);
            return;
        }

        ent.Comp.Error = null;
        GiveCash(ent, args.Actor, amount);
        UpdateAtm(ent);
    }

    /// <summary>Внесение: щёлкнуть республиканскими кредитами по банкомату с картой и введённым паролем.</summary>
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
        _audio.PlayPvs(CashSound, ent);
        _popup.PopupEntity(Loc.GetString("dh-atm-deposited", ("amount", amount)), ent, args.User);
        UpdateAtm(ent);
    }

    private void UpdateAtm(Entity<DHAtmComponent> ent)
    {
        var account = AtmCardAccount(ent, out var card);
        var hasCard = card != null;
        var authorized = account != null && ent.Comp.Authorized == account.Number;
        var state = new DHAtmState(hasCard, authorized,
            card?.Number ?? string.Empty,
            card?.Holder ?? string.Empty,
            authorized ? account!.Balance : 0,
            hasCard && account == null ? Loc.GetString("dh-bank-error-card") : ent.Comp.Error);
        _ui.SetUiState(ent.Owner, DHAtmUiKey.Key, state);
    }

    #endregion

    #region Компьютер бюджета

    private void OnBudgetOpened(Entity<DHBudgetComputerComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateBudget(ent, null);
    }

    private void OnBudgetTransfer(Entity<DHBudgetComputerComponent> ent, ref DHBudgetTransferMessage args)
    {
        var account = _bank.OrgAccount(ent.Comp.Org);
        string status;
        if (_bank.Transfer(account, args.Number, args.Amount, out var error))
        {
            status = Loc.GetString("dh-bank-status-sent", ("amount", args.Amount));
            _audio.PlayPvs(OkSound, ent);
        }
        else
        {
            status = error;
            _audio.PlayPvs(DenySound, ent);
        }

        UpdateBudget(ent, status);
    }

    private void OnBudgetWithdraw(Entity<DHBudgetComputerComponent> ent, ref DHBudgetWithdrawMessage args)
    {
        var account = _bank.OrgAccount(ent.Comp.Org);
        var amount = Math.Clamp(args.Amount, 0, MaxWithdraw);
        if (amount <= 0 || !_bank.Withdraw(account, amount,
                Loc.GetString("dh-bank-note-withdraw", ("name", Name(args.Actor)))))
        {
            _audio.PlayPvs(DenySound, ent);
            UpdateBudget(ent, Loc.GetString("dh-bank-error-funds"));
            return;
        }

        GiveCash(ent, args.Actor, amount);
        UpdateBudget(ent, Loc.GetString("dh-bank-status-withdrawn", ("amount", amount)));
    }

    private void OnBudgetEvac(Entity<DHBudgetComputerComponent> ent, ref DHBudgetEvacMessage args)
    {
        if (ent.Comp.Org != DHBankOrg.Vanguard)
            return;

        var account = _bank.OrgAccount(ent.Comp.Org);
        if (account.Balance < EvacCost)
        {
            _audio.PlayPvs(DenySound, ent);
            UpdateBudget(ent, Loc.GetString("dh-bank-error-funds"));
            return;
        }

        if (!_uprising.CanCallCorporateFlight(out var reason))
        {
            _audio.PlayPvs(DenySound, ent);
            UpdateBudget(ent, reason);
            return;
        }

        _bank.Withdraw(account, EvacCost, Loc.GetString("dh-bank-note-evac"));
        _uprising.TryCallCorporateFlight(out _);
        _audio.PlayPvs(OkSound, ent);
        UpdateBudget(ent, Loc.GetString("dh-bank-status-evac"));
    }

    private void UpdateBudget(Entity<DHBudgetComputerComponent> ent, string? status)
    {
        var account = _bank.OrgAccount(ent.Comp.Org);
        var evac = ent.Comp.Org == DHBankOrg.Vanguard && _uprising.CanCallCorporateFlight(out _);
        var state = new DHBudgetState(ent.Comp.Org, account.Number, account.Balance, _bank.RecentLog(account),
            ent.Comp.Org == DHBankOrg.Vanguard ? EvacCost : 0, evac, status);
        _ui.SetUiState(ent.Owner, DHBudgetUiKey.Key, state);
    }

    #endregion
}
