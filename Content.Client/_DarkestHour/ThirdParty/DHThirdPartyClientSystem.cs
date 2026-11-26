using Content.Client.UserInterface.Systems.Ghost;
using Content.Shared._DarkestHour.ThirdParty;
using Robust.Client.UserInterface;
using Robust.Shared.Console;

namespace Content.Client._DarkestHour.ThirdParty;

/// <summary>
/// Наборы событий третьей стороны (план осады пп. 11 и 28): хранит открытые наборы от сервера. Запись — в окне
/// «Роли призраков», одна кнопка «Интересоваться» на событие (решение автора 2026-11-26, как в RuCM); число событий
/// прибавляется к счётчику ролей на кнопке призрака.
/// </summary>
public sealed partial class DHThirdPartyClientSystem : EntitySystem
{
    [Dependency] private IConsoleHost _console = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    public IReadOnlyList<DHThirdPartyEventInfo> Events { get; private set; } = Array.Empty<DHThirdPartyEventInfo>();

    public event Action? Updated;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DHThirdPartyStateEvent>(OnState);
    }

    private void OnState(DHThirdPartyStateEvent ev)
    {
        Events = ev.Events;
        Updated?.Invoke();
        _ui.GetUIController<GhostUIController>().UpdateGui();
    }

    /// <summary>Открыть окно «Роли призраков».</summary>
    public void OpenWindow()
    {
        _console.RemoteExecuteCommand(null, "ghostroles");
    }

    public void SetInterest(string eventId, bool interested)
    {
        RaiseNetworkEvent(new DHThirdPartyInterestEvent { EventId = eventId, Interested = interested });
    }
}

/// <summary>Открыть окно «Роли призраков» с наборами на события.</summary>
public sealed partial class DHEventsCommand : Robust.Shared.Console.LocalizedEntityCommands
{
    [Dependency] private DHThirdPartyClientSystem _thirdParty = default!;

    public override string Command => "dhevents";

    public override void Execute(Robust.Shared.Console.IConsoleShell shell, string argStr, string[] args)
    {
        _thirdParty.OpenWindow();
    }
}
