using Content.Shared._DarkestHour.Medical.Surgery;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Medical.Surgery;

[UsedImplicitly]
public sealed class DHSurgeryBoundUserInterface : BoundUserInterface
{
    private DHSurgeryWindow? _window;

    public DHSurgeryBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHSurgeryWindow>();
        _window.OnSelectPart += category => SendMessage(new DHSurgerySelectPartMessage(category));
        _window.OnStart += (category, surgery) => SendMessage(new DHSurgeryStartMessage(category, surgery));
        _window.OnStep += category => SendMessage(new DHSurgeryStepMessage(category));
        _window.OnCancel += category => SendMessage(new DHSurgeryCancelMessage(category));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHSurgeryBuiState surgeryState)
            _window?.UpdateState(surgeryState);
    }
}
