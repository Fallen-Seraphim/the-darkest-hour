using System.Linq;
using System.Numerics;
using Content.Server._DarkestHour.Assault;
using Content.Server._DarkestHour.Shuttles.Rift;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Humanoid.Systems;
using Content.Server.Mind;
using Content.Shared._DarkestHour.ThirdParty;
using Content.Shared.Actions;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.ThirdParty;

/// <summary>Состояние задания десантника.</summary>
public enum DHZGAMissionState : byte
{
    /// <summary>Ищет и устраняет цель.</summary>
    Hunting,
    /// <summary>Цель мертва — уходит к точке эвакуации.</summary>
    Evacuating,
    /// <summary>Эвакуирован — успех.</summary>
    Evacuated,
    /// <summary>Нарушил кодекс — ЗГА отреклась, дальше сам по себе.</summary>
    Disavowed,
    /// <summary>Погиб.</summary>
    Dead,
}

/// <summary>Ассоциативный десантник: цель, эвакуация, соблюдение кодекса Куратора (план осады п. 28).</summary>
[RegisterComponent]
public sealed partial class DHZGAParatrooperComponent : Component
{
    [ViewVariables] public EntityUid? Target;
    [ViewVariables] public string TargetName = string.Empty;
    [ViewVariables] public string TargetJob = string.Empty;
    [ViewVariables] public EntityUid? EvacPoint;
    [ViewVariables] public EntityUid? Mars;
    [ViewVariables] public EntityUid? Pinpointer;
    [ViewVariables] public EntityUid? StatusAction;

    [ViewVariables] public DHZGAMissionState State = DHZGAMissionState.Hunting;
    [ViewVariables] public TimeSpan LandedAt;
    [ViewVariables] public TimeSpan? TargetDownAt;
    [ViewVariables] public TimeSpan? CuffedSince;

    /// <summary>Убийства не цели и не напавших (кодекс: не больше <see cref="DHZGAParatrooperSystem.MaxExtraKills"/>).</summary>
    [ViewVariables] public int ExtraKills;

    /// <summary>Кто сам напал на десантника — их убивать можно.</summary>
    [ViewVariables] public HashSet<EntityUid> Aggressors = new();
}

/// <summary>ИИ брони M.A.R.S. 1.5: «голос в броне», всегда при десантнике.</summary>
[RegisterComponent]
public sealed partial class DHMarsComponent : Component
{
    [ViewVariables] public EntityUid? Host;
    [ViewVariables] public EntityUid? StatusAction;
}

/// <summary>Точка эвакуации десантника (невидимая метка, на неё указывает пинпоинтер).</summary>
[RegisterComponent]
public sealed partial class DHZGAEvacPointComponent : Component;

