using Content.Shared._DarkestHour.Supply;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Supply;

[UsedImplicitly]
public sealed class DHVehicleLiftBoundUserInterface : BoundUserInterface
{
    private DHVehicleLiftWindow? _window;

    public DHVehicleLiftBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHVehicleLiftWindow>();
        _window.OnRaise += entity => SendMessage(new DHVehicleLiftRaiseMessage(entity));
        _window.OnLower += () => SendMessage(new DHVehicleLiftLowerMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHVehicleLiftState lift)
            _window?.UpdateState(lift);
    }
}
