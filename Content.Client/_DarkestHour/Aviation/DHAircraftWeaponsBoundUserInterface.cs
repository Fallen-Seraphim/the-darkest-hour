using Content.Client._DarkestHour.TacticalMap;
using Content.Shared._DarkestHour.Aviation;
using Content.Shared._DarkestHour.Overwatch;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.Aviation;

/// <summary>Управление вооружением штурмовика (открывается кнопкой «Вооружение» в интерфейсе полёта).</summary>
public sealed partial class DHAircraftWeaponsBoundUserInterface : BoundUserInterface
{
    [Dependency] private IGameTiming _timing = default!;

    private DHAircraftWeaponsWindow? _window;
    private DHTacticalMapSystem? _terrain;
    private TimeSpan _nextRequest;

    public DHAircraftWeaponsBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _terrain = EntMan.System<DHTacticalMapSystem>();
        _window = this.CreateWindow<DHAircraftWeaponsWindow>();
        _window.Title = Loc.GetString("dh-aircraft-weapons-title", ("name", EntMan.GetComponent<MetaDataComponent>(Owner).EntityName));
        _window.SetAircraft(Owner);
        _window.OnToggle += slot => SendMessage(new DHAircraftToggleHardpointMessage(slot));
        _window.OnSelectAll += select => SendMessage(new DHAircraftSelectAllMessage(select));
        _window.OnTarget += point => SendMessage(new DHAircraftTargetMessage(point));
        _window.OnClearTarget += () => SendMessage(new DHAircraftClearTargetMessage());
        _window.OnFire += () => SendMessage(new DHAircraftFireMessage());
        _window.OnCeaseFire += () => SendMessage(new DHAircraftCannonMessage(false));
        _window.OnAltitude += delta => SendMessage(new DHAircraftAltitudeMessage(delta));

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
