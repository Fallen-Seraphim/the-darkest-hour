using Content.Shared._DarkestHour.Supply;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Supply;

public sealed class DHSupplyBoundUserInterface : BoundUserInterface
{
    private DHSupplyWindow? _window;

    public DHSupplyBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHSupplyWindow>();
        _window.OnOrder += id => SendMessage(new DHSupplyOrderMessage(id));
        _window.OnToggleElevator += () => SendMessage(new DHSupplyToggleElevatorMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHSupplyBuiState supply)
            _window?.UpdateState(supply);
    }
}
