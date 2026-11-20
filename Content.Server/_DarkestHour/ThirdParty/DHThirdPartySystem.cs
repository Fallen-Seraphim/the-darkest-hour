using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared._DarkestHour.ThirdParty;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.ThirdParty;

/// <summary>
/// Набор участников событий третьей стороны из призраков «по интересу» (план осады пп. 11 и 28, как в CMU): событие
/// открывает набор со своими ролями и сроком; наблюдатели записываются на роли (окно приходит им само). Как только на
/// каждую роль есть хотя бы один записавшийся — выбираются участники (каждый на одну роль, случайно из записавшихся)
/// и вызывается обработчик события. Не набралось к сроку — набор закрывается без события.
/// </summary>
public sealed class DHThirdPartySystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IRobustRandom _random = default!;

    public sealed class Recruitment
    {
        public required string Id;
        public required LocId Name;
        public required LocId Description;
        public required List<(string Id, LocId Name, LocId Description)> Roles;
        public required TimeSpan Deadline;

        /// <summary>Участники набраны: роль → игрок.</summary>
        public required Action<Dictionary<string, ICommonSession>> OnReady;

        /// <summary>Набор закрыт без события (срок вышел или отменён).</summary>
        public Action? OnExpired;

        public readonly Dictionary<string, HashSet<NetUserId>> Interested = new();
    }

    private readonly Dictionary<string, Recruitment> _open = new();
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<DHThirdPartyInterestEvent>(OnInterest);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _open.Clear());
        _players.PlayerStatusChanged += OnPlayerStatus;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnPlayerStatus;
    }

    public bool IsOpen(string id) => _open.ContainsKey(id);

    public void Open(Recruitment recruitment)
    {
        foreach (var role in recruitment.Roles)
        {
            recruitment.Interested[role.Id] = new HashSet<NetUserId>();
        }

        _open[recruitment.Id] = recruitment;
        Broadcast(true);

        // Наблюдателям — сообщение в чат (окно открывается само; закрыли — команда dhevents).
        var message = Loc.GetString("dh-third-party-opened", ("name", Loc.GetString(recruitment.Name)));
        foreach (var session in _players.Sessions)
        {
            if (IsObserver(session))
                _chat.DispatchServerMessage(session, message);
        }
    }

    public void Close(string id)
    {
        if (_open.Remove(id, out var recruitment))
        {
            recruitment.OnExpired?.Invoke();
            Broadcast(false);
        }
    }

    private void OnPlayerStatus(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.InGame && _open.Count > 0)
            RaiseNetworkEvent(State(args.Session, false), args.Session);
    }

    private void OnInterest(DHThirdPartyInterestEvent ev, EntitySessionEventArgs args)
    {
        if (!_open.TryGetValue(ev.EventId, out var recruitment) ||
            !recruitment.Interested.TryGetValue(ev.RoleId, out var interested))
        {
            return;
        }

        // Записываются только наблюдатели (призраки).
        if (ev.Interested && !IsObserver(args.SenderSession))
            return;

        // Одна роль на игрока в одном наборе.
        foreach (var set in recruitment.Interested.Values)
        {
            set.Remove(args.SenderSession.UserId);
        }

        if (ev.Interested)
            interested.Add(args.SenderSession.UserId);

        if (!TryStart(recruitment))
            Broadcast(false);
    }

    private bool IsObserver(ICommonSession session)
    {
        return session.Status == SessionStatus.InGame && session.AttachedEntity is { } entity &&
               HasComp<GhostComponent>(entity);
    }

    /// <summary>На каждую роль есть кого взять — набираем (сначала роли, где выбор меньше).</summary>
    private bool TryStart(Recruitment recruitment)
    {
        var picked = new Dictionary<string, ICommonSession>();
        var used = new HashSet<NetUserId>();
        foreach (var (role, set) in recruitment.Interested.OrderBy(p => p.Value.Count))
        {
            var candidates = set
                .Where(user => !used.Contains(user) && _players.TryGetSessionById(user, out var s) && IsObserver(s))
                .ToList();
            if (candidates.Count == 0)
                return false;

            var user = _random.Pick(candidates);
            used.Add(user);
            picked[role] = _players.GetSessionById(user);
        }

        _open.Remove(recruitment.Id);
        Broadcast(false);
        recruitment.OnReady(picked);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_open.Count == 0 || _ticker.RunLevel != GameRunLevel.InRound)
            return;

        var now = _ticker.RoundDuration();
        if (now < _nextCheck)
            return;

        _nextCheck = now + TimeSpan.FromSeconds(1);
        foreach (var recruitment in _open.Values.ToList())
        {
            if (now >= recruitment.Deadline)
            {
                Close(recruitment.Id);
                continue;
            }

            // Кто-то из записавшихся мог за это время перестать быть наблюдателем — проверяем снова.
            TryStart(recruitment);
        }

        // Раз в 10 секунд — обновить оставшееся время у клиентов.
        if ((int) now.TotalSeconds % 10 == 0)
            Broadcast(false);
    }

    private void Broadcast(bool opened)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.Status == SessionStatus.InGame)
                RaiseNetworkEvent(State(session, opened), session);
        }
    }

    private DHThirdPartyStateEvent State(ICommonSession session, bool opened)
    {
        var now = _ticker.RoundDuration();
        var ev = new DHThirdPartyStateEvent { Opened = opened };
        foreach (var recruitment in _open.Values)
        {
            var info = new DHThirdPartyEventInfo
            {
                Id = recruitment.Id,
                Name = Loc.GetString(recruitment.Name),
                Description = Loc.GetString(recruitment.Description),
                SecondsLeft = Math.Max(0, (int) (recruitment.Deadline - now).TotalSeconds),
            };

            foreach (var role in recruitment.Roles)
            {
                var set = recruitment.Interested[role.Id];
                info.Roles.Add(new DHThirdPartyRoleInfo
                {
                    Id = role.Id,
                    Name = Loc.GetString(role.Name),
                    Description = Loc.GetString(role.Description),
                    Interested = set.Count,
                    Mine = set.Contains(session.UserId),
                });
            }

            ev.Events.Add(info);
        }

        return ev;
    }
}
