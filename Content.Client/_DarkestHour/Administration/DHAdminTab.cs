using Content.Client.UserInterface.Controls;
using Robust.Client.Console;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._DarkestHour.Administration;

/// <summary>
/// Вкладка админ-панели «Darkest Hour»: вызовы внешних отрядов одной кнопкой.
/// </summary>
public sealed partial class DHAdminTab : Control
{
    [Dependency] private IClientConsoleHost _console = default!;

    public DHAdminTab()
    {
        IoCManager.InjectDependencies(this);
        Margin = new Thickness(4);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
        };

        root.AddChild(new Label { Text = Loc.GetString("dh-admin-tab-response-header"), FontColorOverride = Color.FromHex("#9A9A9A") });

        var callMetallArmed = new ConfirmButton
        {
            Text = Loc.GetString("dh-admin-tab-call-metall-armed"),
            ToolTip = Loc.GetString("dh-admin-tab-call-metall-armed-tooltip"),
            HorizontalAlignment = HAlignment.Left,
            MinWidth = 260,
        };
        callMetallArmed.OnPressed += _ => _console.ExecuteCommand("dhcallmetallarmed");
        root.AddChild(callMetallArmed);

        var callRepublic = new ConfirmButton
        {
            Text = Loc.GetString("dh-admin-tab-call-republic"),
            ToolTip = Loc.GetString("dh-admin-tab-call-republic-tooltip"),
            HorizontalAlignment = HAlignment.Left,
            MinWidth = 260,
            ModulateSelfOverride = Color.FromHex("#5E8C2A"),
        };
        callRepublic.OnPressed += _ => _console.ExecuteCommand("dhcallrepublic");
        root.AddChild(callRepublic);

        var callDeadend = new ConfirmButton
        {
            Text = Loc.GetString("dh-admin-tab-call-republic-deadend"),
            ToolTip = Loc.GetString("dh-admin-tab-call-republic-deadend-tooltip"),
            HorizontalAlignment = HAlignment.Left,
            MinWidth = 260,
            ModulateSelfOverride = Color.FromHex("#7B2FBE"),
        };
        callDeadend.OnPressed += _ => _console.ExecuteCommand("dhcallrepublicdeadend");
        root.AddChild(callDeadend);

        var callPmc = new ConfirmButton
        {
            Text = Loc.GetString("dh-admin-tab-call-lav-pmc"),
            ToolTip = Loc.GetString("dh-admin-tab-call-lav-pmc-tooltip"),
            HorizontalAlignment = HAlignment.Left,
            MinWidth = 260,
            ModulateSelfOverride = Color.FromHex("#A68BE8"),
        };
        callPmc.OnPressed += _ => _console.ExecuteCommand("dhcalllavpmc");
        root.AddChild(callPmc);

        // Событие «Ассоциативный десантник» (план осады п. 28): открыть набор двух призраков сейчас.
        var callZga = new ConfirmButton
        {
            Text = Loc.GetString("dh-admin-tab-call-zga"),
            ToolTip = Loc.GetString("dh-admin-tab-call-zga-tooltip"),
            HorizontalAlignment = HAlignment.Left,
            MinWidth = 260,
            ModulateSelfOverride = Color.FromHex("#7EC8F0"),
        };
        callZga.OnPressed += _ => _console.ExecuteCommand("dhcallzgaparatrooper");
        root.AddChild(callZga);

        AddChild(root);
    }
}
