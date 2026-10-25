using System.Numerics;
using Content.Shared._DarkestHour.Assault;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Assault;

/// <summary>Автопилот челнока: где стоим, куда летим и сколько осталось, список направлений (ангар и ЗП).</summary>
public sealed class DHDropshipWindow : DefaultWindow
{
    public event Action<NetEntity?>? OnFly;
    public event Action? OnAbort;

    private static readonly Color Ok = Color.FromHex("#8FD66B");
    private static readonly Color Warn = Color.FromHex("#E8B04A");
    private static readonly Color Dim = Color.FromHex("#8A8A8A");

    private readonly Label _state = new();
    private readonly Label _location = new();
    private readonly Label _flight = new();
    private readonly Button _abort = new();
    private readonly BoxContainer _destinations = new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        SeparationOverride = 4,
    };

    public DHDropshipWindow()
    {
        Title = Loc.GetString("dh-dropship-ui-title");
        MinSize = new Vector2(420, 380);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        root.AddChild(_state);
        root.AddChild(_location);
        root.AddChild(_flight);
        _abort.Text = Loc.GetString("dh-dropship-ui-abort");
        _abort.OnPressed += _ => OnAbort?.Invoke();
        root.AddChild(_abort);

        root.AddChild(new Label { Text = Loc.GetString("dh-dropship-ui-destinations"), FontColorOverride = Warn });
        root.AddChild(_destinations);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(root);
        Contents.AddChild(scroll);
    }

    public void UpdateState(DHDropshipConsoleState state)
    {
        Title = state.ShipName;
        _state.Text = Loc.GetString(state.State switch
        {
            DHDropshipState.Docked => "dh-dropship-ui-state-docked",
            DHDropshipState.Departing => "dh-dropship-ui-state-departing",
            DHDropshipState.InFlight => "dh-dropship-ui-state-flight",
            _ => "dh-dropship-ui-state-landed",
        });
        _state.FontColorOverride = state.State is DHDropshipState.Departing or DHDropshipState.InFlight ? Warn : Ok;
        _location.Text = Loc.GetString("dh-dropship-ui-location", ("location", state.Location));
        _flight.Visible = state.Destination != null;
        _flight.Text = state.Destination == null
            ? string.Empty
            : Loc.GetString("dh-dropship-ui-flight", ("destination", state.Destination), ("seconds", state.SecondsLeft));
        _abort.Visible = state.State == DHDropshipState.Departing;

        var busy = state.State == DHDropshipState.Departing ||
                   state.State == DHDropshipState.InFlight && state.Destination != null;

        _destinations.RemoveAllChildren();
        foreach (var destination in state.Destinations)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
            var button = new Button
            {
                Text = destination.Name,
                HorizontalExpand = true,
                Disabled = busy || !destination.Available,
            };
            var zone = destination.Zone;
            button.OnPressed += _ => OnFly?.Invoke(zone);
            row.AddChild(button);
            if (!string.IsNullOrEmpty(destination.Note))
                row.AddChild(new Label { Text = destination.Note, FontColorOverride = Dim });
            _destinations.AddChild(row);
        }

        if (state.Destinations.Count == 0)
            _destinations.AddChild(new Label { Text = Loc.GetString("dh-dropship-ui-no-destinations"), FontColorOverride = Dim });
    }
}
