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

        InitializeAtm();

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
