using System.Numerics;
using Content.Client._DarkestHour.City;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Uprising;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Uprising;

// «Восстание», задания (решение автора 2026-11-12): компьютер заданий стороны, бомба с кодом, база данных военпола.

// ---------------------------------------------------------------- компьютер заданий

[UsedImplicitly]
public sealed class DHTaskConsoleBoundUserInterface : BoundUserInterface
{
    private DHTaskConsoleWindow? _window;

    public DHTaskConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DHTaskConsoleWindow>();
        _window.OnAccept += index => SendMessage(new DHTaskAcceptMessage(index));
        _window.OnAbandon += () => SendMessage(new DHTaskAbandonMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is DHTaskConsoleState console)
            _window?.UpdateState(console);
    }
}

public sealed class DHTaskConsoleWindow : DefaultWindow
{
    public event Action<int>? OnAccept;
    public event Action? OnAbandon;

    private readonly Label _balance = new() { FontColorOverride = DHPhoneUi.Money };
    private readonly BoxContainer _active = DHPhoneUi.Column(4);
    private readonly BoxContainer _offers = DHPhoneUi.Column(6);
    private readonly BoxContainer _log = DHPhoneUi.Column(2);

    public DHTaskConsoleWindow()
    {
        DHFonts.RegisterTerminal(this);
        MinSize = new Vector2(460, 520);

        var root = DHPhoneUi.Column(8);
        root.Margin = new Thickness(8);
        root.AddChild(_balance);
        root.AddChild(_active);
        root.AddChild(_offers);
        root.AddChild(new Label { Text = Loc.GetString("dh-task-ui-log"), FontColorOverride = DHPhoneUi.Dim });
        root.AddChild(_log);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(root);
        Contents.AddChild(scroll);
    }

    private static string Time(int seconds)
    {
        return $"{seconds / 60}:{seconds % 60:D2}";
    }

    public void UpdateState(DHTaskConsoleState state)
    {
        Title = Loc.GetString("dh-task-ui-title-" + state.Side.ToString().ToLowerInvariant());
        _balance.Text = Loc.GetString("dh-task-ui-balance", ("balance", state.Balance));

        _active.RemoveAllChildren();
        _offers.RemoveAllChildren();
        if (state.ActiveName is { } name)
        {
            _active.AddChild(new Label { Text = Loc.GetString("dh-task-ui-active"), FontColorOverride = DHPhoneUi.Dim });
            _active.AddChild(new Label { Text = name, FontColorOverride = DHPhoneUi.Accent });
            _active.AddChild(new RichTextLabel { Text = state.ActiveText, HorizontalExpand = true });
            _active.AddChild(new Label
            {
                Text = Loc.GetString("dh-task-ui-reward-time", ("reward", state.ActiveReward), ("time", Time(state.SecondsLeft))),
                FontColorOverride = DHPhoneUi.Money,
            });
            var abandon = new Button { Text = Loc.GetString("dh-task-ui-abandon") };
            abandon.OnPressed += _ => OnAbandon?.Invoke();
            _active.AddChild(abandon);
        }
        else if (state.Offers.Count == 0)
        {
            _offers.AddChild(new Label
            {
                Text = Loc.GetString("dh-task-ui-cooldown", ("time", Time(state.Cooldown))),
                FontColorOverride = DHPhoneUi.Dim,
            });
        }
        else
        {
            _offers.AddChild(new Label { Text = Loc.GetString("dh-task-ui-offers"), FontColorOverride = DHPhoneUi.Dim });
            for (var i = 0; i < state.Offers.Count; i++)
            {
                var offer = state.Offers[i];
                var box = DHPhoneUi.Column(2);
                box.AddChild(new Label { Text = offer.Name, FontColorOverride = DHPhoneUi.Accent });
                box.AddChild(new RichTextLabel { Text = offer.Description, HorizontalExpand = true });
                var row = DHPhoneUi.Row();
                row.AddChild(new Label
                {
                    Text = Loc.GetString("dh-task-ui-reward", ("reward", offer.Reward)),
                    FontColorOverride = DHPhoneUi.Money,
                    HorizontalExpand = true,
                });
                var index = i;
                var accept = new Button { Text = Loc.GetString("dh-task-ui-accept") };
                accept.OnPressed += _ => OnAccept?.Invoke(index);
                row.AddChild(accept);
                box.AddChild(row);
                _offers.AddChild(box);
            }
        }

        _log.RemoveAllChildren();
        foreach (var line in state.Log)
        {
            _log.AddChild(new Label { Text = line, FontColorOverride = DHPhoneUi.Dim, ClipText = true });
        }
    }
}

// ---------------------------------------------------------------- бомба

[UsedImplicitly]
public sealed class DHTaskBombBoundUserInterface : BoundUserInterface
{
    private DHTaskBombWindow? _window;

    public DHTaskBombBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DHTaskBombWindow>();
        _window.OnCode += code => SendMessage(new DHTaskBombCodeMessage(code));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is DHTaskBombState bomb)
            _window?.UpdateState(bomb);
    }
}

public sealed class DHTaskBombWindow : DefaultWindow
{
    public event Action<string>? OnCode;

    private readonly Label _timer = new() { FontColorOverride = DHPhoneUi.Warn };
    private readonly LineEdit _code = new() { HorizontalExpand = true };
    private readonly Button _enter = new();
    private readonly Label _status = new() { FontColorOverride = DHPhoneUi.Dim };

