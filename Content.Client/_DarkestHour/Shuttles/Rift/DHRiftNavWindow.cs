using System.Linq;
using System.Numerics;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client._DarkestHour.Shuttles.Rift;

/// <summary>
/// Навигационная консоль разрывных прыжков: список систем, карта выбранной системы, точка выхода, прыжок.
/// </summary>
public sealed partial class DHRiftNavWindow : DefaultWindow
{
    [Dependency] private IPrototypeManager _proto = default!;

    public event Action<string>? OnViewSystem;
    public event Action<string, Vector2>? OnJump;

    private readonly BoxContainer _systems = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
    private readonly DHSystemMapControl _map = new() { HorizontalExpand = true, VerticalExpand = true, MinSize = new Vector2(480, 420) };
    private readonly Label _systemName = new();
    private readonly Label _status = new();
    private readonly Label _target = new();
    private readonly Button _jump = new() { HorizontalExpand = true };
    private readonly Button _fit = new();

    private DHRiftNavState? _state;

    public DHRiftNavWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("dh-rift-nav-title");
        MinSize = new Vector2(720, 540);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(6),
        };

        var left = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            MinWidth = 180,
        };
        left.AddChild(new Label { Text = Loc.GetString("dh-rift-nav-systems"), FontColorOverride = Color.FromHex("#8FA6C8") });
        left.AddChild(_systems);
        root.AddChild(left);

        var right = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        right.AddChild(_systemName);
        right.AddChild(_map);
        right.AddChild(_target);
        right.AddChild(_status);

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };
        _fit.Text = Loc.GetString("dh-rift-nav-fit");
        _fit.OnPressed += _ => _map.Fit();
        buttons.AddChild(_fit);
        buttons.AddChild(_jump);
        right.AddChild(buttons);

        root.AddChild(right);
        Contents.AddChild(root);

        _map.OnTargetSelected += _ => UpdateTarget();
        _jump.OnPressed += _ =>
        {
            if (_state != null && _map.Target is { } target)
                OnJump?.Invoke(_state.ViewedSystem, target);
        };

        UpdateTarget();
    }

    public void UpdateState(DHRiftNavState state)
    {
        _state = state;

        _systems.RemoveAllChildren();
        foreach (var entry in state.Systems)
        {
            var text = entry.Current ? Loc.GetString("dh-rift-nav-system-current", ("name", entry.Name)) : entry.Name;
            var button = new Button
            {
                Text = text,
                ToggleMode = true,
                Pressed = entry.Id == state.ViewedSystem,
                HorizontalExpand = true,
            };
            var id = entry.Id;
            button.OnPressed += _ => OnViewSystem?.Invoke(id);
            _systems.AddChild(button);
        }

        _proto.TryIndex<DHStarSystemPrototype>(state.ViewedSystem, out var system);
        _map.SetSystem(system);
        _map.Grids = state.Grids;

        var viewedName = state.Systems.FirstOrDefault(s => s.Id == state.ViewedSystem)?.Name ?? state.ViewedSystem;
        _systemName.Text = Loc.GetString(state.Rift ? "dh-rift-nav-viewing-rift" : "dh-rift-nav-viewing", ("name", viewedName));
        _status.Text = state.Status;
        _status.FontColorOverride = state.CanJump ? Color.LightGreen : Color.OrangeRed;

        UpdateTarget();
    }

    private void UpdateTarget()
    {
        if (_map.Target is not { } target)
        {
            _target.Text = Loc.GetString("dh-rift-nav-no-target");
            _jump.Text = Loc.GetString("dh-rift-nav-jump");
            _jump.Disabled = true;
            return;
        }

        var nearest = string.Empty;
        var nearestDistance = float.MaxValue;
        if (_map.StarSystem != null)
        {
            foreach (var body in _map.StarSystem.Bodies)
            {
                var surface = Vector2.Distance(target, body.Position) - body.Radius;
                if (surface < nearestDistance)
                {
                    nearestDistance = surface;
                    nearest = Loc.GetString(body.Name);
                }
            }
        }

        _target.Text = nearest.Length > 0
            ? Loc.GetString("dh-rift-nav-target", ("x", MathF.Round(target.X / 1000f, 1)), ("y", MathF.Round(target.Y / 1000f, 1)),
                ("body", nearest), ("distance", DHCelestial.FormatDistance(nearestDistance)))
            : Loc.GetString("dh-rift-nav-target-empty", ("x", MathF.Round(target.X / 1000f, 1)), ("y", MathF.Round(target.Y / 1000f, 1)));

        _jump.Text = Loc.GetString(_state?.Rift == true ? "dh-rift-nav-jump-rift" : "dh-rift-nav-jump");
        _jump.Disabled = _state is not { CanJump: true };
    }
}
