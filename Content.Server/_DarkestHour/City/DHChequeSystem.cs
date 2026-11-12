using System.Text;
using Content.Server.GameTicking;
using Content.Shared._DarkestHour.City;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Paper;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._DarkestHour.City;

/// <summary>
/// Чековая машинка «Банка LA-V» (решение автора 2026-11-12): продавец привязывает свою карту и выставляет сумму
/// (товар — в лоток), покупатель вставляет карту и вводит пароль. Перевод, два чека (покупателю и продавцу), товар —
/// покупателю в руки. После оплаты рассылается <see cref="DHChequeIssuedEvent"/> (задания продаж LA-V).
/// </summary>
public sealed partial class DHChequeSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DHBankSystem _bank = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private ServerGameTicker _ticker = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const string ChequeProto = "DHCheque";
    private const int MaxAmount = 1_000_000;

    private static readonly SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");
    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    private int _serial = 1000;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHChequeMachineComponent, EntInsertedIntoContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<DHChequeMachineComponent, EntRemovedFromContainerMessage>(OnSlotChanged);
        Subs.BuiEvents<DHChequeMachineComponent>(DHChequeMachineUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<DHChequeSetSellerMessage>(OnSetSeller);
            subs.Event<DHChequeSetAmountMessage>(OnSetAmount);
            subs.Event<DHChequePayMessage>(OnPay);
            subs.Event<DHChequeResetMessage>(OnReset);
        });
    }

    private void OnSlotChanged(Entity<DHChequeMachineComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        ent.Comp.Error = null;
        UpdateUi(ent);
    }

    private void OnSlotChanged(Entity<DHChequeMachineComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateUi(ent);
    }

    private void OnOpened(Entity<DHChequeMachineComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private DHBankCardComponent? Card(EntityUid machine)
    {
        return _itemSlots.GetItemOrNull(machine, DHChequeMachineComponent.CardSlotId) is { } item &&
               TryComp<DHBankCardComponent>(item, out var card)
            ? card
            : null;
    }

    private void OnSetSeller(Entity<DHChequeMachineComponent> ent, ref DHChequeSetSellerMessage args)
    {
        if (Card(ent) is not { } card || _bank.Get(card.Number) is not { } account)
        {
            Fail(ent, "dh-cheque-error-card");
            return;
        }

        ent.Comp.SellerNumber = account.Number;
        ent.Comp.SellerName = account.Holder;
        ent.Comp.Error = null;
        ent.Comp.Status = Loc.GetString("dh-cheque-status-seller", ("seller", account.Holder));
        if (GetSlot(ent, DHChequeMachineComponent.CardSlotId) is { } slot)
            _itemSlots.TryEjectToHands(ent, slot, args.Actor);
        UpdateUi(ent);
    }

    private ItemSlot? GetSlot(EntityUid machine, string id)
    {
        return _itemSlots.TryGetSlot(machine, id, out var slot) ? slot : null;
    }

    private void OnSetAmount(Entity<DHChequeMachineComponent> ent, ref DHChequeSetAmountMessage args)
    {
        if (args.Amount <= 0 || args.Amount > MaxAmount)
        {
            Fail(ent, "dh-cheque-error-amount");
            return;
        }

        ent.Comp.Amount = args.Amount;
        ent.Comp.Description = args.Description.Trim();
        if (ent.Comp.Description.Length > 60)
            ent.Comp.Description = ent.Comp.Description[..60];

        ent.Comp.Error = null;
        ent.Comp.Status = Loc.GetString("dh-cheque-status-amount", ("amount", ent.Comp.Amount));
        UpdateUi(ent);
    }

    private void OnReset(Entity<DHChequeMachineComponent> ent, ref DHChequeResetMessage args)
    {
        ent.Comp.Amount = 0;
        ent.Comp.Description = string.Empty;
        ent.Comp.Error = null;
        ent.Comp.Status = null;
        UpdateUi(ent);
    }

    private void OnPay(Entity<DHChequeMachineComponent> ent, ref DHChequePayMessage args)
    {
        var comp = ent.Comp;
        if (_bank.Get(comp.SellerNumber) is not { } seller)
        {
            Fail(ent, "dh-cheque-error-no-seller");
            return;
        }

        if (comp.Amount <= 0)
        {
            Fail(ent, "dh-cheque-error-no-amount");
            return;
        }

        if (Card(ent) is not { } card || _bank.Get(card.Number) is not { } buyer)
        {
            Fail(ent, "dh-cheque-error-card");
            return;
        }

        if (buyer.Password != args.Password.Trim())
        {
            Fail(ent, "dh-bank-error-password");
            return;
        }

        if (!_bank.Transfer(buyer, seller.Number, comp.Amount, out var error))
        {
            comp.Error = error;
            comp.Status = null;
            _audio.PlayPvs(DenySound, ent);
            UpdateUi(ent);
            return;
        }

        var goods = _itemSlots.GetItemOrNull(ent.Owner, DHChequeMachineComponent.GoodsSlotId);
        var goodsName = goods is { } g ? Name(g) : comp.Description;
        if (goodsName.Length == 0)
            goodsName = Loc.GetString("dh-cheque-goods-none");

        var goodsProto = goods is { } item ? MetaData(item).EntityPrototype?.ID ?? string.Empty : string.Empty;
        var time = DHCityClock.DateTimeText(_ticker.RoundDuration());
        var serial = ++_serial;

        PrintCheque(ent, args.Actor, serial, buyer, seller, comp.Amount, goodsName, goodsProto, time, false);
        PrintCheque(ent, args.Actor, serial, buyer, seller, comp.Amount, goodsName, goodsProto, time, true);
        _audio.PlayPvs(PrintSound, ent);

        RaiseLocalEvent(new DHChequeIssuedEvent(ent, args.Actor, seller.Number, buyer.Number, comp.Amount, goodsProto, goods));

        // Товар и карта — покупателю.
        if (GetSlot(ent, DHChequeMachineComponent.GoodsSlotId) is { } goodsSlot && goods != null)
            _itemSlots.TryEjectToHands(ent, goodsSlot, args.Actor);
        if (GetSlot(ent, DHChequeMachineComponent.CardSlotId) is { } cardSlot)
            _itemSlots.TryEjectToHands(ent, cardSlot, args.Actor);

        comp.Status = Loc.GetString("dh-cheque-status-paid", ("amount", comp.Amount), ("serial", serial));
        comp.Error = null;
        comp.Amount = 0;
        comp.Description = string.Empty;
        UpdateUi(ent);
    }

    private void PrintCheque(EntityUid machine, EntityUid user, int serial, DHBankAccount buyer, DHBankAccount seller,
        int amount, string goods, string goodsProto, string time, bool sellerCopy)
    {
        var cheque = Spawn(ChequeProto, Transform(machine).Coordinates);
        var comp = EnsureComp<DHChequeComponent>(cheque);
        comp.Serial = serial;
        comp.Buyer = buyer.Holder;
        comp.BuyerNumber = buyer.Number;
        comp.Seller = seller.Holder;
        comp.SellerNumber = seller.Number;
        comp.Amount = amount;
        comp.Goods = goods;
        comp.GoodsProto = goodsProto;
        comp.Time = time;
        comp.SellerCopy = sellerCopy;
        Dirty(cheque, comp);

        var text = new StringBuilder();
        text.AppendLine(Loc.GetString("dh-cheque-text-header"));
        text.AppendLine(Loc.GetString("dh-cheque-text-serial", ("serial", serial),
            ("copy", Loc.GetString(sellerCopy ? "dh-cheque-copy-seller" : "dh-cheque-copy-buyer"))));
        text.AppendLine(Loc.GetString("dh-cheque-text-time", ("time", time)));
        text.AppendLine();
        text.AppendLine(Loc.GetString("dh-cheque-text-seller", ("name", seller.Holder), ("number", seller.Number)));
        text.AppendLine(Loc.GetString("dh-cheque-text-buyer", ("name", buyer.Holder), ("number", buyer.Number)));
        text.AppendLine(Loc.GetString("dh-cheque-text-goods", ("goods", goods)));
        text.AppendLine();
        text.AppendLine(Loc.GetString("dh-cheque-text-amount", ("amount", amount)));
        text.AppendLine(Loc.GetString("dh-cheque-text-footer"));

        if (TryComp<PaperComponent>(cheque, out var paper))
        {
            _paper.SetContent((cheque, paper), text.ToString());
            paper.EditingDisabled = true;
            Dirty(cheque, paper);
        }

        _hands.PickupOrDrop(user, cheque);
    }

    private void Fail(Entity<DHChequeMachineComponent> ent, string error)
    {
        ent.Comp.Error = Loc.GetString(error);
        ent.Comp.Status = null;
        _audio.PlayPvs(DenySound, ent);
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<DHChequeMachineComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, DHChequeMachineUiKey.Key))
            return;

        var card = Card(ent);
        var goods = _itemSlots.GetItemOrNull(ent.Owner, DHChequeMachineComponent.GoodsSlotId);
        _ui.SetUiState(ent.Owner, DHChequeMachineUiKey.Key, new DHChequeMachineState(
            card != null,
            card?.Holder ?? string.Empty,
            card?.Number ?? string.Empty,
            ent.Comp.SellerName,
            ent.Comp.SellerNumber,
            ent.Comp.Amount,
            ent.Comp.Description,
            goods is { } g ? Name(g) : string.Empty,
            ent.Comp.Error,
            ent.Comp.Status));
    }
}
