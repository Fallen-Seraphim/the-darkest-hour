using Content.Client.UserInterface.Controls;
using Robust.Client.Console;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._DarkestHour.Administration;

/// <summary>
/// Вкладка админ-панели «Darkest Hour»: вызовы внешних отрядов одной кнопкой.
/// </summary>
public sealed class DHAdminTab : Control
{
    [Dependency] private readonly IClientConsoleHost _console = default!;

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

        AddChild(root);
    }
}
