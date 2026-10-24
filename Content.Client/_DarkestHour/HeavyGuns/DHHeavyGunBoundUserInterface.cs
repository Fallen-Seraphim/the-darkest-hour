using Content.Shared._DarkestHour.HeavyGuns;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.HeavyGuns;

[UsedImplicitly]
public sealed class DHHeavyGunBoundUserInterface : BoundUserInterface
{
    private DHHeavyGunWindow? _window;

    public DHHeavyGunBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHHeavyGunWindow>();
        _window.OnSelectBody += body => SendMessage(new DHHeavyGunSelectBodyMessage(body));
        _window.OnFire += (guns, target, body, position) =>
            SendMessage(new DHHeavyGunFireMessage(guns, target, body, position));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHHeavyGunConsoleState gunState)
            _window?.UpdateState(gunState);
    }
}
