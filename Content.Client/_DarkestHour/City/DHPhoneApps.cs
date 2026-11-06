using Content.Client.UserInterface.Fragments;
using Content.Shared._DarkestHour.City;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._DarkestHour.City;

// Приложения телефона «Восстания»: «Сообщения», «Банк LA-V», «Телефон». Каждое — фрагмент окна телефона.

internal static class DHPhoneUi
{
    public static readonly Color Accent = Color.FromHex("#6FC2E8");
    public static readonly Color Money = Color.FromHex("#8FD66B");
    public static readonly Color Warn = Color.FromHex("#E8B04A");
    public static readonly Color Dim = Color.FromHex("#8A8A8A");

    public static BoxContainer Column(int separation = 4)
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = separation,
            HorizontalExpand = true,
        };
    }

    public static BoxContainer Row(int separation = 4)
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = separation,
            HorizontalExpand = true,
        };
    }

    public static ScrollContainer Scroll(Control child)
    {
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        scroll.AddChild(child);
        return scroll;
    }

    public static void Send(BoundUserInterface ui, CartridgeMessageEvent message)
    {
        ui.SendMessage(new CartridgeUiMessage(message));
    }
}

// ---------------------------------------------------------------- сообщения

public sealed partial class DHMessengerUi : UIFragment
{
    private DHMessengerFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new DHMessengerFragment();
        _fragment.OnAction += (action, number, text) =>
            DHPhoneUi.Send(userInterface, new DHMessengerUiMessageEvent(action, number, text));
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is DHMessengerUiState messenger)
            _fragment?.UpdateState(messenger);
    }
}

public sealed class DHMessengerFragment : BoxContainer
{
    public event Action<DHMessengerAction, string, string>? OnAction;

    private readonly BoxContainer _list = DHPhoneUi.Column();
    private readonly LineEdit _newNumber = new() { HorizontalExpand = true };
    private readonly BoxContainer _chatView = DHPhoneUi.Column(6);
    private readonly Label _chatTitle = new() { FontColorOverride = DHPhoneUi.Accent, HorizontalExpand = true };
    private readonly BoxContainer _messages = DHPhoneUi.Column(3);
    private readonly LineEdit _input = new() { HorizontalExpand = true };
    private readonly BoxContainer _listView = DHPhoneUi.Column(6);
    private string? _open;

    public DHMessengerFragment()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;

        // список переписок и справочник
        var newRow = DHPhoneUi.Row();
        _newNumber.PlaceHolder = Loc.GetString("dh-phone-ui-number-placeholder");
        var write = new Button { Text = Loc.GetString("dh-phone-ui-write") };
        write.OnPressed += _ => OpenNumber(_newNumber.Text);
        _newNumber.OnTextEntered += args => OpenNumber(args.Text);
        newRow.AddChild(_newNumber);
        newRow.AddChild(write);
        _listView.AddChild(newRow);
        _listView.AddChild(DHPhoneUi.Scroll(_list));
        _listView.VerticalExpand = true;
        AddChild(_listView);

