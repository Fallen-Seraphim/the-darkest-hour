using Content.Shared._DarkestHour.Squads;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Squads;

[UsedImplicitly]
public sealed class DHSquadConsoleBoundUserInterface : BoundUserInterface
{
    private DHSquadConsoleWindow? _window;

    public DHSquadConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHSquadConsoleWindow>();
        _window.OnAssign += (fighter, squad) => SendMessage(new DHSquadAssignMessage(fighter, squad));
        _window.OnSetLeader += fighter => SendMessage(new DHSquadSetLeaderMessage(fighter));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHSquadConsoleState squads)
            _window?.UpdateState(squads);
    }
}
