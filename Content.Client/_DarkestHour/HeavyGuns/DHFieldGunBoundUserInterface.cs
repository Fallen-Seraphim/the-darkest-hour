using Content.Shared._DarkestHour.HeavyGuns;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.HeavyGuns;

[UsedImplicitly]
public sealed class DHFieldGunBoundUserInterface : BoundUserInterface
{
    private DHFieldGunWindow? _window;

    public DHFieldGunBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHFieldGunWindow>();
        _window.OnAim += (azimuth, elevation) => SendMessage(new DHFieldGunAimMessage(azimuth, elevation));
        _window.OnFire += () => SendMessage(new DHFieldGunFireMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHFieldGunState gun)
            _window?.UpdateState(gun);
    }
}
