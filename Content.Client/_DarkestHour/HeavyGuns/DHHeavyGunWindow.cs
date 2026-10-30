using Content.Client._DarkestHour.UI;
using System.Globalization;
using System.Numerics;
using Content.Shared._DarkestHour.HeavyGuns;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.HeavyGuns;

/// <summary>
/// Консоль наведения тяжёлых орудий: список орудий своего грида (выбор флажками), цель — корабль на орбите (ПКО)
/// или точка на поверхности тела (артиллерия), ПВО своего грида, кнопка залпа.
/// </summary>
public sealed class DHHeavyGunWindow : DefaultWindow
{
    public event Action<string>? OnSelectBody;
    public event Action<List<NetEntity>, NetEntity?, string?, Vector2>? OnFire;

    private static readonly Color Ok = Color.FromHex("#8FD66B");
    private static readonly Color Warn = Color.FromHex("#E8B04A");
    private static readonly Color Bad = Color.FromHex("#E05050");

    private readonly Label _status = new() { HorizontalExpand = true };
    private readonly Label _seat = new() { HorizontalExpand = true };
    private readonly BoxContainer _guns = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _targets = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _artillery = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
    private readonly OptionButton _body = new() { HorizontalExpand = true };
    private readonly LineEdit _x = new() { Text = "0", MinWidth = 80 };
    private readonly LineEdit _y = new() { Text = "0", MinWidth = 80 };
    private readonly Label _zone = new();
    private readonly BoxContainer _marks = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
    private readonly BoxContainer _defenses = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly Button _fire = new() { HorizontalExpand = true, Text = "" };

    private readonly HashSet<NetEntity> _selectedGuns = new();
    private readonly List<string> _bodyIds = new();
    private NetEntity? _selectedTarget;
    private DHHeavyGunConsoleState? _state;

    public DHHeavyGunWindow()
    {
        DHFonts.RegisterTerminal(this); // DarkestHour: терминальный шрифт
        Title = Loc.GetString("dh-heavy-gun-ui-title");
        MinSize = new Vector2(460, 520);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        root.AddChild(_status);
        root.AddChild(_seat);

        root.AddChild(Header("dh-heavy-gun-ui-guns"));
        root.AddChild(_guns);

        root.AddChild(Header("dh-heavy-gun-ui-target"));
        root.AddChild(_targets);

        _body.OnItemSelected += args =>
        {
            _body.SelectId(args.Id);
            if (args.Id >= 0 && args.Id < _bodyIds.Count)
                OnSelectBody?.Invoke(_bodyIds[args.Id]);
        };
        _artillery.AddChild(_body);

        var coords = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        coords.AddChild(new Label { Text = "X" });
        coords.AddChild(Nudge(_x, -50));
        coords.AddChild(Nudge(_x, -10));
        coords.AddChild(_x);
        coords.AddChild(Nudge(_x, 10));
        coords.AddChild(Nudge(_x, 50));
        _artillery.AddChild(coords);

        var coordsY = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        coordsY.AddChild(new Label { Text = "Y" });
        coordsY.AddChild(Nudge(_y, -50));
        coordsY.AddChild(Nudge(_y, -10));
        coordsY.AddChild(_y);
        coordsY.AddChild(Nudge(_y, 10));
        coordsY.AddChild(Nudge(_y, 50));
        _artillery.AddChild(coordsY);
        _artillery.AddChild(_zone);
        _artillery.AddChild(new Label { Text = Loc.GetString("dh-heavy-gun-ui-marks"), FontColorOverride = Warn });
        _artillery.AddChild(_marks);
        root.AddChild(_artillery);

        root.AddChild(Header("dh-heavy-gun-ui-defenses"));
        root.AddChild(_defenses);

        _fire.Text = Loc.GetString("dh-heavy-gun-ui-fire");
        _fire.OnPressed += _ => Fire();
        root.AddChild(_fire);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(root);
        Contents.AddChild(scroll);
    }

    private static Label Header(string loc)
    {
        return new Label { Text = Loc.GetString(loc), FontColorOverride = Warn };
    }

    private Button Nudge(LineEdit edit, int delta)
    {
        var button = new Button { Text = delta > 0 ? $"+{delta}" : delta.ToString(), MinWidth = 40 };
        button.OnPressed += _ =>
        {
            var value = ParseOr(edit.Text, 0f) + delta;
            var zone = _state?.TargetZone ?? 1500f;
            edit.Text = Math.Clamp(value, -zone, zone).ToString("0", CultureInfo.InvariantCulture);
        };
        return button;
    }

    private static float ParseOr(string text, float fallback)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private void Fire()
    {
        if (_state == null)
            return;

        var guns = new List<NetEntity>(_selectedGuns);
        var position = new Vector2(ParseOr(_x.Text, 0f), ParseOr(_y.Text, 0f));
        OnFire?.Invoke(guns, _selectedTarget, _state.SelectedBody, position);
    }

