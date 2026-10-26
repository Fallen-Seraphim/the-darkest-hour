using Content.Shared._DarkestHour.Assault;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Assault;

[UsedImplicitly]
public sealed class DHDropshipBoundUserInterface : BoundUserInterface
{
    private DHDropshipWindow? _window;

    public DHDropshipBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHDropshipWindow>();
        _window.OnFly += zone => SendMessage(new DHDropshipFlyMessage(zone));
        _window.OnAbort += () => SendMessage(new DHDropshipAbortMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHDropshipConsoleState console)
            _window?.UpdateState(console);
    }
}
