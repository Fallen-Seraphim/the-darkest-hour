using System.Linq;
using System.Numerics;
using Content.Server._DarkestHour.Assault;
using Content.Server._DarkestHour.Medical.Implants;
using Content.Server._DarkestHour.Supply;
using Content.Server._DarkestHour.Shuttles.Rift;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Humanoid.Systems;
using Content.Server.Mind;
using Content.Shared._DarkestHour.ThirdParty;
using Content.Shared.Actions;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Medical.Implants;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Alert;
using Content.Shared._DarkestHour.Levels;
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
    /// <summary>Нарушил меры предосторожности — ЗГА отреклась, дальше сам по себе.</summary>
    Disavowed,
    /// <summary>Погиб.</summary>
    Dead,
    /// <summary>Взят в плен живым — сторона получила сведения и очки, ЗГА отреклась.</summary>
    Captured,
}

/// <summary>Ассоциативный десантник: цель, эвакуация, соблюдение мер предосторожности (план осады п. 28).</summary>
[RegisterComponent]
public sealed partial class DHZGAParatrooperComponent : Component
{
    [ViewVariables] public EntityUid? Target;
    [ViewVariables] public string TargetName = string.Empty;
    [ViewVariables] public string TargetJob = string.Empty;
    [ViewVariables] public EntityUid? EvacPoint;
    [ViewVariables] public EntityUid? Mars;
    [ViewVariables] public EntityUid? StatusAction;

    [ViewVariables] public DHZGAMissionState State = DHZGAMissionState.Hunting;
    [ViewVariables] public TimeSpan LandedAt;
    [ViewVariables] public TimeSpan? TargetDownAt;
    [ViewVariables] public TimeSpan? CuffedSince;

    /// <summary>Убийства не цели и не напавших (меры предосторожности: не больше <see cref="DHZGAParatrooperSystem.MaxExtraKills"/>).</summary>
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

    /// <summary>Способности (DHMarsSystem).</summary>
    [ViewVariables] public List<EntityUid> Actions = new();

    /// <summary>Детектор движения брони (дочерняя сущность M.A.R.S.).</summary>
    [ViewVariables] public EntityUid? Detector;

    /// <summary>Броня надета — ИИ работает.</summary>
    [ViewVariables] public bool Online = true;

    [ViewVariables] public EntityUid? Marked;
    [ViewVariables] public TimeSpan JamUntil;
    [ViewVariables] public EntityUid? HackTarget;
    [ViewVariables] public TimeSpan HackDone;
    [ViewVariables] public System.Numerics.Vector2? CarryTo;
    [ViewVariables] public TimeSpan CarryUntil;
}

/// <summary>Точка эвакуации десантника (невидимая метка, на неё указывает пинпоинтер).</summary>
[RegisterComponent]
public sealed partial class DHZGAEvacPointComponent : Component;

