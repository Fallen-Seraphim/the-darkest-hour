using Content.Shared._DarkestHour.Orbit;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Orbit;

[UsedImplicitly]
public sealed class DHOrbitConsoleBoundUserInterface : BoundUserInterface
{
    private DHOrbitConsoleWindow? _window;

    public DHOrbitConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHOrbitConsoleWindow>();
        _window.OnSetBurn += burning => SendMessage(new DHOrbitSetBurnMessage(burning));
        _window.OnSetThrottle += throttle => SendMessage(new DHOrbitSetThrottleMessage(throttle));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHOrbitConsoleState orbit)
            _window?.UpdateState(orbit);
    }
}
