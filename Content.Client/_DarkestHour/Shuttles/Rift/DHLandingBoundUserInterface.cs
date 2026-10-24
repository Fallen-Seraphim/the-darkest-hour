using Content.Shared._DarkestHour.Shuttles.Rift;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Shuttles.Rift;

[UsedImplicitly]
public sealed class DHLandingBoundUserInterface : BoundUserInterface
{
    private DHLandingWindow? _window;

    public DHLandingBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHLandingWindow>();
        _window.OnSelectBody += body => SendMessage(new DHLandingSelectBodyMessage(body));
        _window.OnDescend += (body, position) => SendMessage(new DHLandingDescendMessage(body, position));
        _window.OnAscend += () => SendMessage(new DHLandingAscendMessage());
        _window.OnLand += () => SendMessage(new DHLandingLandMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHLandingState landing)
            _window?.UpdateState(landing);
    }
}
