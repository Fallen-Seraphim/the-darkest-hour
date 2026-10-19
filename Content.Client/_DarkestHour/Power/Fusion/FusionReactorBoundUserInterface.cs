using Content.Shared._DarkestHour.Power.Fusion;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Power.Fusion;

[UsedImplicitly]
public sealed class FusionReactorBoundUserInterface : BoundUserInterface
{
    private FusionReactorWindow? _window;

    public FusionReactorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FusionReactorWindow>();
        _window.OnToggle += active => SendMessage(new FusionReactorToggleMessage(active));
        _window.OnSetPower += power => SendMessage(new FusionReactorSetPowerMessage(power));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FusionReactorBuiState reactorState)
            _window?.UpdateState(reactorState);
    }
}
