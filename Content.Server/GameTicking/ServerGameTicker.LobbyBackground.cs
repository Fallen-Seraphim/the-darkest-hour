using Content.Shared.GameTicking.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server.GameTicking;

public sealed partial class ServerGameTicker
{
    [ViewVariables]
    public ProtoId<LobbyBackgroundPrototype>? LobbyBackground { get; private set; }

    [ViewVariables]
    private List<ProtoId<LobbyBackgroundPrototype>>? _lobbyBackgrounds;

    private static readonly string[] WhitelistedBackgroundExtensions = new string[] {"png", "jpg", "jpeg", "webp"};

    private void InitializeLobbyBackground()
    {
        var allprotos = ProtoMan.EnumeratePrototypes<LobbyBackgroundPrototype>().ToList();
        _lobbyBackgrounds ??= new List<ProtoId<LobbyBackgroundPrototype>>();

        //create protoids from them
        foreach (var proto in allprotos)
        {
            var ext = proto.Background.Extension;
            if (!WhitelistedBackgroundExtensions.Contains(ext))
                continue;

            //create a protoid and add it to the list
            _lobbyBackgrounds.Add(new ProtoId<LobbyBackgroundPrototype>(proto.ID));
        }

        // DarkestHour: если есть свои фоны (id на «DH») — только они (ночной Лост-Энджелс, решение автора 2026-11-11).
        if (_lobbyBackgrounds.Any(b => b.Id.StartsWith("DH")))
            _lobbyBackgrounds.RemoveAll(b => !b.Id.StartsWith("DH"));

        RandomizeLobbyBackground();
    }

    private void RandomizeLobbyBackground()
    {
        if (_lobbyBackgrounds != null && _lobbyBackgrounds.Count != 0)
            LobbyBackground = Random.Pick(_lobbyBackgrounds);
        else
            LobbyBackground = null;
    }
}
