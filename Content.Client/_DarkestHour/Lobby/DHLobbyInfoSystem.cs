using Content.Shared._DarkestHour.Lobby;

namespace Content.Client._DarkestHour.Lobby;

/// <summary>Режим и карта раунда от сервера (<see cref="DHLobbyInfoEvent"/>) — для меню входа «Осады».</summary>
public sealed partial class DHLobbyInfoSystem : EntitySystem
{
    /// <summary>Название режима (уже переведённое) или пусто, если сервер ещё не прислал.</summary>
    public string Mode { get; private set; } = string.Empty;

    public string Map { get; private set; } = string.Empty;

    public event Action? Updated;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DHLobbyInfoEvent>(OnInfo);
    }

    private void OnInfo(DHLobbyInfoEvent ev)
    {
        Mode = string.IsNullOrEmpty(ev.Mode) ? string.Empty : Loc.GetString(ev.Mode);
        Map = ev.Map;
        Updated?.Invoke();
    }
}
