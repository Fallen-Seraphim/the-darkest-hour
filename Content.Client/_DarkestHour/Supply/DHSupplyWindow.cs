using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Supply;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client._DarkestHour.Supply;

/// <summary>Окно компьютера снабжения: запас по разделам, заказ и управление лифтом. Собрано кодом, без XAML.</summary>
public sealed partial class DHSupplyWindow : DefaultWindow
{
    [Dependency] private IPrototypeManager _proto = default!;

    public event Action<string>? OnOrder;
    public event Action? OnToggleElevator;

    private readonly Label _elevator = new();
    private readonly Label _refill = new();
    private readonly Button _toggle = new() { HorizontalExpand = true };
    private readonly BoxContainer _list = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };

    public DHSupplyWindow()
    {
        IoCManager.InjectDependencies(this);
        DHFonts.RegisterTerminal(this);
        Title = Loc.GetString("dh-supply-ui-title");
        MinSize = new System.Numerics.Vector2(460, 520);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(6),
        };

        root.AddChild(_elevator);
        root.AddChild(_refill);
        _toggle.OnPressed += _ => OnToggleElevator?.Invoke();
        root.AddChild(_toggle);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_list);
        root.AddChild(scroll);

        Contents.AddChild(root);
    }

    public void UpdateState(DHSupplyBuiState state)
    {
        _elevator.Text = state.HasElevator
            ? Loc.GetString("dh-supply-ui-elevator", ("mode", Loc.GetString("dh-supply-mode-" + state.Mode.ToString().ToLowerInvariant())),
                ("pending", state.Pending))
            : Loc.GetString("dh-supply-ui-no-elevator");
        _refill.Text = state.Paid
            ? Loc.GetString("dh-supply-ui-balance", ("account", state.Account), ("balance", state.Balance))
            : Loc.GetString("dh-supply-ui-refill", ("points", state.Points), ("min", state.RefillMinPoints + 1),
                ("minutes", state.RefillMinutes));

        _toggle.Disabled = !state.HasElevator ||
                           state.Mode is DHSupplyElevatorMode.Raising or DHSupplyElevatorMode.Lowering;
        _toggle.Text = Loc.GetString(state.Mode == DHSupplyElevatorMode.Raised
            ? "dh-supply-ui-lower"
            : "dh-supply-ui-raise");

        _list.RemoveAllChildren();
        string? category = null;
        foreach (var row in state.Rows)
        {
            if (row.Category != category)
            {
                category = row.Category;
                _list.AddChild(new Label { Text = Loc.GetString(category), FontColorOverride = Color.FromHex("#9fd6a0") });
            }

            var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
            var name = _proto.TryIndex<EntityPrototype>(row.Proto, out var proto) ? proto.Name : row.Proto;
            line.AddChild(new Label { Text = name, HorizontalExpand = true, ClipText = true });
            line.AddChild(new Label
            {
                Text = state.Paid
                    ? Loc.GetString("dh-supply-ui-price", ("price", row.Price))
                    : Loc.GetString("dh-supply-ui-count", ("count", row.Count)),
                MinWidth = 70,
            });

            var id = row.Id;
            var unavailable = state.Paid ? row.Price > state.Balance : row.Count <= 0;
            var order = new Button { Text = Loc.GetString("dh-supply-ui-order"), Disabled = unavailable || !state.HasElevator };
            order.OnPressed += _ => OnOrder?.Invoke(id);
            line.AddChild(order);
            _list.AddChild(line);
        }
    }
}
