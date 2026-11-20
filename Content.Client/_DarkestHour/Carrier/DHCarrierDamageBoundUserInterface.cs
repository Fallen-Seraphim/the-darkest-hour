using Content.Shared._DarkestHour.Carrier;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Carrier;

public sealed class DHCarrierDamageBoundUserInterface : BoundUserInterface
{
    private DHCarrierDamageWindow? _window;

    public DHCarrierDamageBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DHCarrierDamageWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHCarrierDamageState damage)
            _window?.UpdateState(damage);
    }
}
