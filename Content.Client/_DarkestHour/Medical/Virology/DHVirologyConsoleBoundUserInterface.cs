using Content.Shared._DarkestHour.Medical.Virology;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Medical.Virology;

[UsedImplicitly]
public sealed class DHVirologyConsoleBoundUserInterface : BoundUserInterface
{
    private DHVirologyConsoleWindow? _window;

    public DHVirologyConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHVirologyConsoleWindow>();
        _window.OnSubmit += (group, spread, symptoms, treatment) =>
            SendMessage(new DHVirologySubmitMessage(group, spread, symptoms, treatment));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHVirologyConsoleState consoleState)
            _window?.UpdateState(consoleState);
    }
}
