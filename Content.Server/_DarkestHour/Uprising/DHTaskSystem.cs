using System.Linq;
using System.Numerics;
using Content.Server._DarkestHour.City;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking;
using Content.Shared._DarkestHour.City;
using Content.Shared._DarkestHour.Levels;
using Content.Shared._DarkestHour.Uprising;
using Content.Shared.Cuffs.Components;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Uprising;

/// <summary>
/// Задания «Восстания» (решение автора 2026-11-12, документ проекта claude/uprising-economy.md). У каждой стороны —
/// три предложения и одно активное задание; награда — на счёт организации. Типы: закладка, разминирование, проверка
/// базы данных (военпол); дальше — ОФСК и LA-V. Плюс награды военполу за арест (член ОФСК в наручниках в камере)
/// и, меньше, за убийство члена ОФСК.
/// </summary>
public sealed partial class DHTaskSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DHBankSystem _bank = default!;
    [Dependency] private DHPoliceDatabaseSystem _database = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ServerGameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public const int ArrestReward = 500;
    public const int KillReward = 200;

    private const int OfferCount = 3;
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan UiInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ArrestInterval = TimeSpan.FromSeconds(3);
    private const int MaxLog = 12;

    private const string StashProto = "DHTaskStash";
    private const string BombProto = "DHTaskBomb";

    private static readonly SoundSpecifier OkSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");
    private static readonly SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");
    private static readonly SoundSpecifier DefuseSound = new SoundPathSpecifier("/Audio/Machines/Nuke/confirm_beep.ogg");

    private sealed class Offer
    {
        public DHTaskPrototype Proto = default!;
        public int Reward;
    }

    private sealed partial class ActiveTask
    {
        public int Id;
        public DHTaskPrototype Proto = default!;
        public int Reward;
        public TimeSpan Deadline;
        public string Text = string.Empty;
        public EntityUid? Target;
        public int Remaining;

        // продажи (LA-V)
        public string GoodsProto = string.Empty;

        // вымогательство (ОФСК)
        public EntityUid? Victim;
        public string VictimAccount = string.Empty;
        public int Amount;
        public int Paid;

        // подрыв (ОФСК): метка объекта и взведённая рядом взрывчатка
        public EntityUid? BombTarget;
        public readonly HashSet<EntityUid> ArmedNear = new();
    }

    private sealed class SideState
    {
        public readonly List<Offer> Offers = new();
        public ActiveTask? Active;
        public TimeSpan OffersAt;
        public readonly List<string> Log = new();
    }

    private readonly Dictionary<DHTaskSide, SideState> _sides = new();
    private readonly HashSet<EntityUid> _arrested = new();
    private int _nextTask = 1;
    private TimeSpan _nextUi;
    private TimeSpan _nextArrestCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _sides.Clear();
            _arrested.Clear();
            _nextTask = 1;
        });

        SubscribeLocalEvent<DHUprisingKilledEvent>(OnKilled);
        SubscribeLocalEvent<DHBankTransferEvent>(OnTransfer);
        SubscribeLocalEvent<DHChequeIssuedEvent>(OnCheque);
        SubscribeLocalEvent<DHTaskStashComponent, InteractHandEvent>(OnStashInteract);

        Subs.BuiEvents<DHTaskConsoleComponent>(DHTaskConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnConsoleOpened);
            subs.Event<DHTaskAcceptMessage>(OnAccept);
            subs.Event<DHTaskAbandonMessage>(OnAbandon);
        });

        Subs.BuiEvents<DHTaskBombComponent>(DHTaskBombUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnBombOpened);
            subs.Event<DHTaskBombCodeMessage>(OnBombCode);
        });
    }

    #region Состояние стороны

    private SideState State(DHTaskSide side)
    {
        if (_sides.TryGetValue(side, out var state))
            return state;

        state = new SideState();
        _sides[side] = state;
        Refill(side, state);
        return state;
    }

    private void Refill(DHTaskSide side, SideState state)
    {
        state.Offers.Clear();
        var pool = _proto.EnumeratePrototypes<DHTaskPrototype>().Where(p => p.Side == side).ToList();
        for (var i = 0; i < OfferCount && pool.Count > 0; i++)
        {
            var proto = PickWeighted(pool);
            pool.Remove(proto);
            state.Offers.Add(new Offer
            {
                Proto = proto,
                Reward = _random.Next(proto.RewardMin, proto.RewardMax + 1) / 10 * 10,
            });
        }
    }

    private DHTaskPrototype PickWeighted(List<DHTaskPrototype> pool)
    {
        var total = pool.Sum(p => p.Weight);
        var roll = _random.NextFloat() * total;
        foreach (var proto in pool)
        {
            roll -= proto.Weight;
            if (roll <= 0)
                return proto;
        }

        return pool[^1];
    }

    private static DHBankOrg OrgOf(DHTaskSide side)
    {
        return side switch
        {
            DHTaskSide.Police => DHBankOrg.Police,
            DHTaskSide.Front => DHBankOrg.Front,
            _ => DHBankOrg.Vanguard,
        };
    }

    private void AddLog(SideState state, string text)
    {
        state.Log.Insert(0, $"[{DHCityClock.TimeText(_ticker.RoundDuration())}] {text}");
        if (state.Log.Count > MaxLog)
            state.Log.RemoveAt(state.Log.Count - 1);
    }

    /// <summary>Выплата стороне на счёт организации (задания, аресты, поддельные паспорта).</summary>
    public void Reward(DHTaskSide side, int amount, string reason)
    {
        _bank.Deposit(_bank.OrgAccount(OrgOf(side)), amount, reason);
        AddLog(State(side), Loc.GetString("dh-task-log-reward", ("amount", amount), ("reason", reason)));
        UpdateConsoles(side);
    }

    #endregion

    #region Компьютер заданий

    private void OnConsoleOpened(Entity<DHTaskConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnAccept(Entity<DHTaskConsoleComponent> ent, ref DHTaskAcceptMessage args)
    {
        var side = ent.Comp.Side;
        var state = State(side);
        if (state.Active != null || args.Index < 0 || args.Index >= state.Offers.Count)
            return;

        var offer = state.Offers[args.Index];
        var task = new ActiveTask
        {
            Id = _nextTask++,
            Proto = offer.Proto,
            Reward = offer.Reward,
            Deadline = _timing.CurTime + offer.Proto.TimeLimit,
        };

        if (!Start(task))
        {
            _popup.PopupEntity(Loc.GetString("dh-task-cant-start"), ent, args.Actor);
            _audio.PlayPvs(DenySound, ent);
            return;
        }

        state.Active = task;
        state.Offers.Clear();
        AddLog(state, Loc.GetString("dh-task-log-accepted", ("name", Loc.GetString(task.Proto.Name))));
        _audio.PlayPvs(OkSound, ent);
        UpdateConsoles(side);
    }

    private void OnAbandon(Entity<DHTaskConsoleComponent> ent, ref DHTaskAbandonMessage args)
    {
        var state = State(ent.Comp.Side);
        if (state.Active is not { } task)
            return;

        Finish(ent.Comp.Side, task, false, Loc.GetString("dh-task-log-abandoned", ("name", Loc.GetString(task.Proto.Name))));
    }

    /// <summary>Запустить задание: спавн закладки/бомбы, фальшивки в базе. False — не получилось (нет мест и т. п.).</summary>
    private bool Start(ActiveTask task)
    {
        switch (task.Proto.Kind)
        {
            case DHTaskKind.Stash:
            {
                if (RandomSpot() is not { } spot)
                    return false;

                var stash = Spawn(StashProto, spot);
                EnsureComp<DHTaskStashComponent>(stash).TaskId = task.Id;
                task.Target = stash;
                task.Text = Loc.GetString("dh-task-stash-text", ("hint", Hint(spot)));
                return true;
            }
            case DHTaskKind.Defuse:
            {
                if (RandomSpot() is not { } spot)
                    return false;

                var bomb = Spawn(BombProto, spot);
                var comp = EnsureComp<DHTaskBombComponent>(bomb);
                comp.TaskId = task.Id;
                comp.Code = _random.Next(0, 10000).ToString("D4");
                comp.ExplodeAt = _timing.CurTime + task.Proto.BombTimer;
                task.Target = bomb;
                task.Deadline = comp.ExplodeAt + TimeSpan.FromSeconds(5);
                task.Text = Loc.GetString("dh-task-defuse-text", ("hint", Hint(spot)), ("code", comp.Code),
                    ("minutes", (int) task.Proto.BombTimer.TotalMinutes));
                return true;
            }
            case DHTaskKind.Database:
                if (!_database.Online())
                    return false;

                task.Remaining = task.Proto.Fakes;
                _database.InjectFakes(task.Id, task.Proto.Fakes);
                task.Text = Loc.GetString("dh-task-database-text", ("count", task.Proto.Fakes));
                return true;
            case DHTaskKind.Extortion:
                return StartExtortion(task);
            case DHTaskKind.Bombing:
                return StartBombing(task);
            case DHTaskKind.Sales:
                return StartSales(task);
        }

        return false;
    }

    /// <summary>Закончить задание: выплата (если успех), уборка, новые предложения.</summary>
    private void Finish(DHTaskSide side, ActiveTask task, bool success, string log)
    {
        var state = State(side);
        if (state.Active != task)
            return;

        state.Active = null;
        // Закладка, бомба (обезвреженная или нет) — убрать с карты.
        if (task.Target is { } target && !TerminatingOrDeleted(target))
            QueueDel(target);

        if (task.Proto.Kind == DHTaskKind.Database)
            _database.ClearFakes(task.Id);

        AddLog(state, log);
        if (success)
        {
            Reward(side, task.Reward, Loc.GetString(task.Proto.Name));
            Refill(side, state);
        }
        else
        {
            state.OffersAt = _timing.CurTime + Cooldown;
        }

        UpdateConsoles(side);
    }

    private bool FindTask(int id, out DHTaskSide side, out ActiveTask task)
    {
        foreach (var (s, state) in _sides)
        {
            if (state.Active is { } active && active.Id == id)
            {
                side = s;
                task = active;
                return true;
            }
        }

        side = default;
        task = default!;
        return false;
    }

    /// <summary>Из базы удалили фальшивку задания.</summary>
    public void OnFakeRemoved(int taskId)
    {
        if (!FindTask(taskId, out var side, out var task))
            return;

        task.Remaining--;
        if (task.Remaining <= 0)
            Finish(side, task, true, Loc.GetString("dh-task-log-done", ("name", Loc.GetString(task.Proto.Name))));
        else
            UpdateConsoles(side);
    }

    #endregion

    #region Места и подсказки

    private EntityCoordinates? RandomSpot()
    {
        var spots = new List<EntityUid>();
        var query = EntityQueryEnumerator<DHTaskSpotComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            spots.Add(uid);
        }

        if (spots.Count == 0)
            return null;

        return Transform(spots[_random.Next(spots.Count)]).Coordinates;
    }

    /// <summary>«Уровень −2, метров 15 к юго-западу от «Склад LA-V»».</summary>
    private string Hint(EntityCoordinates spot)
    {
        var map = _transform.ToMapCoordinates(spot);
        var level = 0;
        if (_transform.GetMap(spot) is { } mapUid && TryComp<DHZLevelComponent>(mapUid, out var z))
            level = z.Level;

        string? beaconName = null;
        var best = float.MaxValue;
        var offset = Vector2.Zero;
        var beacons = EntityQueryEnumerator<DHMapBeaconComponent, TransformComponent>();
        while (beacons.MoveNext(out _, out var beacon, out var xform))
        {
            var delta = map.Position - _transform.GetWorldPosition(xform);
            var distance = delta.Length();
            if (distance >= best)
                continue;

            best = distance;
            offset = delta;
            beaconName = Loc.GetString(beacon.BeaconName);
        }

        var levelText = Loc.GetString("dh-task-hint-level", ("level", level));
        if (beaconName == null)
            return levelText;

        // Метров — с округлением до 5, чтобы искать, а не идти по координатам.
        var meters = Math.Max(5, (int) MathF.Round(best / 5f) * 5);
        return Loc.GetString("dh-task-hint", ("level", levelText), ("meters", meters),
            ("direction", Loc.GetString("dh-task-dir-" + Direction(offset))), ("beacon", beaconName));
    }

    private static string Direction(Vector2 offset)
    {
        if (offset.LengthSquared() < 9f)
            return "here";

        var angle = MathF.Atan2(offset.Y, offset.X) * 180f / MathF.PI; // 0 — восток, 90 — север
        return angle switch
        {
            >= -22.5f and < 22.5f => "e",
            >= 22.5f and < 67.5f => "ne",
            >= 67.5f and < 112.5f => "n",
            >= 112.5f and < 157.5f => "nw",
            >= -67.5f and < -22.5f => "se",
            >= -112.5f and < -67.5f => "s",
            >= -157.5f and < -112.5f => "sw",
            _ => "w",
        };
    }

    #endregion

    #region Закладка и бомба

    private void OnStashInteract(Entity<DHTaskStashComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!TryComp<DHUprisingMemberComponent>(args.User, out var member) || member.Side != DHUprisingSide.Police)
        {
            _popup.PopupEntity(Loc.GetString("dh-task-stash-not-police"), ent, args.User);
            return;
        }

        if (!FindTask(ent.Comp.TaskId, out var side, out var task))
        {
            QueueDel(ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString("dh-task-stash-found"), ent, args.User);
        _audio.PlayPvs(OkSound, ent);
        Finish(side, task, true, Loc.GetString("dh-task-log-done", ("name", Loc.GetString(task.Proto.Name))));
    }

    private void OnBombOpened(Entity<DHTaskBombComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateBomb(ent, null);
    }

    private void OnBombCode(Entity<DHTaskBombComponent> ent, ref DHTaskBombCodeMessage args)
    {
        if (ent.Comp.Defused)
            return;

        if (args.Code.Trim() != ent.Comp.Code)
        {
            ent.Comp.ExplodeAt -= ent.Comp.WrongCodePenalty;
            _audio.PlayPvs(DenySound, ent);
            UpdateBomb(ent, Loc.GetString("dh-task-bomb-wrong", ("seconds", (int) ent.Comp.WrongCodePenalty.TotalSeconds)));
            return;
        }

        ent.Comp.Defused = true;
        _audio.PlayPvs(DefuseSound, ent);
        UpdateBomb(ent, Loc.GetString("dh-task-bomb-defused"));
        if (FindTask(ent.Comp.TaskId, out var side, out var task))
            Finish(side, task, true, Loc.GetString("dh-task-log-done", ("name", Loc.GetString(task.Proto.Name))));
    }

    private void UpdateBomb(Entity<DHTaskBombComponent> ent, string? status)
    {
        if (!_ui.IsUiOpen(ent.Owner, DHTaskBombUiKey.Key))
            return;

        var left = Math.Max(0, (int) (ent.Comp.ExplodeAt - _timing.CurTime).TotalSeconds);
        _ui.SetUiState(ent.Owner, DHTaskBombUiKey.Key, new DHTaskBombState(left, ent.Comp.Defused, status));
    }

    private void Detonate(Entity<DHTaskBombComponent> ent)
    {
        var coords = _transform.GetMapCoordinates(ent);
        QueueDel(ent);
        _explosion.QueueExplosion(coords, "Default", 120f, 6f, 30f, null, canCreateVacuum: false);
        if (FindTask(ent.Comp.TaskId, out var side, out var task))
            Finish(side, task, false, Loc.GetString("dh-task-log-exploded", ("name", Loc.GetString(task.Proto.Name))));
    }

    #endregion

    #region Аресты и убийства

    private void OnKilled(DHUprisingKilledEvent ev)
    {
        // Военполу — за убитого члена ОФСК (меньше, чем за арест).
        if (ev.VictimSide == DHUprisingSide.Front && ev.KillerSide == DHUprisingSide.Police && _arrested.Add(ev.Victim))
            Reward(DHTaskSide.Police, KillReward, Loc.GetString("dh-task-log-kill", ("name", Name(ev.Victim))));

        // ОФСК — за убитого военпола (меньше, чем за пленного).
        if (ev.VictimSide == DHUprisingSide.Police && ev.KillerSide == DHUprisingSide.Front && _arrested.Add(ev.Victim))
            Reward(DHTaskSide.Front, FrontKillReward, Loc.GetString("dh-task-log-kill", ("name", Name(ev.Victim))));

        OnExtortionVictimKilled(ev.Victim);
    }

    /// <summary>Член ОФСК в наручниках в камере военпола — арест (раз за человека).</summary>
    private void CheckArrests()
    {
        var cells = new List<(EntityUid? Map, Vector2 Position, float Radius)>();
        var cellQuery = EntityQueryEnumerator<DHPoliceCellComponent, TransformComponent>();
        while (cellQuery.MoveNext(out _, out var cell, out var xform))
        {
            cells.Add((xform.MapUid, _transform.GetWorldPosition(xform), cell.Radius));
        }

        if (cells.Count == 0)
            return;

        var query = EntityQueryEnumerator<DHUprisingMemberComponent, CuffableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var member, out var cuffable, out var xform))
        {
            if (member.Side != DHUprisingSide.Front || cuffable.CuffedHandCount == 0 || _arrested.Contains(uid) ||
                !_mobState.IsAlive(uid))
            {
                continue;
            }

            var position = _transform.GetWorldPosition(xform);
            foreach (var (map, cellPosition, radius) in cells)
            {
                if (map != xform.MapUid || Vector2.Distance(position, cellPosition) > radius)
                    continue;

                _arrested.Add(uid);
                Reward(DHTaskSide.Police, ArrestReward, Loc.GetString("dh-task-log-arrest", ("name", Name(uid))));
                break;
            }
        }
    }

    #endregion

    #region Обновление

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // Бомбы: время вышло — взрыв.
        var bombs = EntityQueryEnumerator<DHTaskBombComponent>();
        var expired = new List<Entity<DHTaskBombComponent>>();
        while (bombs.MoveNext(out var uid, out var bomb))
        {
            if (!bomb.Defused && now >= bomb.ExplodeAt)
                expired.Add((uid, bomb));
        }

        foreach (var bomb in expired)
        {
            Detonate(bomb);
        }

        foreach (var (side, state) in _sides)
        {
            if (state.Active is { } task && now >= task.Deadline)
            {
                Finish(side, task, false, Loc.GetString("dh-task-log-expired", ("name", Loc.GetString(task.Proto.Name))));
                break; // словарь мог измениться — остальные в следующем кадре
            }

            if (state.Active == null && state.Offers.Count == 0 && now >= state.OffersAt)
            {
                Refill(side, state);
                UpdateConsoles(side);
            }
        }

        if (now >= _nextArrestCheck)
        {
            _nextArrestCheck = now + ArrestInterval;
            CheckArrests();
            CheckCaptures();
            CheckBombings();
        }

        if (now < _nextUi)
            return;

        _nextUi = now + UiInterval;
        foreach (var side in _sides.Keys)
        {
            UpdateConsoles(side);
        }

        var openBombs = EntityQueryEnumerator<DHTaskBombComponent>();
        while (openBombs.MoveNext(out var uid, out var bomb))
        {
            UpdateBomb((uid, bomb), null);
        }
    }

    private void UpdateConsoles(DHTaskSide side)
    {
        var query = EntityQueryEnumerator<DHTaskConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (console.Side == side)
                UpdateUi((uid, console));
        }
    }

    private void UpdateUi(Entity<DHTaskConsoleComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, DHTaskConsoleUiKey.Key))
            return;

        var side = ent.Comp.Side;
        var state = State(side);
        var now = _timing.CurTime;
        var offers = state.Offers
            .Select(o => new DHTaskOfferInfo(Loc.GetString(o.Proto.Name), Loc.GetString(o.Proto.Description), o.Reward))
            .ToList();

        string? activeName = null;
        var activeText = string.Empty;
        var reward = 0;
        var left = 0;
        if (state.Active is { } task)
        {
            activeName = Loc.GetString(task.Proto.Name);
            activeText = task.Text;
            if (task.Proto.Kind == DHTaskKind.Database)
                activeText += "\n" + Loc.GetString("dh-task-database-left", ("count", task.Remaining));
            else if (task.Proto.Kind == DHTaskKind.Sales)
                activeText += "\n" + Loc.GetString("dh-task-sales-left", ("count", task.Remaining));
            else if (task.Proto.Kind == DHTaskKind.Extortion && task.Paid > 0)
                activeText += "\n" + Loc.GetString("dh-task-extortion-paid", ("paid", task.Paid), ("amount", task.Amount));

            reward = task.Reward;
            left = Math.Max(0, (int) (task.Deadline - now).TotalSeconds);
        }

        var cooldown = state.Active == null && state.Offers.Count == 0
            ? Math.Max(0, (int) (state.OffersAt - now).TotalSeconds)
            : 0;

        _ui.SetUiState(ent.Owner, DHTaskConsoleUiKey.Key, new DHTaskConsoleState(side, offers, activeName, activeText,
            reward, left, cooldown, _bank.OrgAccount(OrgOf(side)).Balance, new List<string>(state.Log)));
    }

    #endregion
}

/// <summary>Убит участник «Восстания» (рассылает DHUprisingRuleSystem): награды за убийства.</summary>
public sealed class DHUprisingKilledEvent(EntityUid victim, DHUprisingSide victimSide, DHUprisingSide? killerSide)
    : EntityEventArgs
{
    public readonly EntityUid Victim = victim;
    public readonly DHUprisingSide VictimSide = victimSide;
    public readonly DHUprisingSide? KillerSide = killerSide;
}
