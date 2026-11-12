using System.Numerics;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.City;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.City;

[UsedImplicitly]
public sealed class DHChequeMachineBoundUserInterface : BoundUserInterface
{
    private DHChequeMachineWindow? _window;

    public DHChequeMachineBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHChequeMachineWindow>();
        _window.OnSetSeller += () => SendMessage(new DHChequeSetSellerMessage());
        _window.OnSetAmount += (amount, description) => SendMessage(new DHChequeSetAmountMessage(amount, description));
        _window.OnPay += password => SendMessage(new DHChequePayMessage(password));
        _window.OnReset += () => SendMessage(new DHChequeResetMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHChequeMachineState machine)
            _window?.UpdateState(machine);
    }
}

/// <summary>Окно чековой машинки: продавец, сумма и товар, оплата картой покупателя.</summary>
public sealed class DHChequeMachineWindow : DefaultWindow
{
    public event Action? OnSetSeller;
    public event Action<int, string>? OnSetAmount;
    public event Action<string>? OnPay;
    public event Action? OnReset;

    private readonly Label _seller = new();
    private readonly Label _bill = new() { FontColorOverride = DHPhoneUi.Money };
    private readonly Label _goods = new() { FontColorOverride = DHPhoneUi.Dim };
    private readonly Label _card = new();
    private readonly LineEdit _amount = new() { HorizontalExpand = true };
    private readonly LineEdit _description = new() { HorizontalExpand = true };
    private readonly LineEdit _password = new() { HorizontalExpand = true };
    private readonly Button _sellerButton = new();
    private readonly Button _payButton = new();
    private readonly Label _status = new() { FontColorOverride = DHPhoneUi.Money };
    private readonly Label _error = new() { FontColorOverride = DHPhoneUi.Warn };

    public DHChequeMachineWindow()
    {
        DHFonts.RegisterTerminal(this);
        Title = Loc.GetString("dh-cheque-ui-title");
        MinSize = new Vector2(380, 360);

        var root = DHPhoneUi.Column(6);
        root.Margin = new Thickness(8);

        // Продавец
        root.AddChild(new Label { Text = Loc.GetString("dh-cheque-ui-section-seller"), FontColorOverride = DHPhoneUi.Dim });
        root.AddChild(_seller);
        _sellerButton.Text = Loc.GetString("dh-cheque-ui-set-seller");
        _sellerButton.OnPressed += _ => OnSetSeller?.Invoke();
        root.AddChild(_sellerButton);

        // Счёт к оплате
        root.AddChild(new Label { Text = Loc.GetString("dh-cheque-ui-section-bill"), FontColorOverride = DHPhoneUi.Dim });
        _amount.PlaceHolder = Loc.GetString("dh-bank-ui-amount");
        _description.PlaceHolder = Loc.GetString("dh-cheque-ui-description");
        var billRow = DHPhoneUi.Row();
        billRow.AddChild(_amount);
        var setBill = new Button { Text = Loc.GetString("dh-cheque-ui-set-amount") };
        setBill.OnPressed += _ =>
        {
            if (int.TryParse(_amount.Text.Trim(), out var amount) && amount > 0)
                OnSetAmount?.Invoke(amount, _description.Text);
        };
        billRow.AddChild(setBill);
        root.AddChild(billRow);
        root.AddChild(_description);
        root.AddChild(_bill);
        root.AddChild(_goods);

        // Оплата
        root.AddChild(new Label { Text = Loc.GetString("dh-cheque-ui-section-pay"), FontColorOverride = DHPhoneUi.Dim });
        root.AddChild(_card);
        _password.PlaceHolder = Loc.GetString("dh-bank-app-ui-password");
        _password.OnTextEntered += _ => Pay();
        _payButton.Text = Loc.GetString("dh-cheque-ui-pay");
        _payButton.OnPressed += _ => Pay();
        var payRow = DHPhoneUi.Row();
        payRow.AddChild(_password);
        payRow.AddChild(_payButton);
        root.AddChild(payRow);

        var reset = new Button { Text = Loc.GetString("dh-cheque-ui-reset") };
        reset.OnPressed += _ => OnReset?.Invoke();
        root.AddChild(reset);

        root.AddChild(_status);
        root.AddChild(_error);
        Contents.AddChild(root);
    }

    private void Pay()
    {
        OnPay?.Invoke(_password.Text);
        _password.Clear();
    }

    public void UpdateState(DHChequeMachineState state)
    {
        _seller.Text = state.SellerNumber.Length > 0
            ? Loc.GetString("dh-cheque-ui-seller", ("name", state.SellerName), ("number", state.SellerNumber))
            : Loc.GetString("dh-cheque-ui-no-seller");
        _sellerButton.Disabled = !state.HasCard;

        _bill.Text = state.Amount > 0
            ? Loc.GetString("dh-cheque-ui-bill", ("amount", state.Amount))
            : Loc.GetString("dh-cheque-ui-no-bill");
        _goods.Text = state.Goods.Length > 0
            ? Loc.GetString("dh-cheque-ui-goods", ("goods", state.Goods))
            : state.Description.Length > 0
                ? Loc.GetString("dh-cheque-ui-goods", ("goods", state.Description))
                : Loc.GetString("dh-cheque-ui-goods-hint");

        _card.Text = state.HasCard
            ? Loc.GetString("dh-atm-ui-card", ("holder", state.CardHolder), ("number", state.CardNumber))
            : Loc.GetString("dh-atm-ui-insert-card");
        _payButton.Disabled = !state.HasCard || state.Amount <= 0 || state.SellerNumber.Length == 0;

        _status.Text = state.Status ?? string.Empty;
        _status.Visible = state.Status != null;
        _error.Text = state.Error ?? string.Empty;
        _error.Visible = state.Error != null;
    }
}
