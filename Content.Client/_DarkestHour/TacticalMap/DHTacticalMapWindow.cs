using System.Numerics;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Overwatch;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.TacticalMap;

/// <summary>Окно тактической карты (открывается из консоли и планшета наблюдения).</summary>
public sealed class DHTacticalMapWindow : DefaultWindow
{
    private readonly DHTacticalMapControl _map = new() { HorizontalExpand = true, VerticalExpand = true };

    public DHTacticalMapWindow()
    {
        DHFonts.RegisterTerminal(this);
        Title = Loc.GetString("dh-tacmap-title");
        MinSize = new Vector2(520, 520);
        SetSize = new Vector2(760, 800);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        header.AddChild(new Label { Text = Loc.GetString("dh-tacmap-legend"), HorizontalExpand = true });
        var reset = new Button { Text = Loc.GetString("dh-tacmap-reset") };
        reset.OnPressed += _ => _map.ResetView();
        header.AddChild(reset);
        root.AddChild(header);
        root.AddChild(_map);
        Contents.AddChild(root);
    }

    public void UpdateState(DHOverwatchState state)
    {
        _map.SetState(state);
    }
}
