using Content.Client._DarkestHour.TacticalMap;
using Content.Shared._DarkestHour.Aviation;
using Content.Shared._DarkestHour.Overwatch;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.Aviation;

public sealed partial class DHAircraftBoundUserInterface : BoundUserInterface
{
    [Dependency] private IGameTiming _timing = default!;

    private DHAircraftWindow? _window;
    private DHTacticalMapSystem? _terrain;
    private TimeSpan _nextRequest;

    public DHAircraftBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _terrain = EntMan.System<DHTacticalMapSystem>();
        _window = this.CreateWindow<DHAircraftWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.OnHook += hook => SendMessage(new DHAircraftHookMessage(hook));
        _window.OnLaunch += () => SendMessage(new DHAircraftLaunchMessage());
        _window.OnReturn += () => SendMessage(new DHAircraftReturnMessage());
        _window.OnSpeed += speed => SendMessage(new DHAircraftSpeedMessage(speed));
        _window.OnControl += control => SendMessage(new DHAircraftControlMessage(control));

        if (State is DHAircraftBuiState state)
            UpdateState(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not DHAircraftBuiState cast)
            return;

        _window.UpdateState(cast);

        // Местность тактической карты кэшируется на клиенте по версии — запрашиваем, если её нет.
        if (cast.Map is { TacticalMap: { } ground } map && _terrain != null && !_terrain.Has(ground, map.TerrainVersion) &&
            _timing.RealTime >= _nextRequest)
        {
            _nextRequest = _timing.RealTime + TimeSpan.FromSeconds(3);
            SendMessage(new DHTacticalMapRequestMessage { Map = null });
        }
    }
}
