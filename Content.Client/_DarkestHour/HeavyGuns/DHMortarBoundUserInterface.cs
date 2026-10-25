using Content.Shared._DarkestHour.HeavyGuns;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.HeavyGuns;

[UsedImplicitly]
public sealed class DHMortarBoundUserInterface : BoundUserInterface
{
    private DHMortarWindow? _window;

    public DHMortarBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHMortarWindow>();
        _window.OnAim += target => SendMessage(new DHMortarAimMessage(target));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHMortarState mortar)
            _window?.UpdateState(mortar);
    }
}
