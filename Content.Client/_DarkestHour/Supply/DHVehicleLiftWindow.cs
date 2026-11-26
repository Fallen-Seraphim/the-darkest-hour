using System.Numerics;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Supply;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Supply;

/// <summary>Компьютер лифта техники: положение лифта, трюм с кнопками «Поднять» и платформа с кнопкой «Опустить».</summary>
public sealed class DHVehicleLiftWindow : DefaultWindow
{
    public event Action<NetEntity, bool>? OnRaise;
    public event Action? OnLower;

    private static readonly Color Ok = Color.FromHex("#8FD66B");
    private static readonly Color Warn = Color.FromHex("#E8B04A");
    private static readonly Color Dim = Color.FromHex("#8A8A8A");

    private readonly Label _status = new();
    private readonly BoxContainer _stored = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly Label _platform = new();
    private readonly Button _lower = new();

    public DHVehicleLiftWindow()
    {
        DHFonts.RegisterTerminal(this);
        Title = Loc.GetString("dh-vehicle-lift-ui-title");
        MinSize = new Vector2(440, 380);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        root.AddChild(_status);
        root.AddChild(new Label { Text = Loc.GetString("dh-vehicle-lift-ui-hold"), FontColorOverride = Warn });
        root.AddChild(_stored);
        root.AddChild(new Label { Text = Loc.GetString("dh-vehicle-lift-ui-platform"), FontColorOverride = Warn });
        root.AddChild(_platform);

        _lower.Text = Loc.GetString("dh-vehicle-lift-ui-lower");
        _lower.OnPressed += _ => OnLower?.Invoke();
        root.AddChild(_lower);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(root);
        Contents.AddChild(scroll);
    }

    public void UpdateState(DHVehicleLiftState state)
    {
        var working = state.HasLift && state.Blocked.Length == 0;
        _status.Text = !state.HasLift
            ? Loc.GetString("dh-vehicle-lift-ui-no-lift")
            : (Loc.GetString("dh-vehicle-lift-ui-kind-" + state.Kind.ToString().ToLowerInvariant()) + " — " +
               (state.Blocked.Length > 0
                   ? state.Blocked
                   : Loc.GetString("dh-vehicle-lift-ui-mode-" + state.Mode.ToString().ToLowerInvariant())));
        _status.FontColorOverride = working ? Ok : Warn;

        _stored.RemoveAllChildren();
        if (state.Stored.Count == 0)
        {
            _stored.AddChild(new Label { Text = Loc.GetString("dh-vehicle-lift-ui-hold-empty"), FontColorOverride = Dim });
        }

        foreach (var row in state.Stored)
        {
            var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
            line.AddChild(new Label { Text = row.Name, HorizontalExpand = true });
            var canRaise = working && state.Mode == DHSupplyElevatorMode.Lowered;
            var entity = row.Entity;
            // Поддон сброса: машиной на платформу или поддоном для сброса на маяк — одно из двух.
            var button = new Button
            {
                Text = Loc.GetString(row.Pallet ? "dh-vehicle-lift-ui-raise-vehicle" : "dh-vehicle-lift-ui-raise"),
                Disabled = !canRaise,
            };
            button.OnPressed += _ => OnRaise?.Invoke(entity, false);
            line.AddChild(button);
            if (row.Pallet && state.AllowPallet)
            {
                var pallet = new Button
                {
                    Text = Loc.GetString("dh-vehicle-lift-ui-raise-pallet"),
                    ToolTip = Loc.GetString("dh-vehicle-lift-ui-raise-pallet-tooltip"),
                    Disabled = !canRaise,
                };
                pallet.OnPressed += _ => OnRaise?.Invoke(entity, true);
                line.AddChild(pallet);
            }
            _stored.AddChild(line);
        }

        if (state.Mode != DHSupplyElevatorMode.Raised)
        {
            _platform.Text = Loc.GetString("dh-vehicle-lift-ui-platform-down");
            _platform.FontColorOverride = Dim;
        }
        else if (state.OnPlatform.Count == 0)
        {
            _platform.Text = Loc.GetString("dh-vehicle-lift-ui-platform-empty");
            _platform.FontColorOverride = Dim;
        }
        else
        {
            _platform.Text = string.Join("\n", state.OnPlatform);
            _platform.FontColorOverride = null;
        }

        _lower.Disabled = !working || state.Mode != DHSupplyElevatorMode.Raised;
    }
}
