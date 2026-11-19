using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Carrier;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Carrier;

/// <summary>Схема повреждений авианосца: прочность корпуса и состояние отсеков. Собрано кодом, без XAML.</summary>
public sealed class DHCarrierDamageWindow : DefaultWindow
{
    private static readonly Color Ok = Color.FromHex("#7fd890");
    private static readonly Color Warn = Color.FromHex("#e8c04a");
    private static readonly Color Bad = Color.FromHex("#e8604a");

    private readonly Label _hull = new();
    private readonly BoxContainer _list = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };

    public DHCarrierDamageWindow()
    {
        DHFonts.RegisterTerminal(this);
        Title = Loc.GetString("dh-carrier-damage-ui-title");
        MinSize = new System.Numerics.Vector2(460, 380);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(6),
        };

        root.AddChild(_hull);
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_list);
        root.AddChild(scroll);
        Contents.AddChild(root);
    }

    public void UpdateState(DHCarrierDamageState state)
    {
        var percent = state.MaxHull > 0f ? state.Hull / state.MaxHull : 0f;
        _hull.Text = Loc.GetString("dh-carrier-damage-ui-hull", ("hull", (int) MathF.Ceiling(percent * 100f)));
        _hull.FontColorOverride = percent > 0.6f ? Ok : percent > 0.3f ? Warn : Bad;

        _list.RemoveAllChildren();
        foreach (var row in state.Rows)
        {
            var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
            line.AddChild(new Label { Text = Loc.GetString(row.Name), HorizontalExpand = true, ClipText = true });

            string status;
            Color color;
            if (row.Breaches > 0)
            {
                status = row.Fires > 0
                    ? Loc.GetString("dh-carrier-damage-ui-breach-fire", ("breaches", row.Breaches), ("fires", row.Fires))
                    : Loc.GetString("dh-carrier-damage-ui-breach", ("breaches", row.Breaches));
                color = Bad;
            }
            else if (row.Fires > 0)
            {
                status = Loc.GetString("dh-carrier-damage-ui-fire", ("fires", row.Fires));
                color = Warn;
            }
            else
            {
                status = Loc.GetString("dh-carrier-damage-ui-ok");
                color = Ok;
            }

            line.AddChild(new Label { Text = status, FontColorOverride = color });
            _list.AddChild(line);
        }
    }
}