    public DHTaskBombWindow()
    {
        DHFonts.RegisterTerminal(this);
        Title = Loc.GetString("dh-task-bomb-ui-title");
        MinSize = new Vector2(300, 150);

        var root = DHPhoneUi.Column(6);
        root.Margin = new Thickness(8);
        root.AddChild(_timer);
        _code.PlaceHolder = Loc.GetString("dh-task-bomb-ui-code");
        _code.OnTextEntered += _ => Send();
        _enter.Text = Loc.GetString("dh-task-bomb-ui-enter");
        _enter.OnPressed += _ => Send();
        var row = DHPhoneUi.Row();
        row.AddChild(_code);
        row.AddChild(_enter);
        root.AddChild(row);
        root.AddChild(_status);
        Contents.AddChild(root);
    }

    private void Send()
    {
        OnCode?.Invoke(_code.Text);
        _code.Clear();
    }

    public void UpdateState(DHTaskBombState state)
    {
        _timer.Text = state.Defused
            ? Loc.GetString("dh-task-bomb-ui-defused")
            : Loc.GetString("dh-task-bomb-ui-timer", ("time", $"{state.SecondsLeft / 60}:{state.SecondsLeft % 60:D2}"));
        _enter.Disabled = state.Defused;
        if (state.Status != null)
            _status.Text = state.Status;
    }
}

// ---------------------------------------------------------------- база данных военпола

[UsedImplicitly]
public sealed class DHPoliceDatabaseBoundUserInterface : BoundUserInterface
{
    private DHPoliceDatabaseWindow? _window;

    public DHPoliceDatabaseBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DHPoliceDatabaseWindow>();
        _window.OnDelete += id => SendMessage(new DHPoliceDatabaseDeleteMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is DHPoliceDatabaseState db)
            _window?.UpdateState(db);
    }
}

public sealed class DHPoliceDatabaseWindow : DefaultWindow
{
    public event Action<int>? OnDelete;

    private readonly Label _status = new() { FontColorOverride = DHPhoneUi.Warn };
    private readonly LineEdit _filter = new() { HorizontalExpand = true };
    private readonly BoxContainer _records = DHPhoneUi.Column(2);
    private readonly BoxContainer _help = DHPhoneUi.Column(2);
    private DHPoliceDatabaseState? _state;
    private int? _confirm;

    public DHPoliceDatabaseWindow()
    {
        DHFonts.RegisterTerminal(this);
        Title = Loc.GetString("dh-police-db-ui-title");
        MinSize = new Vector2(860, 560);

        var root = DHPhoneUi.Row();
        root.Margin = new Thickness(8);

        var left = DHPhoneUi.Column(6);
        left.HorizontalExpand = true;
        _filter.PlaceHolder = Loc.GetString("dh-police-db-ui-filter");
        _filter.OnTextChanged += _ => Rebuild();
        left.AddChild(_filter);
        left.AddChild(new Label { Text = Loc.GetString("dh-police-db-ui-columns"), FontColorOverride = DHPhoneUi.Dim });
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_records);
        left.AddChild(scroll);
        left.AddChild(_status);
        root.AddChild(left);

        var right = DHPhoneUi.Column(4);
        right.MinWidth = 250;
        right.AddChild(new RichTextLabel { Text = Loc.GetString("dh-police-db-ui-rules") });
        right.AddChild(new Label { Text = Loc.GetString("dh-police-db-ui-worlds"), FontColorOverride = DHPhoneUi.Dim });
        var helpScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        helpScroll.AddChild(_help);
        right.AddChild(helpScroll);
        root.AddChild(right);

        Contents.AddChild(root);
    }

    public void UpdateState(DHPoliceDatabaseState state)
    {
        _state = state;
        _confirm = null;
        _status.Text = state.Status ?? string.Empty;
        _help.RemoveAllChildren();
        foreach (var world in state.Worlds)
        {
            _help.AddChild(new Label { Text = world, FontColorOverride = DHPhoneUi.Dim, ClipText = true });
        }

        Rebuild();
    }

    private void Rebuild()
    {
        _records.RemoveAllChildren();
        if (_state is not { } state)
            return;

        if (!state.Online)
        {
            _records.AddChild(new Label { Text = Loc.GetString("dh-police-db-offline"), FontColorOverride = DHPhoneUi.Warn });
            return;
        }

        var filter = _filter.Text.Trim();
        foreach (var record in state.Records)
        {
            if (filter.Length > 0 &&
                !record.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase) &&
                !record.Number.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            var row = DHPhoneUi.Row();
            row.AddChild(new Label
            {
                Text = $"{record.Name} | {record.Number} | {record.Sex}, {record.Species} | {record.BirthDate}, {record.BirthPlace} | {record.Issued}",
                HorizontalExpand = true,
                ClipText = true,
            });

            var id = record.Id;
            var delete = new Button
            {
                Text = Loc.GetString(_confirm == id ? "dh-police-db-ui-confirm" : "dh-police-db-ui-delete"),
            };
            delete.OnPressed += _ =>
            {
                // Двойное нажатие: настоящая запись удаляется со штрафом.
                if (_confirm == id)
                {
                    OnDelete?.Invoke(id);
                    _confirm = null;
                }
                else
                {
                    _confirm = id;
                    Rebuild();
                }
            };
            row.AddChild(delete);
            _records.AddChild(row);
        }
    }
}
