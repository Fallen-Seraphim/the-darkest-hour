using Content.Shared._DarkestHour.Shuttles.Rift;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Shuttles.Rift;

[UsedImplicitly]
public sealed class DHRiftNavBoundUserInterface : BoundUserInterface
{
    private DHRiftNavWindow? _window;

    public DHRiftNavBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHRiftNavWindow>();
        _window.OnViewSystem += system => SendMessage(new DHRiftNavViewSystemMessage(system));
        _window.OnJump += (system, position) => SendMessage(new DHRiftNavJumpMessage(system, position));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHRiftNavState nav)
            _window?.UpdateState(nav);
    }
}
