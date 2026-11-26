using System.Linq;
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
/// Консоль наблюдения: сверху — выбор отряда (как в RMC/CMU), слева — его бойцы (состояние, здоровье, где находятся),
/// справа — вид глазами выбранного, снизу — приказ выбранному отряду или всей стороне (только командная консоль).
/// </summary>
public sealed class DHOverwatchWindow : DefaultWindow
{
    public event Action<NetEntity?>? OnWatch;
    public event Action<string, string?>? OnOrder;
    public event Action? OnTacticalMap;

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

    /// <summary>Выбор отряда: «Вся сторона» (null), отряды, «Вне отрядов» (<see cref="DHOverwatchOrderMessage.NoSquad"/>).</summary>
    private readonly OptionButton _squad = new() { MinWidth = 180 };
    private readonly List<string?> _squadIds = new();
    private string _squadKey = string.Empty;
    private string? _selectedSquad;
    private bool _squadChosen;

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
        _squad.OnItemSelected += args =>
        {
            _squad.SelectId(args.Id);
            _selectedSquad = args.Id < _squadIds.Count ? _squadIds[args.Id] : null;
            _squadChosen = true;
            Rebuild();
        };
        left.AddChild(_squad);
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
        var tacmap = new Button { Text = Loc.GetString("dh-overwatch-ui-tacmap") };
        tacmap.OnPressed += _ => OnTacticalMap?.Invoke();
        viewHeader.AddChild(tacmap);
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

        OnOrder?.Invoke(text, _selectedSquad);
        _order.Text = string.Empty;
    }

    /// <summary>Отряд по умолчанию (свой отряд командира, открывшего планшет).</summary>
    public string? DefaultSquad;

    public void UpdateState(DHOverwatchState state, NetEntity? watching, IEye? eye)
    {
        _state = state;
        _watching = watching;
        UpdateSquads(state);
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

    private void UpdateSquads(DHOverwatchState state)
    {
        _squad.Visible = !state.LAV;
        if (state.LAV)
        {
            _selectedSquad = null;
            return;
        }

        var key = string.Join(",", state.Squads.Select(q => q.Id)) + "|" + state.FactionOrders;
        if (key == _squadKey)
            return;

        _squadKey = key;
        _squad.Clear();
        _squadIds.Clear();
        if (state.FactionOrders)
        {
            _squad.AddItem(Loc.GetString("dh-overwatch-squad-all"), _squadIds.Count);
            _squadIds.Add(null);
        }

        foreach (var squad in state.Squads)
        {
            _squad.AddItem(squad.Name, _squadIds.Count);
            _squadIds.Add(squad.Id);
        }

        _squad.AddItem(Loc.GetString("dh-overwatch-squad-none"), _squadIds.Count);
        _squadIds.Add(DHOverwatchOrderMessage.NoSquad);

        // Командная консоль — вся сторона; планшет командира отряда — его отряд.
        if (!_squadChosen || !_squadIds.Contains(_selectedSquad))
        {
            _selectedSquad = DefaultSquad != null && _squadIds.Contains(DefaultSquad) && !state.FactionOrders
                ? DefaultSquad
                : _squadIds[0];
        }

        _squad.SelectId(_squadIds.IndexOf(_selectedSquad));
    }

    /// <summary>Боец в выбранном отряде (вся сторона и LA-V — все).</summary>
    private bool InSelection(DHOverwatchEntry fighter)
    {
        if (_state == null || _state.LAV || _selectedSquad == null)
            return true;

        return (fighter.Squad ?? DHOverwatchOrderMessage.NoSquad) == _selectedSquad;
    }

    private void Rebuild()
    {
        _list.RemoveAllChildren();
        if (_state == null)
            return;

        int alive = 0, crit = 0, dead = 0;
        foreach (var fighter in _state.Fighters)
        {
            if (!InSelection(fighter))
                continue;

            switch (fighter.Status)
            {
                case DHOverwatchStatus.Alive: alive++; break;
                case DHOverwatchStatus.Critical: crit++; break;
                default: dead++; break;
            }
        }

        _summary.Text = Loc.GetString("dh-overwatch-ui-summary",
            ("faction", Loc.GetString(_state.LAV ? "dh-overwatch-faction-lav"
                : _state.Faction == DHAssaultFaction.Republic ? "dh-overwatch-faction-republic"
                : "dh-overwatch-faction-empire")),
            ("alive", alive), ("crit", crit), ("dead", dead));

        if (alive + crit + dead == 0)
        {
            _list.AddChild(new Label { Text = Loc.GetString("dh-overwatch-ui-empty"), FontColorOverride = Dim });
            return;
        }

        foreach (var fighter in _state.Fighters)
        {
            if (!InSelection(fighter) || _hideDead.Pressed && fighter.Status == DHOverwatchStatus.Dead)
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
