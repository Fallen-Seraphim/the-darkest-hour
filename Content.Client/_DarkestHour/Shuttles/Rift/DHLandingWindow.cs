using Content.Client._DarkestHour.UI;
using System.Numerics;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Shuttles.Rift;

/// <summary>
/// Консоль посадки: на орбите — тела в пределах досягаемости и точка спуска в атмосферу выбранного тела;
/// в атмосфере — посадка под кораблём или подъём на орбиту; на поверхности — взлёт в атмосферу.
/// </summary>
public sealed class DHLandingWindow : DefaultWindow
{
    public event Action<string>? OnSelectBody;
    public event Action<string, Vector2>? OnDescend;
    public event Action? OnAscend;
    public event Action? OnLand;

    private readonly Label _header = new();
    private readonly BoxContainer _bodies = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
    private readonly DHSystemMapControl _zone = new() { HorizontalExpand = true, VerticalExpand = true, MinSize = new Vector2(400, 360) };
    private readonly Label _target = new();
    private readonly Label _status = new();
    private readonly Button _action = new() { HorizontalExpand = true };
    private readonly Button _climb = new() { HorizontalExpand = true, Visible = false };

    private DHLandingState? _state;
    private bool? _onSurface;

    public DHLandingWindow()
    {
        DHFonts.RegisterTerminal(this); // DarkestHour: терминальный шрифт
        Title = Loc.GetString("dh-landing-title");
        MinSize = new Vector2(640, 500);

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
            MinWidth = 190,
        };
        left.AddChild(_header);
        left.AddChild(_bodies);
        root.AddChild(left);

        var right = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        right.AddChild(_zone);
        right.AddChild(_target);
        right.AddChild(_status);
        right.AddChild(_action);
        right.AddChild(_climb);
        root.AddChild(right);

        Contents.AddChild(root);

        _zone.OnTargetSelected += _ => UpdateControls();
        _action.OnPressed += _ =>
        {
            if (_state == null)
                return;

            if (_state.SurfaceBody != null)
                OnAscend?.Invoke();
            else if (_state.AtmosphereBody != null)
                OnLand?.Invoke();
            else if (_state.SelectedBody != null && _zone.Target is { } target)
                OnDescend?.Invoke(_state.SelectedBody, target);
        };
        _climb.OnPressed += _ => OnAscend?.Invoke();
    }

    public void UpdateState(DHLandingState state)
    {
        var previousBody = _state?.SelectedBody;
        _state = state;

        var onSurface = state.SurfaceBody != null || state.AtmosphereBody != null;
        if (_onSurface != onSurface || previousBody != state.SelectedBody)
        {
            _zone.Target = null;
            _zone.Fit();
        }
        _onSurface = onSurface;

        _zone.ZoneHalfSize = onSurface ? 0f : state.ZoneSize;
        _zone.Grids = state.SurfaceGrids;

        _bodies.RemoveAllChildren();
        if (state.SurfaceBody != null)
        {
            _header.Text = Loc.GetString("dh-landing-on-surface", ("body", state.SurfaceBody));
        }
        else if (state.AtmosphereBody != null)
        {
            _header.Text = Loc.GetString("dh-landing-in-atmosphere", ("body", state.AtmosphereBody));
        }
        else
        {
            _header.Text = Loc.GetString("dh-landing-bodies", ("range", DHCelestial.FormatDistance(state.LandingRange)));
            foreach (var body in state.Bodies)
            {
                var button = new Button
                {
                    Text = Loc.GetString("dh-landing-body-entry", ("name", body.Name), ("altitude", DHCelestial.FormatDistance(body.Altitude))),
                    ToggleMode = true,
                    Pressed = body.Id == state.SelectedBody,
                    HorizontalExpand = true,
                    ModulateSelfOverride = body.InRange ? null : Color.Gray,
                };
                var id = body.Id;
                button.OnPressed += _ => OnSelectBody?.Invoke(id);
                _bodies.AddChild(button);
            }
        }

        _status.Text = state.Status;
        _status.FontColorOverride = state.CanAct ? Color.LightGreen : Color.OrangeRed;
        UpdateControls();
    }

    private void UpdateControls()
    {
        if (_state == null)
            return;

        _climb.Visible = _state.AtmosphereBody != null;
        if (_state.SurfaceBody != null)
        {
            _target.Text = Loc.GetString("dh-landing-surface-hint");
            _action.Text = Loc.GetString("dh-landing-take-off");
            _action.Disabled = !_state.CanAct;
            return;
        }

        if (_state.AtmosphereBody != null)
        {
            _target.Text = Loc.GetString("dh-landing-atmosphere-hint");
            _action.Text = Loc.GetString("dh-landing-land");
            _action.Disabled = !_state.CanAct;
            _climb.Text = Loc.GetString("dh-landing-ascend");
            _climb.Disabled = !_state.CanAct;
            return;
        }

        _target.Text = _zone.Target is { } target
            ? Loc.GetString("dh-landing-target", ("x", MathF.Round(target.X)), ("y", MathF.Round(target.Y)))
            : Loc.GetString("dh-landing-no-target");
        _action.Text = Loc.GetString("dh-landing-descend");
        _action.Disabled = !_state.CanAct || _zone.Target == null || _state.SelectedBody == null;
    }
}
