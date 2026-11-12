using System.Numerics;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.City;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.City;

// ---------------------------------------------------------------- компьютер бюджета

[UsedImplicitly]
public sealed class DHBudgetBoundUserInterface : BoundUserInterface
{
    private DHBudgetWindow? _window;

    public DHBudgetBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHBudgetWindow>();
        _window.OnTransfer += (number, amount) => SendMessage(new DHBudgetTransferMessage(number, amount));
        _window.OnWithdraw += amount => SendMessage(new DHBudgetWithdrawMessage(amount));
        _window.OnEvac += () => SendMessage(new DHBudgetEvacMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHBudgetState budget)
            _window?.UpdateState(budget);
    }
}

public sealed class DHBudgetWindow : DefaultWindow
{
    public event Action<string, int>? OnTransfer;
    public event Action<int>? OnWithdraw;
    public event Action? OnEvac;

    private readonly Label _number = new() { FontColorOverride = DHPhoneUi.Dim };
    private readonly Label _balance = new() { FontColorOverride = DHPhoneUi.Money };
    private readonly LineEdit _to = new() { HorizontalExpand = true };
    private readonly LineEdit _amount = new() { HorizontalExpand = true };
    private readonly Button _evac = new() { HorizontalExpand = true };
    private readonly Label _status = new() { FontColorOverride = DHPhoneUi.Warn };
    private readonly BoxContainer _log = DHPhoneUi.Column(2);

    public DHBudgetWindow()
    {
        DHFonts.RegisterTerminal(this);
        MinSize = new Vector2(440, 480);

        var root = DHPhoneUi.Column(6);
        root.Margin = new Thickness(8);
        root.AddChild(_number);
        root.AddChild(_balance);

        root.AddChild(new Label { Text = Loc.GetString("dh-bank-ui-transfer"), FontColorOverride = DHPhoneUi.Accent });
        _to.PlaceHolder = Loc.GetString("dh-bank-ui-to");
        _amount.PlaceHolder = Loc.GetString("dh-bank-ui-amount");
        root.AddChild(_to);
        var row = DHPhoneUi.Row(6);
        row.AddChild(_amount);
        var send = new Button { Text = Loc.GetString("dh-bank-ui-send") };
        send.OnPressed += _ =>
        {
            if (Amount() is { } amount)
                OnTransfer?.Invoke(_to.Text, amount);
        };
        var withdraw = new Button { Text = Loc.GetString("dh-atm-ui-withdraw") };
        withdraw.OnPressed += _ =>
        {
            if (Amount() is { } amount)
                OnWithdraw?.Invoke(amount);
        };
        row.AddChild(send);
        row.AddChild(withdraw);
        root.AddChild(row);

        _evac.OnPressed += _ => OnEvac?.Invoke();
        root.AddChild(_evac);
        root.AddChild(_status);

        root.AddChild(new Label { Text = Loc.GetString("dh-bank-ui-history"), FontColorOverride = DHPhoneUi.Accent });
        root.AddChild(DHPhoneUi.Scroll(_log));
        root.VerticalExpand = true;
        Contents.AddChild(root);
    }

    private int? Amount()
    {
        var ok = int.TryParse(_amount.Text.Trim(), out var amount) && amount > 0;
        _amount.Clear();
        return ok ? amount : null;
    }

    public void UpdateState(DHBudgetState state)
    {
        Title = Loc.GetString("dh-budget-ui-title-" + state.Org.ToString().ToLowerInvariant());
        _number.Text = Loc.GetString("dh-bank-ui-number", ("number", state.Number));
        _balance.Text = Loc.GetString("dh-bank-ui-balance", ("balance", state.Balance));

        _evac.Visible = state.EvacCost > 0;
        _evac.Text = Loc.GetString("dh-budget-ui-evac", ("cost", state.EvacCost));
        _evac.Disabled = !state.EvacAvailable || state.Balance < state.EvacCost;

        _status.Text = state.Status ?? string.Empty;
        _status.Visible = state.Status != null;

        _log.RemoveAllChildren();
        foreach (var line in state.Log)
        {
            _log.AddChild(new Label { Text = line, FontColorOverride = DHPhoneUi.Dim, ClipText = true });
        }
    }
}
