using Content.Shared._DarkestHour.Supply;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Supply;

[UsedImplicitly]
public sealed class DHSupplyDropBoundUserInterface : BoundUserInterface
{
    private DHSupplyDropWindow? _window;

    public DHSupplyDropBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHSupplyDropWindow>();
        _window.OnLaunch += beacon => SendMessage(new DHSupplyDropLaunchMessage(beacon));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHSupplyDropState drop)
            _window?.UpdateState(drop);
    }
}
