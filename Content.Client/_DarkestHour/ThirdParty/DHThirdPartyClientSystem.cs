using Content.Shared._DarkestHour.ThirdParty;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Components;
using Robust.Client.Player;

namespace Content.Client._DarkestHour.ThirdParty;

/// <summary>
/// Наборы событий третьей стороны (план осады пп. 11 и 28): хранит открытые наборы от сервера; когда открывается
/// новый — показывает наблюдателю окно записи (<see cref="DHThirdPartyWindow"/>).
/// </summary>
public sealed partial class DHThirdPartyClientSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;

    public IReadOnlyList<DHThirdPartyEventInfo> Events { get; private set; } = Array.Empty<DHThirdPartyEventInfo>();

    public event Action? Updated;

    private DHThirdPartyWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DHThirdPartyStateEvent>(OnState);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }

    private void OnState(DHThirdPartyStateEvent ev)
    {
        Events = ev.Events;
        Updated?.Invoke();

        if (Events.Count == 0)
        {
            _window?.Close();
            return;
        }

        if (ev.Opened && _player.LocalEntity is { } local && HasComp<GhostComponent>(local))
            OpenWindow();
    }

    public void OpenWindow()
    {
        if (_window is { IsOpen: true })
        {
            _window.MoveToFront();
            return;
        }

        _window = new DHThirdPartyWindow(this);
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
    }

    public void SetInterest(string eventId, string roleId, bool interested)
    {
        RaiseNetworkEvent(new DHThirdPartyInterestEvent { EventId = eventId, RoleId = roleId, Interested = interested });
    }
}

/// <summary>Открыть окно записи на события третьей стороны.</summary>
public sealed partial class DHEventsCommand : Robust.Shared.Console.LocalizedEntityCommands
{
    [Dependency] private DHThirdPartyClientSystem _thirdParty = default!;

    public override string Command => "dhevents";

    public override void Execute(Robust.Shared.Console.IConsoleShell shell, string argStr, string[] args)
    {
        _thirdParty.OpenWindow();
    }
}
