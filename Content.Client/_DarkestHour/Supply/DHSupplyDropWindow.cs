using System.Numerics;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Supply;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Supply;

/// <summary>Консоль «Сброс грузов»: что на площадке, готовность и включённые маяки с кнопкой сброса.</summary>
public sealed class DHSupplyDropWindow : DefaultWindow
{
    public event Action<NetEntity>? OnLaunch;

    private static readonly Color Ok = Color.FromHex("#8FD66B");
    private static readonly Color Warn = Color.FromHex("#E8B04A");
    private static readonly Color Dim = Color.FromHex("#8A8A8A");

    private readonly Label _status = new();
    private readonly Label _cargo = new();
    private readonly BoxContainer _beacons = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };

    public DHSupplyDropWindow()
    {
        DHFonts.RegisterTerminal(this);
        Title = Loc.GetString("dh-supply-drop-ui-title");
        MinSize = new Vector2(460, 380);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        root.AddChild(_status);
        root.AddChild(new Label { Text = Loc.GetString("dh-supply-drop-ui-pad"), FontColorOverride = Warn });
        root.AddChild(_cargo);
        root.AddChild(new Label { Text = Loc.GetString("dh-supply-drop-ui-beacons"), FontColorOverride = Warn });
        root.AddChild(_beacons);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(root);
        Contents.AddChild(scroll);
    }

    public void UpdateState(DHSupplyDropState state)
    {
        var ready = state.HasPad && state.Cooldown <= 0 && state.Blocked.Length == 0 && state.Cargo.Count > 0;
        if (!state.HasPad)
            _status.Text = Loc.GetString("dh-supply-drop-ui-no-pad");
        else if (state.Blocked.Length > 0)
            _status.Text = state.Blocked;
        else if (state.Cooldown > 0)
            _status.Text = Loc.GetString("dh-supply-drop-ui-cooldown", ("seconds", state.Cooldown));
        else
            _status.Text = Loc.GetString("dh-supply-drop-ui-ready");
        _status.FontColorOverride = state.HasPad && state.Cooldown <= 0 && state.Blocked.Length == 0 ? Ok : Warn;

        if (state.Cargo.Count == 0)
        {
            _cargo.Text = Loc.GetString("dh-supply-drop-ui-pad-empty");
            _cargo.FontColorOverride = Dim;
        }
        else
        {
            _cargo.Text = string.Join("\n", state.Cargo);
            _cargo.FontColorOverride = null;
        }

        _beacons.RemoveAllChildren();
        if (state.Beacons.Count == 0)
        {
            _beacons.AddChild(new Label
            {
                Text = Loc.GetString("dh-supply-drop-ui-no-beacons"),
                FontColorOverride = Dim,
            });
            return;
        }

        foreach (var beacon in state.Beacons)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
            row.AddChild(new Label
            {
                Text = Loc.GetString("dh-supply-drop-ui-beacon", ("name", beacon.Name), ("x", beacon.X), ("y", beacon.Y)),
                HorizontalExpand = true,
                FontColorOverride = beacon.Incoming ? Warn : null,
            });

            var button = new Button { Text = Loc.GetString("dh-supply-drop-ui-launch"), Disabled = !ready };
            var target = beacon.Beacon;
            button.OnPressed += _ => OnLaunch?.Invoke(target);
            row.AddChild(button);
            _beacons.AddChild(row);
        }
    }
}
