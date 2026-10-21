using System.Linq;
using System.Numerics;
using Content.Client.Shuttles.UI;
using Content.Shared._DarkestHour.Shuttles.Weapons;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Map;

namespace Content.Client._DarkestHour.Shuttles.Weapons;

/// <summary>
/// Консоль наведения: радар слева (клик — огонь выбранными орудиями по точке), справа список орудий на гриде,
/// непрерывный огонь, прекращение огня и (у станционной консоли) противометеорная защита. Собрано в коде, без XAML.
/// </summary>
public sealed class DHGunneryWindow : DefaultWindow
{
    public event Action<EntityCoordinates, List<NetEntity>, bool>? OnFire;
    public event Action? OnCeaseFire;
    public event Action<bool>? OnAutoDefense;

    private readonly ShuttleNavControl _radar = new()
    {
        MinSize = new Vector2(512, 512),
        HorizontalExpand = true,
        VerticalExpand = true,
    };

    private readonly BoxContainer _guns = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
    private readonly Dictionary<NetEntity, CheckBox> _gunBoxes = new();
    private readonly HashSet<NetEntity> _selected = new();
    private readonly CheckBox _continuous = new();
    private readonly CheckBox _autoDefense = new();
    private readonly Label _noGuns = new() { FontColorOverride = Color.Gray };

    private bool _initialised;

    public DHGunneryWindow()
    {
        Title = Loc.GetString("dh-gunnery-ui-title");
        MinSize = new Vector2(860, 560);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8, Margin = new Thickness(6) };
        root.AddChild(_radar);

        var side = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            MinWidth = 300,
        };

        side.AddChild(new Label { Text = Loc.GetString("dh-gunnery-ui-hint"), FontColorOverride = Color.Gray });

        var selectRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        var all = new Button { Text = Loc.GetString("dh-gunnery-ui-select-all"), HorizontalExpand = true };
        all.OnPressed += _ => SetAll(true);
        var none = new Button { Text = Loc.GetString("dh-gunnery-ui-select-none"), HorizontalExpand = true };
        none.OnPressed += _ => SetAll(false);
        selectRow.AddChild(all);
        selectRow.AddChild(none);
        side.AddChild(selectRow);

        _noGuns.Text = Loc.GetString("dh-gunnery-ui-no-guns");
        side.AddChild(_noGuns);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_guns);
        side.AddChild(scroll);

        _continuous.Text = Loc.GetString("dh-gunnery-ui-continuous");
        side.AddChild(_continuous);

        var cease = new Button { Text = Loc.GetString("dh-gunnery-ui-cease") };
        cease.OnPressed += _ => OnCeaseFire?.Invoke();
        side.AddChild(cease);

        _autoDefense.Text = Loc.GetString("dh-gunnery-ui-auto-defense");
        _autoDefense.Visible = false;
        _autoDefense.OnToggled += args => OnAutoDefense?.Invoke(args.Pressed);
        side.AddChild(_autoDefense);

        root.AddChild(side);
        Contents.AddChild(root);

        _radar.OnRadarClick += coords =>
        {
            if (_selected.Count > 0)
                OnFire?.Invoke(coords, _selected.ToList(), _continuous.Pressed);
        };
    }

    public void SetConsole(EntityUid console)
    {
        _radar.SetConsole(console);
    }

    private void SetAll(bool value)
    {
        foreach (var (gun, box) in _gunBoxes)
        {
            box.Pressed = value;
            if (value)
                _selected.Add(gun);
            else
                _selected.Remove(gun);
        }
    }

    public void UpdateState(DHGunneryBoundUserInterfaceState state)
    {
        _radar.UpdateState(state.Nav);

        _autoDefense.Visible = state.AutoDefenseAvailable;
        _autoDefense.Pressed = state.AutoDefense;

        _guns.RemoveAllChildren();
        _gunBoxes.Clear();
        _noGuns.Visible = state.Guns.Count == 0;

        var present = state.Guns.Select(g => g.Gun).ToHashSet();
        _selected.IntersectWith(present);

        // При первом открытии выбраны все орудия.
        if (!_initialised && state.Guns.Count > 0)
        {
            _selected.UnionWith(present);
            _initialised = true;
        }

        foreach (var gun in state.Guns)
        {
            var status = !gun.Powered
                ? Loc.GetString("dh-gunnery-ui-unpowered")
                : gun.Firing ? Loc.GetString("dh-gunnery-ui-firing") : string.Empty;

            var box = new CheckBox
            {
                Text = Loc.GetString("dh-gunnery-ui-gun", ("name", gun.Name), ("caliber", gun.Caliber),
                    ("ammo", gun.Ammo), ("capacity", gun.Capacity), ("status", status)),
                Pressed = _selected.Contains(gun.Gun),
            };

            var id = gun.Gun;
            box.OnToggled += args =>
            {
                if (args.Pressed)
                    _selected.Add(id);
                else
                    _selected.Remove(id);
            };

            _gunBoxes[id] = box;
            _guns.AddChild(box);
        }
    }
}