    public void UpdateState(DHHeavyGunConsoleState state)
    {
        _state = state;

        _status.Text = state.Status;
        _seat.Text = Loc.GetString(state.Seated ? "dh-heavy-gun-ui-seated" : "dh-heavy-gun-ui-not-seated");
        _seat.FontColorOverride = state.Seated ? Ok : Bad;

        // Орудия
        _guns.RemoveAllChildren();
        var known = new HashSet<NetEntity>();
        foreach (var gun in state.Guns)
        {
            known.Add(gun.Gun);
            if (!gun.Ready)
                _selectedGuns.Remove(gun.Gun);

            var box = new CheckBox
            {
                Text = Loc.GetString("dh-heavy-gun-ui-gun", ("name", gun.Name), ("caliber", gun.Caliber), ("status", gun.Status)),
                Pressed = _selectedGuns.Contains(gun.Gun),
                Disabled = !gun.Ready,
            };
            var net = gun.Gun;
            box.OnToggled += args =>
            {
                if (args.Pressed)
                    _selectedGuns.Add(net);
                else
                    _selectedGuns.Remove(net);
            };
            _guns.AddChild(box);
        }

        _selectedGuns.RemoveWhere(g => !known.Contains(g));
        if (state.Guns.Count == 0)
            _guns.AddChild(new Label { Text = Loc.GetString("dh-heavy-gun-ui-no-guns"), FontColorOverride = Bad });

        // Цели
        _targets.RemoveAllChildren();
        _artillery.Visible = state.Role == DHGunRole.Artillery;
        if (state.Role == DHGunRole.AntiOrbital)
        {
            if (_selectedTarget != null && state.Targets.Find(t => t.Target == _selectedTarget) == null)
                _selectedTarget = null;

            foreach (var target in state.Targets)
            {
                var button = new Button
                {
                    Text = Loc.GetString("dh-heavy-gun-ui-target-entry", ("name", target.Name),
                        ("altitude", (int) target.Altitude)),
                    ToggleMode = true,
                    Pressed = target.Target == _selectedTarget,
                    HorizontalExpand = true,
                };
                var net = target.Target;
                button.OnPressed += _ =>
                {
                    _selectedTarget = net;
                    if (_state != null)
                        UpdateState(_state);
                };
                _targets.AddChild(button);
            }
        }
        else
        {
            _bodyIds.Clear();
            _body.Clear();
            for (var i = 0; i < state.Bodies.Count; i++)
            {
                var body = state.Bodies[i];
                _bodyIds.Add(body.Id);
                _body.AddItem(Loc.GetString("dh-heavy-gun-ui-body-entry", ("name", body.Name), ("altitude", (int) body.Altitude)), i);
                if (body.Id == state.SelectedBody)
                    _body.SelectId(i);
            }

            _zone.Text = Loc.GetString("dh-heavy-gun-ui-zone", ("zone", (int) state.TargetZone));

            // Метки ЛЦУ: нажатие — тело и координаты метки в прицел.
            _marks.RemoveAllChildren();
            foreach (var mark in state.Marks)
            {
                var bodyName = state.Bodies.Find(b => b.Id == mark.Body)?.Name ?? mark.Body ?? string.Empty;
                var button = new Button
                {
                    Text = Loc.GetString("dh-heavy-gun-ui-mark", ("code", mark.Code), ("body", bodyName),
                        ("x", (int) MathF.Round(mark.Position.X)), ("y", (int) MathF.Round(mark.Position.Y))),
                    HorizontalExpand = true,
                };
                var markBody = mark.Body;
                var position = mark.Position;
                button.OnPressed += _ =>
                {
                    _x.Text = MathF.Round(position.X).ToString("0", CultureInfo.InvariantCulture);
                    _y.Text = MathF.Round(position.Y).ToString("0", CultureInfo.InvariantCulture);
                    if (markBody != null && markBody != _state?.SelectedBody)
                        OnSelectBody?.Invoke(markBody);
                };
                _marks.AddChild(button);
            }

            if (state.Marks.Count == 0)
                _marks.AddChild(new Label { Text = Loc.GetString("dh-heavy-gun-ui-no-marks") });
        }

        // ПВО
        _defenses.RemoveAllChildren();
        foreach (var defense in state.Defenses)
        {
            _defenses.AddChild(new Label
            {
                Text = Loc.GetString("dh-heavy-gun-ui-defense", ("name", defense.Name), ("ammo", defense.Ammo), ("max", defense.MaxAmmo)),
                FontColorOverride = defense.Ammo > 0 ? Ok : Bad,
            });
        }

        if (state.Defenses.Count == 0)
            _defenses.AddChild(new Label { Text = Loc.GetString("dh-heavy-gun-ui-no-defenses") });

        _fire.Disabled = !state.Seated;
    }
}