        // открытая переписка
        var top = DHPhoneUi.Row();
        var back = new Button { Text = Loc.GetString("dh-phone-ui-back") };
        back.OnPressed += _ => OnAction?.Invoke(DHMessengerAction.Close, string.Empty, string.Empty);
        top.AddChild(back);
        top.AddChild(_chatTitle);
        _chatView.AddChild(top);
        _chatView.AddChild(DHPhoneUi.Scroll(_messages));
        var inputRow = DHPhoneUi.Row();
        _input.PlaceHolder = Loc.GetString("dh-phone-ui-message-placeholder");
        _input.OnTextEntered += _ => SendInput();
        var send = new Button { Text = Loc.GetString("dh-phone-ui-send") };
        send.OnPressed += _ => SendInput();
        inputRow.AddChild(_input);
        inputRow.AddChild(send);
        _chatView.AddChild(inputRow);
        _chatView.VerticalExpand = true;
        _chatView.Visible = false;
        AddChild(_chatView);
    }

    private void OpenNumber(string number)
    {
        number = number.Trim();
        if (number.Length == 0)
            return;

        _newNumber.Clear();
        OnAction?.Invoke(DHMessengerAction.Open, number, string.Empty);
    }

    private void SendInput()
    {
        if (_open == null || _input.Text.Trim().Length == 0)
            return;

        OnAction?.Invoke(DHMessengerAction.Send, _open, _input.Text);
        _input.Clear();
    }

    public void UpdateState(DHMessengerUiState state)
    {
        _open = state.Open;
        _listView.Visible = state.Open == null;
        _chatView.Visible = state.Open != null;

        _list.RemoveAllChildren();
        foreach (var contact in state.Contacts)
        {
            var number = contact.Number;
            var text = contact.Unread > 0
                ? Loc.GetString("dh-phone-ui-contact-unread", ("name", contact.Name), ("number", number), ("unread", contact.Unread))
                : Loc.GetString("dh-phone-ui-contact", ("name", contact.Name), ("number", number));
            var button = new Button { Text = text, HorizontalExpand = true, ClipText = true };
            if (contact.Unread > 0)
                button.ModulateSelfOverride = DHPhoneUi.Warn;
            button.OnPressed += _ => OnAction?.Invoke(DHMessengerAction.Open, number, string.Empty);
            _list.AddChild(button);
        }

        if (state.Open == null)
            return;

        _chatTitle.Text = state.OpenName == state.Open
            ? state.Open
            : $"{state.OpenName} ({state.Open})";
        _messages.RemoveAllChildren();
        if (state.Messages.Count == 0)
            _messages.AddChild(new Label { Text = Loc.GetString("dh-phone-ui-no-messages"), FontColorOverride = DHPhoneUi.Dim });

        foreach (var message in state.Messages)
        {
            var label = new RichTextLabel { HorizontalExpand = true };
            var who = message.Outgoing ? Loc.GetString("dh-phone-ui-me") : state.OpenName ?? state.Open;
            label.SetMessage(Robust.Shared.Utility.FormattedMessage.FromUnformatted($"[{message.Time}] {who}: {message.Text}"),
                defaultColor: message.Outgoing ? DHPhoneUi.Dim : DHPhoneUi.Accent);
            _messages.AddChild(label);
        }
    }
}

// ---------------------------------------------------------------- банк LA-V

public sealed partial class DHBankAppUi : UIFragment
{
    private DHBankAppFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new DHBankAppFragment();
        _fragment.OnAction += (action, first, second, amount) =>
            DHPhoneUi.Send(userInterface, new DHBankAppUiMessageEvent(action, first, second, amount));
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is DHBankAppUiState bank)
            _fragment?.UpdateState(bank);
    }
}

public sealed class DHBankAppFragment : BoxContainer
{
    public event Action<DHBankAppAction, string, string, int>? OnAction;

    private readonly BoxContainer _loginView = DHPhoneUi.Column(6);
    private readonly LineEdit _login = new() { HorizontalExpand = true };
    private readonly LineEdit _password = new() { HorizontalExpand = true };

    private readonly BoxContainer _accountView = DHPhoneUi.Column(6);
    private readonly Label _holder = new() { FontColorOverride = DHPhoneUi.Accent };
    private readonly Label _number = new() { FontColorOverride = DHPhoneUi.Dim };
    private readonly Label _balance = new() { FontColorOverride = DHPhoneUi.Money };
    private readonly LineEdit _to = new() { HorizontalExpand = true };
    private readonly LineEdit _amount = new() { HorizontalExpand = true };
    private readonly BoxContainer _log = DHPhoneUi.Column(2);
    private readonly Label _status = new() { FontColorOverride = DHPhoneUi.Warn };

    public DHBankAppFragment()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;

        _loginView.AddChild(new Label { Text = Loc.GetString("dh-bank-app-ui-welcome"), FontColorOverride = DHPhoneUi.Accent });
        _login.PlaceHolder = Loc.GetString("dh-bank-app-ui-login");
        _password.PlaceHolder = Loc.GetString("dh-bank-app-ui-password");
        _password.OnTextEntered += _ => Login();
        _loginView.AddChild(_login);
        _loginView.AddChild(_password);
        var enter = new Button { Text = Loc.GetString("dh-bank-app-ui-enter") };
        enter.OnPressed += _ => Login();
        _loginView.AddChild(enter);
        _loginView.AddChild(new Label { Text = Loc.GetString("dh-bank-app-ui-hint"), FontColorOverride = DHPhoneUi.Dim });
        AddChild(_loginView);

