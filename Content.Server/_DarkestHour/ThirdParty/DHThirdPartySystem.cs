using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared._DarkestHour.ThirdParty;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Components;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.ThirdParty;

/// <summary>
/// Набор участников событий третьей стороны из призраков «по интересу» (план осады пп. 11 и 28, как в RuCM): событие
/// открывает набор со своими ролями и сроком; наблюдатели в окне «Роли призраков» нажимают одну кнопку
/// «Интересоваться» на всё событие (решение автора 2026-11-26). Записалось достаточно — роли раздаются записавшимся
/// случайно (по порядку ролей: сначала командные) и вызывается обработчик события. Не набралось к сроку — набор
/// закрывается без события.
/// </summary>
public sealed partial class DHThirdPartySystem : EntitySystem
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
        public Action<Dictionary<string, ICommonSession>>? OnReady;

        /// <summary>Набор закрыт без события (срок вышел или отменён).</summary>
        public Action? OnExpired;

        /// <summary>
        /// Отряд (LA-V, решение автора 2026-11-26): мест на роль — <see cref="Slots"/> (по умолчанию 1), событие стартует,
        /// когда записалось не меньше <see cref="MinTotal"/>; места раздаются по порядку ролей, незанятыми остаются
        /// последние. Без <see cref="MinTotal"/> нужно по одному на каждое место.
        /// </summary>
        public Dictionary<string, int>? Slots;
        public int? MinTotal;

        /// <summary>
        /// Отряд: набрался минимум, но места ещё есть — ждать столько, пока запишутся остальные (или все места
        /// заполнятся). К сроку набора отряд стартует сразу, если минимум есть.
        /// </summary>
        public TimeSpan Gather = TimeSpan.Zero;

        /// <summary>Когда набрался минимум (для <see cref="Gather"/>).</summary>
        public TimeSpan? MinReachedAt;

        /// <summary>Участники отряда (для <see cref="MinTotal"/>): роль и игрок.</summary>
        public Action<List<(string Role, ICommonSession Session)>>? OnReadySquad;

        public readonly HashSet<NetUserId> Interested = new();

        /// <summary>Места по порядку ролей: роль повторяется столько раз, сколько у неё мест.</summary>
        public IEnumerable<string> Seats()
        {
            foreach (var role in Roles)
            {
                var slots = Slots?.GetValueOrDefault(role.Id, 1) ?? 1;
                for (var i = 0; i < slots; i++)
                {
                    yield return role.Id;
                }
            }
        }

        /// <summary>Сколько нужно, чтобы событие началось.</summary>
        public int Required => MinTotal ?? Seats().Count();
    }

    private readonly Dictionary<string, Recruitment> _open = new();
    private TimeSpan _nextCheck;

    /// <summary>В раунде уже было событие третьей стороны (решение автора 2026-11-26: одно за раунд).</summary>
    public string? Happened { get; private set; }

    /// <summary>Занять раунд событием; false — другое событие уже было.</summary>
    public bool Claim(string id)
    {
        if (Happened != null && Happened != id)
            return false;

        Happened = id;
        return true;
    }

    public bool AnyOpen => _open.Count > 0;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<DHThirdPartyInterestEvent>(OnInterest);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _open.Clear();
            Happened = null;
        });
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
        _open[recruitment.Id] = recruitment;
        Broadcast(true);

        // Наблюдателям — сообщение в чат (запись — в окне «Роли призраков»).
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
        if (!_open.TryGetValue(ev.EventId, out var recruitment))
            return;

        // Записываются только наблюдатели (призраки).
        if (ev.Interested && !IsObserver(args.SenderSession))
            return;

        if (ev.Interested)
            recruitment.Interested.Add(args.SenderSession.UserId);
        else
            recruitment.Interested.Remove(args.SenderSession.UserId);

        if (!TryStart(recruitment))
            Broadcast(false);
    }

    private bool IsObserver(ICommonSession session)
    {
        return session.Status == SessionStatus.InGame && session.AttachedEntity is { } entity &&
               HasComp<GhostComponent>(entity);
    }

    /// <summary>Записавшиеся, которые всё ещё наблюдатели, вразнобой.</summary>
    private List<NetUserId> Candidates(Recruitment recruitment)
    {
        var candidates = recruitment.Interested
            .Where(user => _players.TryGetSessionById(user, out var s) && IsObserver(s))
            .ToList();
        _random.Shuffle(candidates);
        return candidates;
    }

    /// <summary>Записалось достаточно — раздаём места по порядку ролей случайным записавшимся.</summary>
    private bool TryStart(Recruitment recruitment, bool force = false)
    {
        var candidates = Candidates(recruitment);
        var seats = recruitment.Seats().ToList();
        if (candidates.Count < recruitment.Required)
        {
            recruitment.MinReachedAt = null;
            return false;
        }

        // Минимум есть, но места ещё остались — даём дозаписаться.
        var now = _ticker.RoundDuration();
        if (!force && candidates.Count < seats.Count && recruitment.Gather > TimeSpan.Zero)
        {
            recruitment.MinReachedAt ??= now;
            if (now - recruitment.MinReachedAt < recruitment.Gather)
                return false;
        }

        var picked = seats.Zip(candidates, (role, user) => (Role: role, Session: _players.GetSessionById(user))).ToList();

        _open.Remove(recruitment.Id);

        // Взятые в одно событие больше не ждут другого.
        foreach (var other in _open.Values)
        {
            foreach (var (_, session) in picked)
            {
                other.Interested.Remove(session.UserId);
            }
        }

        Broadcast(false);
        if (recruitment.OnReadySquad != null)
            recruitment.OnReadySquad(picked);
        else
            recruitment.OnReady?.Invoke(picked.ToDictionary(p => p.Role, p => p.Session));

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
                if (!TryStart(recruitment, force: true))
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

            info.Interested = recruitment.Interested.Count;
            info.Required = recruitment.Required;
            info.Slots = recruitment.Seats().Count();
            info.Mine = recruitment.Interested.Contains(session.UserId);
            foreach (var role in recruitment.Roles)
            {
                var slots = recruitment.Slots?.GetValueOrDefault(role.Id, 1) ?? 1;
                info.Roles.Add(new DHThirdPartyRoleInfo
                {
                    Id = role.Id,
                    Name = slots > 1 ? $"{Loc.GetString(role.Name)} ×{slots}" : Loc.GetString(role.Name),
                    Description = Loc.GetString(role.Description),
                });
            }

            ev.Events.Add(info);
        }

        return ev;
    }
}
