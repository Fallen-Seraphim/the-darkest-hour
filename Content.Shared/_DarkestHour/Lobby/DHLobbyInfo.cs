using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Lobby;

/// <summary>
/// Сервер → клиент в лобби (план осады п. 23): режим и карта текущего или следующего раунда — для меню входа и
/// личного дела. <see cref="Mode"/> — ключ локализации названия режима.
/// </summary>
[Serializable, NetSerializable]
public sealed class DHLobbyInfoEvent : EntityEventArgs
{
    public string Mode;
    public string Map;

    public DHLobbyInfoEvent(string mode, string map)
    {
        Mode = mode;
        Map = map;
    }
}