/// <summary>
/// Событие «Ассоциативный десантник» (план осады п. 28, решения автора 2026-11-20): в «Осаде» с вероятностью 35% за
/// раунд на 30-й минуте открывается набор двух призраков — десантника и ИИ его брони M.A.R.S. 1.5 (набор до 50-й
/// минуты); админ может открыть набор кнопкой. Десантник высаживается у случайного края карты (суша или мелководье),
/// трекер в алертах ведёт его к цели — офицеру одной из сторон на поверхности; устранив её, он уходит к точке
/// эвакуации. Меры предосторожности (решение автора 2026-11-20, игрокам — без цифр): не вмешиваться в бой сторон,
/// калечить и убивать только цель и сопротивляющихся (лишние жертвы — третья ведёт к провалу); не попадаться живым
/// (2 минуты в наручниках — провал); 25 минут на цель и 10 — на эвакуацию. Провал — ЗГА публично отрекается.
/// </summary>
public sealed class DHZGAParatrooperSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private DHAssaultRuleSystem _assault = default!;
    [Dependency] private DHCommandPointsSystem _commandPoints = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DHImplantSystem _implants = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private RandomHumanoidSystem _humanoid = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private DHLandingSystem _landing = default!;
    [Dependency] private DHMarsSystem _mars = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private DHZLevelSystem _zLevels = default!;
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
    private static readonly ProtoId<AlertPrototype> TrackerAlert = "DHZGATargetTracker";

    /// <summary>Ближе этого (клеток) — трекер показывает «рядом».</summary>
    private const float TrackerDirectRange = 1.5f;
    private static readonly EntProtoId StatusActionProto = "DHActionZGAStatus";
    private static readonly EntProtoId LethalImplantProto = "DHImplantZGALethal";
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";

    /// <summary>Очки командования стороне, взявшей десантника живым (решение по умолчанию: 60 — полстартового бюджета).</summary>
    public const float CapturePoints = 60f;

    /// <summary>Кто рядом с пленным, тот и взял его (клетки).</summary>
    private const float CaptureRadius = 4f;

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

    // Для сводки раунда (тело к концу раунда может уже исчезнуть).
    private string? _summaryName;
    private string _summaryTarget = string.Empty;
    private string _summaryJob = string.Empty;
    private int _summaryKills;
    private DHAssaultFaction? _capturedBy;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<DHZGAParatrooperComponent, DamageDealtEvent>(OnParatrooperDamaged);
        SubscribeLocalEvent<DHZGAParatrooperComponent, DHZGAStatusActionEvent>(OnParatrooperStatus);
        SubscribeLocalEvent<DHMarsComponent, DHZGAStatusActionEvent>(OnMarsStatus);
        SubscribeLocalEvent<DHImplantComponent, DHZGALethalActionEvent>(OnLethal);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _rolled = false;
        _scheduled = null;
        _happened = false;
        Outcome = null;
        _summaryName = null;
        _summaryKills = 0;
        _capturedBy = null;
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

        if (!TryFindPlanet(out var surface, out _) ||
            !TryPickTarget(surface, out var target, out var targetJob))
        {
            var message = Loc.GetString("dh-zga-no-target");
            _chatManager.DispatchServerMessage(paratrooperSession, message);
            _chatManager.DispatchServerMessage(marsSession, message);
            return;
        }

        _happened = true;
        var targetPosition = _transform.GetWorldPosition(target);

        // Высадка — у случайного края карты, на суше или мелководье (решение автора 2026-11-20); эвакуация —
        // подальше от цели.
        var drop = _landing.RandomEdgePoint(surface) ??
                   _landing.KeepInsideMappedSurface(surface, targetPosition + _random.NextAngle().ToVec() * 200f);
        var evacPosition = FarPoint(surface, targetPosition);

        // Имя — по RandomMetadata в настройках (десантник ЗГА с военной фамилией).
        var paratrooper = _humanoid.SpawnRandomHumanoid(ParatrooperSettings, new EntityCoordinates(surface, drop),
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

        // Трекер в алертах (как у бойцов отряда на командира) показывает направление на цель.

        _actions.AddAction(paratrooper, ref comp.StatusAction, StatusActionProto);
        _actions.AddAction(mars, ref marsComp.StatusAction, StatusActionProto);
        _mars.Setup((mars, marsComp), paratrooper, target);

        // Летальная инъекция — имплант в торсе (на основе базового).
        _implants.Install(paratrooper, Spawn(LethalImplantProto, Transform(paratrooper).Coordinates));

        _summaryName = Name(paratrooper);
        _summaryTarget = comp.TargetName;
        _summaryJob = comp.TargetJob;
        _summaryKills = 0;
        _capturedBy = null;

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
        _chatManager.DispatchServerMessage(session, Loc.GetString("dh-zga-precautions"));
    }

    #endregion

    #region Задание и меры предосторожности

    private void UpdateMissions(TimeSpan now)
    {
        var query = EntityQueryEnumerator<DHZGAParatrooperComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.State is not (DHZGAMissionState.Hunting or DHZGAMissionState.Evacuating))
            {
                _alerts.ClearAlert(uid, TrackerAlert);
                continue;
            }

            UpdateTracker(uid, comp);

            // Меры предосторожности: не попадаться живым.
            if (TryComp<CuffableComponent>(uid, out var cuffable) && cuffable.CuffedHandCount > 0 && !_mobState.IsDead(uid))
            {
                comp.CuffedSince ??= now;
                if (now - comp.CuffedSince >= CaptureTime)
                {
                    Capture((uid, comp));
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

    /// <summary>
    /// Трекер в алертах, как у бойцов отряда на командира (решение автора 2026-11-20): стрелка на цель, после её
    /// устранения — на точку эвакуации. Другая карта (не та же Z-стопка) — трекер погашен.
    /// </summary>
    private void UpdateTracker(EntityUid uid, DHZGAParatrooperComponent comp)
    {
        var goal = comp.State == DHZGAMissionState.Hunting ? comp.Target : comp.EvacPoint;
        short severity = 0;
        if (goal is { } g && !TerminatingOrDeleted(g) && Locate(uid) is { } self && Locate(g) is { } there &&
            (self.Map == there.Map || self.Stack != null && self.Stack == there.Stack))
        {
            severity = TrackerDirection(there.Position - self.Position);
        }

        _alerts.ShowAlert(uid, TrackerAlert, severity);
    }

    private (EntityUid Map, int? Stack, Vector2 Position)? Locate(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.MapUid is not { } map)
            return null;

        int? stack = _zLevels.TryGetLevel(map, out var level) ? level.Stack : null;
        return (map, stack, _transform.GetWorldPosition(xform));
    }

    /// <summary>1 — рядом; 2–9 — юг, юго-восток, восток, северо-восток, север, северо-запад, запад, юго-запад.</summary>
    private static short TrackerDirection(Vector2 delta)
    {
        if (delta.Length() <= TrackerDirectRange)
            return 1;

        var angle = MathF.Atan2(delta.X, -delta.Y);
        if (angle < 0)
            angle += MathF.Tau;

        return (short) (2 + (int) MathF.Round(angle / (MathF.PI / 4)) % 8);
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
            _summaryKills = comp.ExtraKills;
            if (comp.ExtraKills >= MaxExtraKills)
            {
                Disavow((uid, comp), "dh-zga-violation-kills");
                continue;
            }

            Notify((uid, comp), Loc.GetString("dh-zga-extra-kill"));
        }
    }

    /// <summary>
    /// Пленён живым (2 минуты в наручниках): ЗГА отрекается, а сторона тех, кто рядом, получает сведения о задании и
    /// очки командования.
    /// </summary>
    private void Capture(Entity<DHZGAParatrooperComponent> ent)
    {
        Disavow(ent, "dh-zga-violation-captured");
        ent.Comp.State = DHZGAMissionState.Captured;
        Outcome = DHZGAMissionState.Captured;

        if (CaptorFaction(ent) is not { } faction)
            return;

        _capturedBy = faction;
        _commandPoints.Add(faction, CapturePoints);
        _assault.AnnounceToFaction(faction, Loc.GetString("dh-zga-intel-sender"),
            Loc.GetString("dh-zga-intel", ("name", Name(ent)), ("target", ent.Comp.TargetName),
                ("job", ent.Comp.TargetJob), ("points", (int) CapturePoints)),
            color: ZgaColor, onScreen: true);
    }

    /// <summary>Сторона ближайшего живого бойца рядом с пленным.</summary>
    private DHAssaultFaction? CaptorFaction(EntityUid captive)
    {
        var center = _transform.GetMapCoordinates(captive);
        DHAssaultFaction? best = null;
        var bestDistance = float.MaxValue;
        foreach (var fighter in _lookup.GetEntitiesInRange<DHAssaultFighterComponent>(center, CaptureRadius))
        {
            if (fighter.Owner == captive || !_mobState.IsAlive(fighter))
                continue;

            var distance = (_transform.GetMapCoordinates(fighter).Position - center.Position).Length();
            if (distance >= bestDistance)
                continue;

            best = fighter.Comp.Faction;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>Летальная инъекция: десантник умирает сразу — живым его не взять.</summary>
    private void OnLethal(Entity<DHImplantComponent> ent, ref DHZGALethalActionEvent args)
    {
        if (args.Handled || ent.Comp.Host is not { } host || _mobState.IsDead(host))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("dh-zga-lethal-others", ("name", Name(host))), host,
            Robust.Shared.Player.Filter.PvsExcept(host), true, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("dh-zga-lethal-self"), host, host, PopupType.LargeCaution);
        _damageable.TryChangeDamage(host, new DamageSpecifier { DamageDict = { [PoisonType] = 500 } },
            ignoreResistances: true, origin: host);
    }

    private void OnRoundEndText(ref RoundEndTextAppendEvent args)
    {
        if (_summaryName == null)
            return;

        var outcome = Outcome ?? DHZGAMissionState.Hunting;
        args.AddLine(string.Empty);
        args.AddLine(Loc.GetString("dh-zga-summary-title"));
        args.AddLine(Loc.GetString("dh-zga-summary-line", ("name", _summaryName), ("target", _summaryTarget),
            ("job", _summaryJob)));
        args.AddLine(Loc.GetString("dh-zga-summary-outcome",
            ("outcome", Loc.GetString($"dh-zga-outcome-{outcome.ToString().ToLowerInvariant()}")),
            ("kills", _summaryKills)));
        if (_capturedBy is { } faction)
        {
            args.AddLine(Loc.GetString("dh-zga-summary-captured",
                ("faction", Loc.GetString($"dh-zga-faction-{faction.ToString().ToLowerInvariant()}"))));
        }
    }

    /// <summary>Нарушение мер предосторожности: ЗГА публично отрекается, дальше — сам по себе.</summary>
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
            ("kills", ent.Comp.ExtraKills)));
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
