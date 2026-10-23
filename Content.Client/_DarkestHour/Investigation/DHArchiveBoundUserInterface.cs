using Content.Shared._DarkestHour.Investigation;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Investigation;

[UsedImplicitly]
public sealed class DHArchiveBoundUserInterface : BoundUserInterface
{
    private DHArchiveWindow? _window;

    public DHArchiveBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHArchiveWindow>();
        _window.OnRefresh += () => SendMessage(new DHArchiveRefreshMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHArchiveBoundUserInterfaceState archive)
            _window?.UpdateState(archive);
    }
}