        var top = DHPhoneUi.Row();
        _holder.HorizontalExpand = true;
        top.AddChild(_holder);
        var logout = new Button { Text = Loc.GetString("dh-bank-app-ui-logout") };
        logout.OnPressed += _ => OnAction?.Invoke(DHBankAppAction.Logout, string.Empty, string.Empty, 0);
        top.AddChild(logout);
        _accountView.AddChild(top);
        _accountView.AddChild(_number);
        _accountView.AddChild(_balance);
        _accountView.AddChild(new Label { Text = Loc.GetString("dh-bank-ui-transfer"), FontColorOverride = DHPhoneUi.Accent });
        _to.PlaceHolder = Loc.GetString("dh-bank-ui-to");
        _amount.PlaceHolder = Loc.GetString("dh-bank-ui-amount");
        _accountView.AddChild(_to);
        var transferRow = DHPhoneUi.Row();
        transferRow.AddChild(_amount);
        var send = new Button { Text = Loc.GetString("dh-bank-ui-send") };
        send.OnPressed += _ => Transfer();
        transferRow.AddChild(send);
        _accountView.AddChild(transferRow);
        _accountView.AddChild(new Label { Text = Loc.GetString("dh-bank-ui-history"), FontColorOverride = DHPhoneUi.Accent });
        _accountView.AddChild(DHPhoneUi.Scroll(_log));
        _accountView.VerticalExpand = true;
        _accountView.Visible = false;
        AddChild(_accountView);

        AddChild(_status);
    }

    private void Login()
    {
        OnAction?.Invoke(DHBankAppAction.Login, _login.Text, _password.Text, 0);
        _password.Clear();
    }

    private void Transfer()
    {
        if (!int.TryParse(_amount.Text.Trim(), out var amount) || amount <= 0)
            return;

        OnAction?.Invoke(DHBankAppAction.Transfer, _to.Text, string.Empty, amount);
        _amount.Clear();
    }

    public void UpdateState(DHBankAppUiState state)
    {
        _loginView.Visible = !state.LoggedIn;
        _accountView.Visible = state.LoggedIn;
        _status.Text = state.Status ?? string.Empty;
        _status.Visible = state.Status != null;

        if (!state.LoggedIn)
            return;

        _holder.Text = state.Holder;
        _number.Text = Loc.GetString("dh-bank-ui-number", ("number", state.Number));
        _balance.Text = Loc.GetString("dh-bank-ui-balance", ("balance", state.Balance));
        _log.RemoveAllChildren();
        foreach (var line in state.Log)
        {
            _log.AddChild(new Label { Text = line, FontColorOverride = DHPhoneUi.Dim, ClipText = true });
        }
    }
}

// ---------------------------------------------------------------- звонки

public sealed partial class DHCallUi : UIFragment
{
    private DHCallFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new DHCallFragment();
        _fragment.OnAction += (action, number) =>
            DHPhoneUi.Send(userInterface, new DHCallUiMessageEvent(action, number));
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is DHCallUiState call)
            _fragment?.UpdateState(call);
    }
}

public sealed class DHCallFragment : BoxContainer
{
    public event Action<DHCallAction, string>? OnAction;

    private readonly Label _status = new();
    private readonly Button _answer = new() { HorizontalExpand = true };
    private readonly Button _hangUp = new() { HorizontalExpand = true };
    private readonly LineEdit _number = new() { HorizontalExpand = true };
    private readonly BoxContainer _book = DHPhoneUi.Column(3);

    public DHCallFragment()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        SeparationOverride = 6;

        AddChild(_status);
        var buttons = DHPhoneUi.Row(6);
        _answer.Text = Loc.GetString("dh-phone-ui-answer");
        _answer.OnPressed += _ => OnAction?.Invoke(DHCallAction.Answer, string.Empty);
        _hangUp.Text = Loc.GetString("dh-phone-ui-hang-up");
        _hangUp.OnPressed += _ => OnAction?.Invoke(DHCallAction.HangUp, string.Empty);
        buttons.AddChild(_answer);
        buttons.AddChild(_hangUp);
        AddChild(buttons);

        var dial = DHPhoneUi.Row();
        _number.PlaceHolder = Loc.GetString("dh-phone-ui-number-placeholder");
        _number.OnTextEntered += args => Dial(args.Text);
        var call = new Button { Text = Loc.GetString("dh-phone-ui-call") };
        call.OnPressed += _ => Dial(_number.Text);
        dial.AddChild(_number);
        dial.AddChild(call);
        AddChild(dial);

