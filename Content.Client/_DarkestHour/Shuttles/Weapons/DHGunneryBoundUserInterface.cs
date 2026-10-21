using Content.Shared._DarkestHour.Shuttles.Weapons;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Shuttles.Weapons;

[UsedImplicitly]
public sealed class DHGunneryBoundUserInterface : BoundUserInterface
{
    private DHGunneryWindow? _window;

    public DHGunneryBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHGunneryWindow>();
        _window.SetConsole(Owner);
        _window.OnFire += (target, guns, continuous) =>
            SendMessage(new DHGunneryFireMessage(EntMan.GetNetCoordinates(target), guns, continuous));
        _window.OnCeaseFire += () => SendMessage(new DHGunneryCeaseFireMessage());
        _window.OnAutoDefense += enabled => SendMessage(new DHGunneryAutoDefenseMessage(enabled));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHGunneryBoundUserInterfaceState gunnery)
            _window?.UpdateState(gunnery);
    }
}
