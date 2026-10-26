using Content.Client.Eye;
using Content.Shared._DarkestHour.Overwatch;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics;

namespace Content.Client._DarkestHour.Overwatch;

[UsedImplicitly]
public sealed class DHOverwatchBoundUserInterface : BoundUserInterface
{
    [Dependency] private IPlayerManager _player = default!;

    private readonly EyeLerpingSystem _eyeLerping;

    private DHOverwatchWindow? _window;
    private EntityUid? _current;

    public DHOverwatchBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _eyeLerping = EntMan.System<EyeLerpingSystem>();
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHOverwatchWindow>();
        _window.OnWatch += target => SendMessage(new DHOverwatchWatchMessage(target));
        _window.OnOrder += text => SendMessage(new DHOverwatchOrderMessage(text));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not DHOverwatchState cast)
            return;

        NetEntity? watchingNet = null;
        if (_player.LocalEntity is { } local &&
            cast.Watching.TryGetValue(EntMan.GetNetEntity(local), out var target))
        {
            watchingNet = target;
        }

        // Глаз бойца приходит, как только он попал в нашу видимость (подписка на сервере), — до того «связь».
        var watching = EntMan.GetEntity(watchingNet);
        if (watching != _current)
        {
            if (_current != null)
                _eyeLerping.RemoveEye(_current.Value);

            _current = null;
        }

        IEye? eye = null;
        if (watching is { } uid && EntMan.TryGetComponent<EyeComponent>(uid, out var eyeComp))
        {
            if (_current == null)
            {
                _eyeLerping.AddEye(uid);
                _current = uid;
            }

            eye = eyeComp.Eye;
        }

        _window.UpdateState(cast, watchingNet, eye);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (_current != null)
        {
            _eyeLerping.RemoveEye(_current.Value);
            _current = null;
        }
    }
}
