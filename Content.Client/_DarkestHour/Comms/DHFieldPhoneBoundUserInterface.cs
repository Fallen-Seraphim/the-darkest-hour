using Content.Shared._DarkestHour.Comms;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Comms;

[UsedImplicitly]
public sealed class DHFieldPhoneBoundUserInterface : BoundUserInterface
{
    private DHFieldPhoneWindow? _window;

    public DHFieldPhoneBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHFieldPhoneWindow>();
        _window.OnCall += phone => SendMessage(new DHFieldPhoneCallMessage(phone));
        _window.OnAnswer += () => SendMessage(new DHFieldPhoneAnswerMessage());
        _window.OnHangUp += () => SendMessage(new DHFieldPhoneHangUpMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHFieldPhoneState phone)
            _window?.UpdateState(phone);
    }
}
