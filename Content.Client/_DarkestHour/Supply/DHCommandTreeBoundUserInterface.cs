using Content.Shared._DarkestHour.Supply;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Supply;

public sealed class DHCommandTreeBoundUserInterface : BoundUserInterface
{
    private DHCommandTreeWindow? _window;

    public DHCommandTreeBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHCommandTreeWindow>();
        _window.OnUnlock += node => SendMessage(new DHCommandTreeUnlockMessage(node));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHCommandTreeState tree)
            _window?.UpdateState(tree);
    }
}
