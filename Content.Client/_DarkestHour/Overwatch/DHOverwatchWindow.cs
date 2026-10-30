using Content.Client._DarkestHour.UI;
using System.Numerics;
using Content.Client.Viewport;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Overwatch;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Graphics;

namespace Content.Client._DarkestHour.Overwatch;

/// <summary>
/// Консоль наблюдения: слева — бойцы стороны (состояние, здоровье, где находятся), справа — вид глазами выбранного,
/// снизу — приказ всей стороне.
/// </summary>
public sealed class DHOverwatchWindow : DefaultWindow
{
    public event Action<NetEntity?>? OnWatch;
    public event Action<string>? OnOrder;

    private static readonly Color Ok = Color.FromHex("#8FD66B");
    private static readonly Color Warn = Color.FromHex("#E8B04A");
    private static readonly Color Bad = Color.FromHex("#E05050");
    private static readonly Color Dim = Color.FromHex("#8A8A8A");

    private readonly FixedEye _defaultEye = new();

    private readonly Label _summary = new() { HorizontalExpand = true };
    private readonly CheckBox _hideDead = new() { Text = "" };
    private readonly BoxContainer _list = new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        SeparationOverride = 2,
    };

    private readonly Label _viewTitle = new() { HorizontalExpand = true };
    private readonly ScalingViewport _view = new()
    {
        MinSize = new Vector2(420, 420),
        HorizontalExpand = true,
        VerticalExpand = true,
        MouseFilter = MouseFilterMode.Ignore,
        ViewportSize = new Vector2i(500, 500),
        StretchMode = ScalingViewportStretchMode.Bilinear,
    };

    private readonly Label _noSignal = new()
    {
        HorizontalAlignment = HAlignment.Center,
        VerticalAlignment = VAlignment.Center,
    };

    private readonly LineEdit _order = new() { HorizontalExpand = true };

    private DHOverwatchState? _state;
    private NetEntity? _watching;

    public DHOverwatchWindow()
    {
        DHFonts.RegisterTerminal(this); // DarkestHour: терминальный шрифт
        Title = Loc.GetString("dh-overwatch-ui-title");
        MinSize = new Vector2(860, 560);
        SetSize = new Vector2(960, 620);

        _hideDead.Text = Loc.GetString("dh-overwatch-ui-hide-dead");
        _hideDead.OnToggled += _ => Rebuild();
        _noSignal.Text = Loc.GetString("dh-overwatch-ui-no-signal");
        _order.PlaceHolder = Loc.GetString("dh-overwatch-ui-order-placeholder");
        _view.Eye = _defaultEye;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            VerticalExpand = true,
        };

        // Левая колонка: бойцы.
        var left = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            MinWidth = 360,
        };
        left.AddChild(_summary);
        left.AddChild(_hideDead);
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_list);
        left.AddChild(scroll);
        body.AddChild(left);

        // Правая колонка: камера.
        var right = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
        };
        var viewHeader = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        viewHeader.AddChild(_viewTitle);
        var disconnect = new Button { Text = Loc.GetString("dh-overwatch-ui-disconnect") };
        disconnect.OnPressed += _ => OnWatch?.Invoke(null);
        viewHeader.AddChild(disconnect);
        right.AddChild(viewHeader);

        var viewPanel = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#0b0d0f") },
        };
        viewPanel.AddChild(_noSignal);
        viewPanel.AddChild(_view);
        right.AddChild(viewPanel);
        body.AddChild(right);

        root.AddChild(body);

        // Низ: приказ стороне.
        var orderRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        orderRow.AddChild(new Label { Text = Loc.GetString("dh-overwatch-ui-order") });
        orderRow.AddChild(_order);
        var send = new Button { Text = Loc.GetString("dh-overwatch-ui-order-send") };
        send.OnPressed += _ => SendOrder();
        _order.OnTextEntered += _ => SendOrder();
        orderRow.AddChild(send);
        root.AddChild(orderRow);

        Contents.AddChild(root);
        SetView(null, null);
    }

    private void SendOrder()
    {
        var text = _order.Text.Trim();
        if (text.Length == 0)
            return;

        OnOrder?.Invoke(text);
        _order.Text = string.Empty;
    }

    public void UpdateState(DHOverwatchState state, NetEntity? watching, IEye? eye)
    {
        _state = state;
        _watching = watching;
        Rebuild();

        DHOverwatchEntry? entry = null;
        if (watching != null)
            entry = state.Fighters.Find(f => f.Entity == watching);

        SetView(entry, eye);
    }

    private void SetView(DHOverwatchEntry? entry, IEye? eye)
    {
        _view.Eye = eye ?? _defaultEye;
        _view.Visible = eye != null;
        _noSignal.Visible = eye == null;
        _noSignal.Text = Loc.GetString(entry == null ? "dh-overwatch-ui-no-signal" : "dh-overwatch-ui-connecting");

        _viewTitle.Text = entry == null
            ? Loc.GetString("dh-overwatch-ui-view-none")
            : Loc.GetString("dh-overwatch-ui-view", ("name", entry.Name), ("job", entry.Job));
    }

    private void Rebuild()
    {
        _list.RemoveAllChildren();
        if (_state == null)
            return;

        int alive = 0, crit = 0, dead = 0;
        foreach (var fighter in _state.Fighters)
        {
            switch (fighter.Status)
            {
                case DHOverwatchStatus.Alive: alive++; break;
                case DHOverwatchStatus.Critical: crit++; break;
                default: dead++; break;
            }
        }

        _summary.Text = Loc.GetString("dh-overwatch-ui-summary",
            ("faction", Loc.GetString(_state.Faction == DHAssaultFaction.Republic
                ? "dh-overwatch-faction-republic"
                : "dh-overwatch-faction-empire")),
            ("alive", alive), ("crit", crit), ("dead", dead));

        if (_state.Fighters.Count == 0)
        {
            _list.AddChild(new Label { Text = Loc.GetString("dh-overwatch-ui-empty"), FontColorOverride = Dim });
            return;
        }

        foreach (var fighter in _state.Fighters)
        {
            if (_hideDead.Pressed && fighter.Status == DHOverwatchStatus.Dead)
                continue;

            _list.AddChild(Row(fighter));
        }
    }

    private Control Row(DHOverwatchEntry fighter)
    {
        var (statusText, color) = fighter.Status switch
        {
            DHOverwatchStatus.Critical => (Loc.GetString("dh-overwatch-status-critical"), Warn),
            DHOverwatchStatus.Dead => (Loc.GetString("dh-overwatch-status-dead"), Bad),
            _ => (Loc.GetString("dh-overwatch-status-alive", ("health", fighter.Health)),
                fighter.Health >= 50 ? Ok : Warn),
        };

        var button = new ContainerButton
        {
            StyleClasses = { ContainerButton.StyleClassButton },
            HorizontalExpand = true,
            ToggleMode = true,
            Pressed = fighter.Entity == _watching,
            Disabled = fighter.Status == DHOverwatchStatus.Dead,
        };

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6, 2),
        };
        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        top.AddChild(new Label { Text = fighter.Name, HorizontalExpand = true, ClipText = true });
        top.AddChild(new Label { Text = statusText, FontColorOverride = color });
        content.AddChild(top);

        var detail = Loc.GetString("dh-overwatch-ui-row-detail", ("job", fighter.Job), ("location", fighter.Location),
            ("x", fighter.X), ("y", fighter.Y));
        if (fighter.Disconnected)
            detail += " " + Loc.GetString("dh-overwatch-ui-ssd");

        content.AddChild(new Label { Text = detail, FontColorOverride = Dim, ClipText = true });
        button.AddChild(content);

        var target = fighter.Entity;
        button.OnPressed += _ => OnWatch?.Invoke(target == _watching ? null : target);
        return button;
    }
}
