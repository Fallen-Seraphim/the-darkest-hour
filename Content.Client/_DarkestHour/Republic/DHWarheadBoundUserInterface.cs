using Content.Shared._DarkestHour.Republic;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Republic;

[UsedImplicitly]
public sealed class DHWarheadBoundUserInterface : BoundUserInterface
{
    private DHWarheadWindow? _window;

    public DHWarheadBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHWarheadWindow>();
        _window.OnDigit += digit => SendMessage(new DHWarheadKeypadMessage(digit));
        _window.OnClear += () => SendMessage(new DHWarheadClearMessage());
        _window.OnEnter += () => SendMessage(new DHWarheadEnterMessage());
        _window.OnAdjustTimer += delta => SendMessage(new DHWarheadAdjustTimerMessage(delta));
        _window.OnArm += () => SendMessage(new DHWarheadArmMessage());
        _window.OnDisarm += () => SendMessage(new DHWarheadDisarmMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHWarheadState warhead)
            _window?.UpdateState(warhead);
    }
}
