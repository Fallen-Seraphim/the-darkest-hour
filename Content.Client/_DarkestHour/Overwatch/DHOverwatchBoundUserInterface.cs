using Content.Client._DarkestHour.TacticalMap;
using Content.Client.Eye;
using Content.Shared._DarkestHour.Overwatch;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.Overwatch;

[UsedImplicitly]
public sealed partial class DHOverwatchBoundUserInterface : BoundUserInterface
{
    [Dependency] private IPlayerManager _player = default!;

    private readonly EyeLerpingSystem _eyeLerping;

    private DHOverwatchWindow? _window;
    private DHTacticalMapWindow? _tacmap;
    private DHOverwatchState? _state;
    private readonly DHTacticalMapSystem _terrain;
    private TimeSpan _nextRequest;
    private EntityUid? _current;

    public DHOverwatchBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _eyeLerping = EntMan.System<EyeLerpingSystem>();
        _terrain = EntMan.System<DHTacticalMapSystem>();
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHOverwatchWindow>();
        _window.OnWatch += target => SendMessage(new DHOverwatchWatchMessage(target));
        _window.OnOrder += text => SendMessage(new DHOverwatchOrderMessage(text));
        _window.OnTacticalMap += OpenTacticalMap;
    }

    private void OpenTacticalMap()
    {
        if (_tacmap is { IsOpen: true })
        {
            _tacmap.MoveToFront();
            return;
        }

        _tacmap = new DHTacticalMapWindow();
        _tacmap.OpenCentered();
        if (_state != null)
            UpdateTacticalMap(_state);
    }

    /// <summary>Отметки — в окно карты; местность запрашиваем, если её нет или версия устарела.</summary>
    private void UpdateTacticalMap(DHOverwatchState state)
    {
        if (_tacmap is not { IsOpen: true })
            return;

        _tacmap.UpdateState(state);
        if (state.TacticalMap is not { } map || _terrain.Has(map, state.TerrainVersion))
            return;

        var now = IoCManager.Resolve<IGameTiming>().RealTime;
        if (now < _nextRequest)
            return;

        _nextRequest = now + TimeSpan.FromSeconds(3);
        SendMessage(new DHTacticalMapRequestMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not DHOverwatchState cast)
            return;

        _state = cast;
        UpdateTacticalMap(cast);

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
        _tacmap?.Close();
        _tacmap = null;

        if (_current != null)
        {
            _eyeLerping.RemoveEye(_current.Value);
            _current = null;
        }
    }
}