        AddChild(new Label { Text = Loc.GetString("dh-phone-ui-book"), FontColorOverride = DHPhoneUi.Warn });
        AddChild(DHPhoneUi.Scroll(_book));
    }

    private void Dial(string number)
    {
        number = number.Trim();
        if (number.Length == 0)
            return;

        _number.Clear();
        OnAction?.Invoke(DHCallAction.Call, number);
    }

    public void UpdateState(DHCallUiState state)
    {
        var other = state.Other ?? "—";
        _status.Text = state.Status switch
        {
            DHCallStatus.Calling => Loc.GetString("dh-phone-ui-calling", ("other", other)),
            DHCallStatus.Ringing => Loc.GetString("dh-phone-ui-ringing", ("other", other)),
            DHCallStatus.InCall => Loc.GetString("dh-phone-ui-in-call", ("other", other)),
            _ => Loc.GetString("dh-phone-ui-idle"),
        };
        _status.FontColorOverride = state.Status switch
        {
            DHCallStatus.InCall => DHPhoneUi.Money,
            DHCallStatus.Idle => DHPhoneUi.Dim,
            _ => DHPhoneUi.Warn,
        };
        _answer.Disabled = state.Status != DHCallStatus.Ringing;
        _hangUp.Disabled = state.Status == DHCallStatus.Idle;

        _book.RemoveAllChildren();
        foreach (var contact in state.Contacts)
        {
            var number = contact.Number;
            var button = new Button
            {
                Text = Loc.GetString("dh-phone-ui-contact", ("name", contact.Name), ("number", number)),
                HorizontalExpand = true,
                ClipText = true,
                Disabled = contact.Busy || state.Status != DHCallStatus.Idle,
            };
            button.OnPressed += _ => OnAction?.Invoke(DHCallAction.Call, number);
            _book.AddChild(button);
        }
    }
}

// ---------------------------------------------------------------- погода

public sealed partial class DHWeatherAppUi : UIFragment
{
    private DHWeatherAppFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new DHWeatherAppFragment();
        _fragment.OnRefresh += () => DHPhoneUi.Send(userInterface, new DHWeatherAppRefreshMessage());
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is DHWeatherAppUiState weather)
            _fragment?.UpdateState(weather);
    }
}

public sealed class DHWeatherAppFragment : BoxContainer
{
    public event Action? OnRefresh;

    private static readonly Color Warm = Color.FromHex("#E8B04A");
    private static readonly Color Cold = Color.FromHex("#6FC2E8");

    private readonly Label _now = new() { FontColorOverride = DHPhoneUi.Accent };
    private readonly Label _nowTemp = new();
    private readonly BoxContainer _list = DHPhoneUi.Column(3);
    private readonly Label _updated = new() { FontColorOverride = DHPhoneUi.Dim };

    public DHWeatherAppFragment()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        SeparationOverride = 6;

        AddChild(new Label { Text = Loc.GetString("dh-weather-app-place"), FontColorOverride = DHPhoneUi.Dim });
        var now = DHPhoneUi.Row(8);
        _now.HorizontalExpand = true;
        now.AddChild(_now);
        now.AddChild(_nowTemp);
        AddChild(now);
        AddChild(new Label { Text = Loc.GetString("dh-weather-app-forecast"), FontColorOverride = DHPhoneUi.Accent });
        AddChild(DHPhoneUi.Scroll(_list));

        var bottom = DHPhoneUi.Row();
        _updated.HorizontalExpand = true;
        bottom.AddChild(_updated);
        var refresh = new Button { Text = Loc.GetString("dh-weather-app-refresh") };
        refresh.OnPressed += _ => OnRefresh?.Invoke();
        bottom.AddChild(refresh);
        AddChild(bottom);
    }

    private static Color TempColor(int temperature)
    {
        return temperature >= 15 ? Warm : Cold;
    }

    private static string TempText(int temperature)
    {
        return Loc.GetString("dh-weather-app-temperature", ("value", temperature));
    }

    public void UpdateState(DHWeatherAppUiState state)
    {
        _list.RemoveAllChildren();
        if (state.Updated.Length == 0 || state.Lines.Count == 0)
        {
            _now.Text = Loc.GetString("dh-weather-app-offline");
            _nowTemp.Text = string.Empty;
            _updated.Text = string.Empty;
            return;
        }

        var current = state.Lines[0];
        _now.Text = current.Condition;
        _nowTemp.Text = TempText(current.Temperature);
        _nowTemp.FontColorOverride = TempColor(current.Temperature);
        _updated.Text = Loc.GetString("dh-weather-app-updated", ("time", state.Updated));

        foreach (var line in state.Lines)
        {
            var row = DHPhoneUi.Row(8);
            row.AddChild(new Label
            {
                Text = line.Time,
                MinWidth = 120,
                FontColorOverride = line.Current ? DHPhoneUi.Accent : DHPhoneUi.Dim,
            });
            row.AddChild(new Label { Text = line.Condition, HorizontalExpand = true, ClipText = true });
            row.AddChild(new Label { Text = TempText(line.Temperature), FontColorOverride = TempColor(line.Temperature) });
            _list.AddChild(row);
        }
    }
}