/// <summary>
/// Событие «Ассоциативный десантник» (план осады п. 28, решения автора 2026-11-20): в «Осаде» с вероятностью 35% за
/// раунд на 30-й минуте открывается набор двух призраков — десантника и ИИ его брони M.A.R.S. 1.5 (набор до 50-й
/// минуты); админ может открыть набор кнопкой. Десантник прыгает с парашютом в стороне от цели — офицера одной из
/// сторон на поверхности, — устраняет её и уходит к точке эвакуации. Кодекс Куратора: убивать только цель и тех,
/// кто напал сам (третье «лишнее» убийство — провал); не попадаться живым (2 минуты в наручниках — провал); 25 минут
/// на цель и 10 — на эвакуацию. Провал — ЗГА публично отрекается от оперативника.
/// </summary>
public sealed partial class DHZGAParatrooperSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private RandomHumanoidSystem _humanoid = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private DHLandingSystem _landing = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPinpointerSystem _pinpointer = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DHThirdPartySystem _thirdParty = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public const string EventId = "zga-paratrooper";
    private const string ParatrooperRole = "paratrooper";
    private const string MarsRole = "mars";

    public const float Chance = 0.35f;
    public static readonly TimeSpan OpenAt = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan CloseAt = TimeSpan.FromMinutes(50);
    private static readonly TimeSpan AdminRecruitTime = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan HuntTime = TimeSpan.FromMinutes(25);
    public static readonly TimeSpan EvacTime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan CaptureTime = TimeSpan.FromMinutes(2);
    public const int MaxExtraKills = 3;
    private const float EvacRadius = 2f;

    private const string ParatrooperSettings = "DHZGAParatrooper";
    private static readonly EntProtoId MarsProto = "DHMarsCore";
    private static readonly EntProtoId EvacProto = "DHZGAEvacPoint";
    private static readonly EntProtoId PinpointerProto = "DHZGAPinpointer";
    private static readonly EntProtoId StatusActionProto = "DHActionZGAStatus";

    /// <summary>Цели — командование сторон; нет никого на поверхности — командиры отрядов.</summary>
    private static readonly ProtoId<JobPrototype>[] CommandJobs =
    {
        "DHGarrisonCommander", "DHGarrisonOfficer", "DHGarrisonArtilleryOfficer",
        "DHCarrierOperationCommander", "DHCarrierCaptain", "DHCarrierSupportOfficer", "DHMarineCommander",
    };

    private static readonly ProtoId<JobPrototype>[] FallbackJobs = { "DHMarineSquadLeader", "DHGarrisonMortarCommander" };

    private static readonly Color ZgaColor = Color.FromHex("#7EC8F0");

    // Расписание события в текущем раунде.
    private bool _rolled;
    private TimeSpan? _scheduled;
    private bool _happened;

    /// <summary>Итог для сводки раунда.</summary>
    public DHZGAMissionState? Outcome { get; private set; }

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<DHZGAParatrooperComponent, DamageDealtEvent>(OnParatrooperDamaged);
        SubscribeLocalEvent<DHZGAParatrooperComponent, DHZGAStatusActionEvent>(OnParatrooperStatus);
        SubscribeLocalEvent<DHMarsComponent, DHZGAStatusActionEvent>(OnMarsStatus);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _rolled = false;
        _scheduled = null;
        _happened = false;
        Outcome = null;
    }

    #region Расписание и набор

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_ticker.RunLevel != GameRunLevel.InRound)
            return;

        var now = _ticker.RoundDuration();
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + TimeSpan.FromSeconds(1);

        if (!_rolled && _ticker.IsGameRuleActive<DHAssaultRuleComponent>())
        {
            _rolled = true;
            if (_random.Prob(Chance))
                _scheduled = OpenAt;
        }

        if (_scheduled is { } at && now >= at && !_happened && !_thirdParty.IsOpen(EventId))
        {
            _scheduled = null;
            OpenRecruitment(CloseAt);
        }

        UpdateMissions(now);
    }

    /// <summary>Открыть набор (по расписанию или админом).</summary>
    public bool OpenRecruitment(TimeSpan deadline, bool admin = false)
    {
        if (_happened || _thirdParty.IsOpen(EventId))
            return false;

        if (admin)
            deadline = _ticker.RoundDuration() + AdminRecruitTime;

        _thirdParty.Open(new DHThirdPartySystem.Recruitment
        {
            Id = EventId,
            Name = "dh-zga-event-name",
            Description = "dh-zga-event-desc",
            Roles = new()
            {
                (ParatrooperRole, "dh-zga-role-paratrooper", "dh-zga-role-paratrooper-desc"),
                (MarsRole, "dh-zga-role-mars", "dh-zga-role-mars-desc"),
            },
            Deadline = deadline,
            OnReady = Start,
        });
        return true;
    }

    public bool AdminOpen() => OpenRecruitment(TimeSpan.Zero, admin: true);

    #endregion

    #region Высадка

    private void Start(Dictionary<string, ICommonSession> players)
    {
        var paratrooperSession = players[ParatrooperRole];
        var marsSession = players[MarsRole];

        if (!TryFindPlanet(out var surface, out var atmosphere) ||
            !TryPickTarget(surface, out var target, out var targetJob))
        {
            var message = Loc.GetString("dh-zga-no-target");
            _chatManager.DispatchServerMessage(paratrooperSession, message);
            _chatManager.DispatchServerMessage(marsSession, message);
            return;
        }

        _happened = true;
        var targetPosition = _transform.GetWorldPosition(target);

        // Прыжок — в 45–70 клетках от цели; эвакуация — подальше от неё.
        var drop = _landing.KeepInsideMappedSurface(surface,
            targetPosition + _random.NextAngle().ToVec() * _random.NextFloat(45f, 70f));
        var evacPosition = FarPoint(surface, targetPosition);

        // Имя — по RandomMetadata в настройках (десантник ЗГА с военной фамилией).
        var paratrooper = _humanoid.SpawnRandomHumanoid(ParatrooperSettings, new EntityCoordinates(atmosphere, drop),
            Loc.GetString("dh-zga-role-paratrooper"));

        var mars = Spawn(MarsProto, new EntityCoordinates(paratrooper, Vector2.Zero));
        var evac = Spawn(EvacProto, new EntityCoordinates(surface, evacPosition));

        var comp = EnsureComp<DHZGAParatrooperComponent>(paratrooper);
        comp.Target = target;
        comp.TargetName = Name(target);
        comp.TargetJob = targetJob;
        comp.EvacPoint = evac;
        comp.Mars = mars;
        comp.LandedAt = _ticker.RoundDuration();

        var marsComp = EnsureComp<DHMarsComponent>(mars);
        marsComp.Host = paratrooper;

        // Пинпоинтер наводится на цель, после неё — на эвакуацию.
        var pinpointer = Spawn(PinpointerProto, Transform(paratrooper).Coordinates);
        _pinpointer.SetTarget(pinpointer, target);
        _pinpointer.SetActive(pinpointer, true);
        _hands.PickupOrDrop(paratrooper, pinpointer);
        comp.Pinpointer = pinpointer;

        _actions.AddAction(paratrooper, ref comp.StatusAction, StatusActionProto);
        _actions.AddAction(mars, ref marsComp.StatusAction, StatusActionProto);

        if (_mind.TryGetMind(paratrooperSession, out var paratrooperMind, out _))
            _mind.TransferTo(paratrooperMind, paratrooper, ghostCheckOverride: true);
        if (_mind.TryGetMind(marsSession, out var marsMind, out _))
            _mind.TransferTo(marsMind, mars, ghostCheckOverride: true);

        Briefing(paratrooperSession, comp, mars: false);
        Briefing(marsSession, comp, mars: true);
    }

    private bool TryFindPlanet(out EntityUid surface, out EntityUid atmosphere)
    {
        surface = default;
        atmosphere = default;
        var bases = EntityQueryEnumerator<DHPlanetBaseComponent>();
        while (bases.MoveNext(out _, out var planetBase))
        {
            if (_landing.GetSurface(planetBase.System, planetBase.Body) is not { } s ||
                _landing.EnsureAtmosphere(planetBase.System, planetBase.Body) is not { } a)
            {
                continue;
            }

            surface = s;
            atmosphere = a;
            return true;
        }

        return false;
    }

    /// <summary>Живой офицер на поверхности (игрок); нет — командир отряда.</summary>
    private bool TryPickTarget(EntityUid surface, out EntityUid target, out string job)
    {
        foreach (var jobs in new[] { CommandJobs, FallbackJobs })
        {
            var candidates = new List<(EntityUid Uid, string Job)>();
            var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var xform))
            {
                if (xform.MapUid != surface || !_mobState.IsAlive(uid) ||
                    !_mind.TryGetMind(uid, out var mindId, out _) ||
                    !_jobs.MindTryGetJob(mindId, out var jobProto) || !jobs.Any(j => j.Id == jobProto.ID))
                {
                    continue;
                }

                candidates.Add((uid, jobProto.LocalizedName));
            }

            if (candidates.Count == 0)
                continue;

            (target, job) = _random.Pick(candidates);
            return true;
        }

        target = default;
        job = string.Empty;
        return false;
    }

    private Vector2 FarPoint(EntityUid surface, Vector2 from)
    {
        var best = from;
        var bestDistance = -1f;
        for (var i = 0; i < 12; i++)
        {
            var point = _landing.KeepInsideMappedSurface(surface,
                from + _random.NextAngle().ToVec() * _random.NextFloat(90f, 160f));
            var distance = Vector2.Distance(point, from);
            if (distance <= bestDistance)
                continue;

            best = point;
            bestDistance = distance;
        }

        return best;
    }

    private void Briefing(ICommonSession session, DHZGAParatrooperComponent comp, bool mars)
    {
        _chatManager.DispatchServerMessage(session, Loc.GetString(mars ? "dh-zga-briefing-mars" : "dh-zga-briefing-paratrooper",
            ("target", comp.TargetName), ("job", comp.TargetJob)));
        _chatManager.DispatchServerMessage(session, Loc.GetString("dh-zga-codex",
            ("hunt", (int) HuntTime.TotalMinutes), ("evac", (int) EvacTime.TotalMinutes),
            ("kills", MaxExtraKills), ("capture", (int) CaptureTime.TotalMinutes)));
    }

    #endregion

    #region Задание и кодекс

    private void UpdateMissions(TimeSpan now)
    {
        var query = EntityQueryEnumerator<DHZGAParatrooperComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.State is not (DHZGAMissionState.Hunting or DHZGAMissionState.Evacuating))
                continue;

            // Кодекс: не попадаться живым.
            if (TryComp<CuffableComponent>(uid, out var cuffable) && cuffable.CuffedHandCount > 0 && !_mobState.IsDead(uid))
            {
                comp.CuffedSince ??= now;
                if (now - comp.CuffedSince >= CaptureTime)
                {
                    Disavow((uid, comp), "dh-zga-violation-captured");
                    continue;
                }
            }
            else
            {
                comp.CuffedSince = null;
            }

            if (comp.State == DHZGAMissionState.Hunting)
            {
                if (comp.Target is not { } target || TerminatingOrDeleted(target) || _mobState.IsDead(target))
                {
                    comp.State = DHZGAMissionState.Evacuating;
                    comp.TargetDownAt = now;
                    if (comp.Pinpointer is { } pinpointer && comp.EvacPoint is { } evac)
                        _pinpointer.SetTarget(pinpointer, evac);
                    Notify((uid, comp), Loc.GetString("dh-zga-target-down", ("evac", (int) EvacTime.TotalMinutes)));
                    continue;
                }

                if (now - comp.LandedAt >= HuntTime)
                    Disavow((uid, comp), "dh-zga-violation-hunt-time");
                continue;
            }

            // Эвакуация.
            if (comp.TargetDownAt is { } down && now - down >= EvacTime)
            {
                Disavow((uid, comp), "dh-zga-violation-evac-time");
                continue;
            }

            if (comp.EvacPoint is { } point && !_mobState.IsIncapacitated(uid) &&
                Transform(point).MapUid == xform.MapUid &&
                Vector2.Distance(_transform.GetWorldPosition(point), _transform.GetWorldPosition(xform)) <= EvacRadius)
            {
                Evacuate((uid, comp));
            }
        }
    }

    private void OnParatrooperDamaged(Entity<DHZGAParatrooperComponent> ent, ref DamageDealtEvent args)
    {
        if (args.Origin is { } origin && origin != ent.Owner && origin != ent.Comp.Mars)
            ent.Comp.Aggressors.Add(origin);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        // Погиб сам десантник — ИИ брони стирается вместе с ним.
        if (args.NewMobState == MobState.Dead && TryComp<DHZGAParatrooperComponent>(args.Target, out var self))
        {
            if (self.State is DHZGAMissionState.Hunting or DHZGAMissionState.Evacuating)
            {
                self.State = DHZGAMissionState.Dead;
                Outcome = DHZGAMissionState.Dead;
            }

            EraseMars((args.Target, self));
            return;
        }

        if (args.NewMobState != MobState.Dead || args.Origin is not { } killer)
            return;

        var query = EntityQueryEnumerator<DHZGAParatrooperComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (killer != uid && killer != comp.Mars ||
                comp.State is not (DHZGAMissionState.Hunting or DHZGAMissionState.Evacuating) ||
                args.Target == comp.Target || comp.Aggressors.Contains(args.Target))
            {
                continue;
            }

            comp.ExtraKills++;
            if (comp.ExtraKills >= MaxExtraKills)
            {
                Disavow((uid, comp), "dh-zga-violation-kills");
                continue;
            }

            Notify((uid, comp), Loc.GetString("dh-zga-extra-kill", ("count", comp.ExtraKills), ("max", MaxExtraKills)));
        }
    }

    /// <summary>Нарушение кодекса: ЗГА публично отрекается, дальше — сам по себе.</summary>
    private void Disavow(Entity<DHZGAParatrooperComponent> ent, LocId reason)
    {
        ent.Comp.State = DHZGAMissionState.Disavowed;
        Outcome = DHZGAMissionState.Disavowed;
        Notify(ent, Loc.GetString("dh-zga-disavowed-self", ("reason", Loc.GetString(reason))));
        _chat.DispatchGlobalAnnouncement(Loc.GetString("dh-zga-disavowed-announcement", ("name", Name(ent))),
            Loc.GetString("dh-zga-announcement-sender"), colorOverride: ZgaColor);
    }

    private void Evacuate(Entity<DHZGAParatrooperComponent> ent)
    {
        ent.Comp.State = DHZGAMissionState.Evacuated;
        Outcome = DHZGAMissionState.Evacuated;
        Notify(ent, Loc.GetString("dh-zga-evacuated"));
        _popup.PopupCoordinates(Loc.GetString("dh-zga-evacuated-others"), Transform(ent).Coordinates, PopupType.Medium);

        if (ent.Comp.EvacPoint is { } evac)
            QueueDel(evac);

        // Тело с бронёй уходит на эвакуацию; оба игрока становятся наблюдателями.
        QueueDel(ent.Comp.Mars);
        QueueDel(ent);
    }

    private void EraseMars(Entity<DHZGAParatrooperComponent> ent)
    {
        if (ent.Comp.Mars is not { } mars || TerminatingOrDeleted(mars))
            return;

        if (TryComp<ActorComponent>(mars, out var actor) && actor.PlayerSession is { } session)
            _chatManager.DispatchServerMessage(session, Loc.GetString("dh-zga-mars-erased"));

        ent.Comp.Mars = null;
        QueueDel(mars);
    }

    /// <summary>Сообщение десантнику и его ИИ.</summary>
    private void Notify(Entity<DHZGAParatrooperComponent> ent, string message)
    {
        foreach (var uid in new[] { ent.Owner, ent.Comp.Mars })
        {
            if (uid is { } who && TryComp<ActorComponent>(who, out var actor))
                _chatManager.DispatchServerMessage(actor.PlayerSession, message);
        }

        _popup.PopupEntity(message, ent, ent, PopupType.Medium);
    }

    private void OnParatrooperStatus(Entity<DHZGAParatrooperComponent> ent, ref DHZGAStatusActionEvent args)
    {
        args.Handled = true;
        SendStatus(ent, args.Performer);
    }

    private void OnMarsStatus(Entity<DHMarsComponent> ent, ref DHZGAStatusActionEvent args)
    {
        args.Handled = true;
        if (TryComp<DHZGAParatrooperComponent>(ent.Comp.Host, out var host))
            SendStatus((ent.Comp.Host.Value, host), args.Performer);
    }

    private void SendStatus(Entity<DHZGAParatrooperComponent> ent, EntityUid performer)
    {
        if (!TryComp<ActorComponent>(performer, out var actor) || actor.PlayerSession is not { } session)
            return;

        var now = _ticker.RoundDuration();
        var left = ent.Comp.State switch
        {
            DHZGAMissionState.Hunting => HuntTime - (now - ent.Comp.LandedAt),
            DHZGAMissionState.Evacuating => EvacTime - (now - (ent.Comp.TargetDownAt ?? now)),
            _ => TimeSpan.Zero,
        };

        _chatManager.DispatchServerMessage(session, Loc.GetString("dh-zga-status",
            ("state", Loc.GetString($"dh-zga-state-{ent.Comp.State.ToString().ToLowerInvariant()}")),
            ("target", ent.Comp.TargetName), ("job", ent.Comp.TargetJob),
            ("minutes", Math.Max(0, (int) left.TotalMinutes)), ("seconds", Math.Max(0, left.Seconds)),
            ("kills", ent.Comp.ExtraKills), ("max", MaxExtraKills)));
    }

    #endregion
}

/// <summary>Админ: открыть набор ассоциативного десантника сейчас (на 15 минут).</summary>
[Content.Server.Administration.AdminCommand(Content.Shared.Administration.AdminFlags.Admin)]
public sealed partial class DHCallZGAParatrooperCommand : Robust.Shared.Console.LocalizedEntityCommands
{
    [Dependency] private DHZGAParatrooperSystem _zga = default!;

    public override string Command => "dhcallzgaparatrooper";

    public override void Execute(Robust.Shared.Console.IConsoleShell shell, string argStr, string[] args)
    {
        if (_zga.AdminOpen())
            shell.WriteLine(Loc.GetString("dh-zga-admin-opened"));
        else
            shell.WriteError(Loc.GetString("dh-zga-admin-busy"));
    }
}
