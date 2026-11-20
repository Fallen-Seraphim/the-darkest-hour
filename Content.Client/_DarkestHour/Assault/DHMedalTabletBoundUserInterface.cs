using Content.Shared._DarkestHour.Assault;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Assault;

/// <summary>Наградной лист командира (план осады п. 6).</summary>
[UsedImplicitly]
public sealed class DHMedalTabletBoundUserInterface : BoundUserInterface
{
    private DHMedalTabletWindow? _window;

    public DHMedalTabletBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHMedalTabletWindow>();
        _window.OnAward += (target, medal, citation) => SendMessage(new DHMedalAwardMessage(target, medal, citation));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHMedalTabletState tablet)
            _window?.UpdateState(tablet);
    }
}
