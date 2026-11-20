using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Voting.Managers;
using Content.Shared._DarkestHour.CCVar;
using Content.Shared._DarkestHour.Lobby;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Prototypes;
using Content.Shared.Voting;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Lobby;

/// <summary>
/// Лобби «Осады» (план п. 23, решения автора 2026-11-18 и 2026-11-20):
/// <list type="bullet">
/// <item>сообщает клиентам режим и карту — для меню входа (<see cref="DHLobbyInfoEvent"/>);</item>
/// <item>при открытии лобби сам запускает голосования за режим и за карту, если есть из чего выбирать
/// (<c>dh.lobby_auto_vote</c>).</item>
/// </list>
/// </summary>
public sealed partial class DHLobbySystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameMapManager _maps = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ServerGameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IVoteManager _votes = default!;

    private static readonly TimeSpan InfoInterval = TimeSpan.FromSeconds(5);

    private TimeSpan? _autoVoteAt;
    private TimeSpan _nextInfo;
    private (string Mode, string Map)? _lastInfo;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        _autoVoteAt = ev.New == GameRunLevel.PreRoundLobby
            ? _timing.CurTime + TimeSpan.FromSeconds(_cfg.GetCVar(DHCVars.LobbyAutoVoteDelay))
            : null;
        _lastInfo = null;
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        var (mode, map) = CurrentInfo();
        RaiseNetworkEvent(new DHLobbyInfoEvent(mode, map), ev.PlayerSession);
    }

    private (string Mode, string Map) CurrentInfo()
    {
        var preset = _ticker.CurrentPreset ?? _ticker.Preset;
        var map = _maps.GetSelectedMap()?.MapName ?? string.Empty;
        return (preset?.ModeTitle ?? string.Empty, map);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now >= _nextInfo)
        {
            _nextInfo = now + InfoInterval;
            var info = CurrentInfo();
            if (_lastInfo != info)
            {
                _lastInfo = info;
                RaiseNetworkEvent(new DHLobbyInfoEvent(info.Mode, info.Map));
            }
        }

        if (_autoVoteAt is not { } at || now < at)
            return;

        _autoVoteAt = null;
        if (_ticker.RunLevel != GameRunLevel.PreRoundLobby || !_cfg.GetCVar(DHCVars.LobbyAutoVote) ||
            _players.PlayerCount == 0)
        {
            return;
        }

        var presets = _proto.EnumeratePrototypes<GamePresetPrototype>().Count(p =>
            p.ShowInVote &&
            _players.PlayerCount >= (p.MinPlayers ?? int.MinValue) &&
            _players.PlayerCount <= (p.MaxPlayers ?? int.MaxValue));
        if (presets > 1)
            _votes.CreateStandardVote(null, StandardVoteType.Preset);

        if (_maps.CurrentlyEligibleMaps().Count() > 1)
            _votes.CreateStandardVote(null, StandardVoteType.Map);
    }
}
