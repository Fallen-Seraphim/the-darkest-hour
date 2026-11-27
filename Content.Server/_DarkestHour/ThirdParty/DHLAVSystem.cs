using System.Linq;
using System.Numerics;
using Content.Server._DarkestHour.Assault;
using Content.Server._DarkestHour.Shuttles.Rift;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Humanoid.Systems;
using Content.Server.Mind;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.ControlPoints;
using Content.Shared._DarkestHour.Flags;
using Content.Shared._DarkestHour.Levels;
using Content.Shared._DarkestHour.ThirdParty;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Antag;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._DarkestHour.ThirdParty;

/// <summary>Задание отряда LA-V.</summary>
public enum DHLAVTargetKind : byte
{
    /// <summary>Удержать объект до конца операции.</summary>
    Hold,

    /// <summary>Вывезти экспериментальный образец на шаттле.</summary>
    Sample,
}

/// <summary>Итог операции LA-V.</summary>
public enum DHLAVOutcome : byte
{
    /// <summary>Идёт.</summary>
    Active,

    /// <summary>Объект удержан до конца операции.</summary>
    Held,

    /// <summary>Образец вывезен.</summary>
    Stolen,

    /// <summary>Шаттл ушёл, задание не выполнено.</summary>
    Withdrew,

    /// <summary>Объект не удержан (к концу операции у цели никого) или образец не вывезен.</summary>
    Failed,

    /// <summary>Отряд уничтожен.</summary>
    Wiped,
}

/// <summary>Боец отряда LA-V (событие «Осады»).</summary>
[RegisterComponent]
public sealed partial class DHLAVMemberComponent : Component
{
    [ViewVariables] public string Role = string.Empty;
    [ViewVariables] public EntityUid? StatusAction;
}

/// <summary>
/// Событие «ЧВК LA-V» (план осады п. 11, решения автора 2026-11-26). В «Осаде» с вероятностью 35% на 40-й минуте
/// (если в раунде ещё не было события третьей стороны) открывается набор отряда из призраков: командир, заместитель,
/// боевой техник, снайпер-разведчик, медик и пять бойцов; отряд выходит, когда записалось не меньше шести. Шаттл
/// «Vanguard Runner» (порт шаттла PMC из RMC-14) садится на свободную ЗП, бойцы появляются на борту с фиксированным
/// снаряжением по ролям. Задание знает только LA-V: удержать до конца операции случайный объект (деревня, стройка,
/// АЭС, космопорт, плотина, танкер) или вывезти образец на своём шаттле. Эвакуация — пульт шаттла.
/// </summary>
public sealed partial class DHLAVSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private DHAssaultRuleSystem _assault = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private Content.Shared.Access.Systems.SharedIdCardSystem _idCard = default!;
    [Dependency] private Content.Shared.Inventory.InventorySystem _inventory = default!;
    [Dependency] private RandomHumanoidSystem _humanoid = default!;
    [Dependency] private DHLandingSystem _landing = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DHRiftSystem _rift = default!;
    [Dependency] private Content.Server._DarkestHour.Chat.DHSpeechColorSystem _speechColor = default!;
    [Dependency] private DHThirdPartySystem _thirdParty = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DHZLevelSystem _zLevels = default!;

    public const string EventId = "lav";

    public const float Chance = 0.35f;
    public static readonly TimeSpan OpenAt = TimeSpan.FromMinutes(40);
    private static readonly TimeSpan RecruitTime = TimeSpan.FromMinutes(20);

    /// <summary>Минимум набрался — столько ждём остальных, пока места не заполнятся.</summary>
    private static readonly TimeSpan GatherTime = TimeSpan.FromSeconds(90);

    public const int MinSquad = 6;

    private static readonly ResPath ShuttlePath = new("/Maps/_DarkestHour/Shuttles/lav_shuttle.yml");
    private static readonly ProtoId<AlertPrototype> TrackerAlert = "DHLAVTargetTracker";
    private static readonly EntProtoId StatusActionProto = "DHActionLAVStatus";
    /// <summary>Сколько шаттл ждёт в атмосфере выбора ЗП, прежде чем сесть сам.</summary>
    private static readonly TimeSpan AutoLandTime = TimeSpan.FromMinutes(5);

    /// <summary>Снижение от команды до посадки.</summary>
    private static readonly TimeSpan DescentTime = TimeSpan.FromSeconds(10);

    private static readonly VerbCategory LandCategory = new("dh-lav-verb-land", null);

    private static readonly SoundSpecifier LandSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_end.ogg");
    private static readonly SoundSpecifier LaunchSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_begin.ogg");
    private static readonly Color LavColor = Color.FromHex("#8A6AD0");

    /// <summary>Объекты для удержания — мирные точки контроля (военные — ПКО, ПВО, РЛС — не задание ЧВК).</summary>
    private static readonly string[] HoldPoints = { "village", "factory", "terminal", "npp", "spaceport", "dam" };

    /// <summary>Танкер — ЗП с этим словом в имени.</summary>
    private const string TankerZoneWord = "Танкер";
    private const float TankerRadius = 12f;
    private const float TrackerDirectRange = 1.5f;

    /// <summary>Роли: id, локаль, мест, настройки персонажа.</summary>
    private static readonly (string Id, int Slots, string Settings)[] SquadRoles =
    {
        ("commander", 1, "DHLAVSquadCommander"),
        ("deputy", 1, "DHLAVSquadDeputy"),
        ("technician", 1, "DHLAVSquadTechnician"),
        ("sniper", 1, "DHLAVSquadSniper"),
        ("medic", 1, "DHLAVSquadMedic"),
        ("operative", 5, "DHLAVSquadOperative"),
    };

    private bool _rolled;
    private TimeSpan? _scheduled;
    private bool _happened;
    private TimeSpan _nextUpdate;

    private EntityUid? _shuttle;
    private EntityUid? _surface;

    /// <summary>Шаттл ещё в атмосфере (до посадки).</summary>
    private bool _inAtmosphere;

    /// <summary>Снижение начато: когда и куда садимся.</summary>
    private TimeSpan? _landAt;
    private EntityUid? _landZone;

    /// <summary>Не выбрали ЗП — автопилот садится сам.</summary>
    private TimeSpan _autoLandAt;
    private bool _autoLandWarned;

    private DHLAVTargetKind _kind;
    private EntityUid? _target;
    private Vector2 _targetPosition;
    private EntityUid? _targetMap;
    private float _targetRadius;
    private string _targetName = string.Empty;
    private string _zoneName = string.Empty;

    public DHLAVOutcome? Outcome { get; private set; }
    private readonly List<string> _summaryNames = new();
    private int _evacuated;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<DHLAVMemberComponent, DHLAVStatusActionEvent>(OnStatus);
        SubscribeLocalEvent<DHLAVShuttleConsoleComponent, ActivateInWorldEvent>(OnConsoleActivate);
        SubscribeLocalEvent<DHLAVShuttleConsoleComponent, DHLAVEvacDoAfterEvent>(OnEvacDoAfter);
        SubscribeLocalEvent<DHLAVShuttleConsoleComponent, GetVerbsEvent<Verb>>(OnConsoleVerbs);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _rolled = false;
        _scheduled = null;
        _happened = false;
        _shuttle = null;
        _surface = null;
        _inAtmosphere = false;
        _landAt = null;
        _landZone = null;
        _target = null;
        _targetMap = null;
        Outcome = null;
        _summaryNames.Clear();
        _evacuated = 0;
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

        // Одно событие третьей стороны за раунд: пока открыт набор ЗГА — ждём, событие было — не открываем.
        if (_scheduled is { } at && now >= at && !_happened && !_thirdParty.AnyOpen)
        {
            _scheduled = null;
            if (_thirdParty.Happened == null)
                OpenRecruitment(now + RecruitTime);
        }

        if (Outcome == DHLAVOutcome.Active)
        {
            UpdateSquad();
            UpdateLanding(now);
        }
    }

    public bool OpenRecruitment(TimeSpan deadline, int min = MinSquad)
    {
        if (_happened || _thirdParty.IsOpen(EventId) || _thirdParty.Happened is { } other && other != EventId)
            return false;

        _thirdParty.Open(new DHThirdPartySystem.Recruitment
        {
            Id = EventId,
            Name = "dh-lav-event-name",
            Description = "dh-lav-event-desc",
            Roles = SquadRoles.Select(r => (r.Id, (LocId) $"dh-lav-role-{r.Id}", (LocId) $"dh-lav-role-{r.Id}-desc")).ToList(),
            Slots = SquadRoles.ToDictionary(r => r.Id, r => r.Slots),
            MinTotal = Math.Clamp(min, 1, SquadRoles.Sum(r => r.Slots)),
            Gather = GatherTime,
            Deadline = deadline,
            OnReadySquad = Start,
        });
        return true;
    }

    public bool AdminOpen(int min) => OpenRecruitment(_ticker.RoundDuration() + TimeSpan.FromMinutes(15), min);

    #endregion

    #region Высадка

    private void Start(List<(string Role, ICommonSession Session)> squad)
    {
        if (!TryFindSurface(out var surface, out var atmosphere) || !TryPickTarget(surface))
        {
            Fail(squad, "dh-lav-no-target");
            return;
        }

        if (!TryDeployShuttle(surface, atmosphere, out var shuttle))
        {
            Fail(squad, "dh-lav-no-zone");
            return;
        }

        if (!_thirdParty.Claim(EventId))
        {
            QueueDel(shuttle);
            return;
        }

        _happened = true;
        _shuttle = shuttle;
        _surface = surface;
        _inAtmosphere = true;
        _landAt = null;
        _landZone = null;
        _autoLandAt = _ticker.RoundDuration() + AutoLandTime;
        _autoLandWarned = false;
        _zoneName = Loc.GetString("dh-lav-zone-atmosphere");
        Outcome = DHLAVOutcome.Active;
        _summaryNames.Clear();
        _evacuated = 0;

        // Места: командир — на месте командира, остальные — по меткам вразнобой.
        var leaderSpots = new List<EntityCoordinates>();
        var spots = new List<EntityCoordinates>();
        var markers = EntityQueryEnumerator<DHLAVSpawnPointComponent, TransformComponent>();
        var toDelete = new List<EntityUid>();
        while (markers.MoveNext(out var uid, out var marker, out var xform))
        {
            if (xform.GridUid != shuttle)
                continue;

            (marker.Leader ? leaderSpots : spots).Add(xform.Coordinates);
            toDelete.Add(uid);
        }

        foreach (var uid in toDelete)
        {
            QueueDel(uid);
        }

        _random.Shuffle(spots);
        var fallback = new EntityCoordinates(shuttle, Vector2.Zero);
        var spotIndex = 0;

        // Командир первым; нет командира — старший по списку ролей.
        var ordered = squad.OrderBy(m => Array.FindIndex(SquadRoles, r => r.Id == m.Role)).ToList();
        foreach (var (role, session) in ordered)
        {
            EntityCoordinates coords;
            if (role == "commander" && leaderSpots.Count > 0)
                coords = leaderSpots[0];
            else if (spots.Count > 0)
                coords = spots[spotIndex++ % spots.Count];
            else
                coords = leaderSpots.Count > 0 ? leaderSpots[0] : fallback;

            var settings = SquadRoles.First(r => r.Id == role).Settings;
            var mob = _humanoid.SpawnRandomHumanoid(settings, coords, Loc.GetString($"dh-lav-role-{role}"));
            var member = EnsureComp<DHLAVMemberComponent>(mob);
            member.Role = role;
            _actions.AddAction(mob, ref member.StatusAction, StatusActionProto);
            _speechColor.SetColor(mob, Content.Shared._DarkestHour.Chat.DHSpeechColors.Vanguard);

            // Жетоны: имя бойца (должность и доступ — из PresetIdCard).
            if (_inventory.TryGetSlotEntity(mob, "id", out var tags))
                _idCard.TryChangeFullName(tags.Value, Name(mob));

            if (_mind.TryGetMind(session, out var mind, out _))
                _mind.TransferTo(mind, mob, ghostCheckOverride: true);

            _summaryNames.Add(Loc.GetString("dh-lav-summary-member", ("name", Name(mob)),
                ("role", Loc.GetString($"dh-lav-role-{role}"))));

            _antag.SendBriefing(session, Loc.GetString(BriefingKey(role), ("target", _targetName),
                ("minutes", (int) AutoLandTime.TotalMinutes), ("radius", (int) TargetRadius())), LavColor, null);
        }
    }

    private string BriefingKey(string role)
    {
        var kind = _kind == DHLAVTargetKind.Sample ? "sample" : "hold";
        return role == "commander" ? $"dh-lav-briefing-commander-{kind}" : $"dh-lav-briefing-{kind}";
    }

    private void Fail(List<(string Role, ICommonSession Session)> squad, LocId reason)
    {
        var message = Loc.GetString(reason);
        foreach (var (_, session) in squad)
        {
            _chatManager.DispatchServerMessage(session, message);
        }
    }

    private bool TryFindSurface(out EntityUid surface, out EntityUid atmosphere)
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

    /// <summary>Случайная цель: мирная точка контроля, танкер или образец (если он ещё на планете).</summary>
    private bool TryPickTarget(EntityUid surface)
    {
        var candidates = new List<(DHLAVTargetKind Kind, EntityUid Uid, string Name, float Radius)>();

        var points = EntityQueryEnumerator<DHControlPointComponent, TransformComponent>();
        while (points.MoveNext(out var uid, out var point, out var xform))
        {
            if (xform.MapUid == surface && HoldPoints.Contains(point.PointId))
                candidates.Add((DHLAVTargetKind.Hold, uid, Loc.GetString(point.PointName), point.Radius));
        }

        var zones = EntityQueryEnumerator<DHLandingZoneComponent, TransformComponent>();
        while (zones.MoveNext(out var uid, out var zone, out var xform))
        {
            if (xform.MapUid == surface && zone.ZoneName.Contains(TankerZoneWord, StringComparison.OrdinalIgnoreCase))
                candidates.Add((DHLAVTargetKind.Hold, uid, Loc.GetString("dh-lav-target-tanker"), TankerRadius));
        }

        var samples = EntityQueryEnumerator<DHExperimentalSampleComponent, TransformComponent>();
        while (samples.MoveNext(out var uid, out _, out var xform))
        {
            if (OnSurfaceStack(xform.MapUid, surface))
                candidates.Add((DHLAVTargetKind.Sample, uid, Loc.GetString("dh-lav-target-sample"), 0f));
        }

        if (candidates.Count == 0)
            return false;

        var pick = _random.Pick(candidates);
        _kind = pick.Kind;
        _target = pick.Uid;
        _targetName = pick.Name;
        _targetRadius = pick.Radius;
        _targetMap = Transform(pick.Uid).MapUid;
        _targetPosition = _transform.GetWorldPosition(pick.Uid);
        return true;
    }

    /// <summary>Карта — поверхность или уровень той же Z-стопки (бункер, этажи).</summary>
    private bool OnSurfaceStack(EntityUid? map, EntityUid surface)
    {
        if (map == null)
            return false;

        if (map == surface)
            return true;

        return _zLevels.TryGetLevel(map.Value, out var level) && _zLevels.TryGetLevel(surface, out var surfaceLevel) &&
               level.Stack == surfaceLevel.Stack;
    }

    /// <summary>
    /// Шаттл грузится на временную карту и выходит в слой атмосферы над районом операции — над случайной ЗП
    /// (решение автора 2026-11-26: отряд готовится, посадку выбирают на пульте).
    /// </summary>
    private bool TryDeployShuttle(EntityUid surface, EntityUid atmosphere, out EntityUid shuttle)
    {
        shuttle = default;
        var tempMap = _map.CreateMap(out var tempId);
        if (!_loader.TryLoadGrid(tempId, ShuttlePath, out var grid))
        {
            Log.Error($"LA-V: failed to load the shuttle {ShuttlePath}");
            QueueDel(tempMap);
            return false;
        }

        // Над ЗП, а если там тесно (авианосец в атмосфере) — в стороне.
        var points = FreeZones(grid.Value, surface).Select(z => z.Position).ToList();
        _random.Shuffle(points);
        if (points.Count == 0 && _landing.RandomEdgePoint(surface) is { } edge)
            points.Add(edge);
        Vector2? position = null;
        foreach (var point in points)
        {
            for (var attempt = 0; attempt < 6 && position == null; attempt++)
            {
                var candidate = point + (attempt == 0 ? Vector2.Zero : _random.NextAngle().ToVec() * 60f * attempt);
                if (_rift.ExitFree(grid.Value, new EntityCoordinates(atmosphere, candidate)))
                    position = candidate;
            }

            if (position != null)
                break;
        }

        if (position == null)
        {
            QueueDel(tempMap);
            return false;
        }

        shuttle = grid.Value;
        _meta.SetEntityName(shuttle, Loc.GetString("dh-lav-shuttle-name"));
        _landing.PlaceShuttle(shuttle, new EntityCoordinates(atmosphere, position.Value));
        QueueDel(tempMap);
        return true;
    }

    /// <summary>Свободные ЗП на поверхности для этого шаттла.</summary>
    private List<(EntityUid Zone, string Name, Vector2 Position)> FreeZones(EntityUid shuttle, EntityUid surface)
    {
        var zones = new List<(EntityUid, string, Vector2)>();
        var query = EntityQueryEnumerator<DHLandingZoneComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var zone, out var xform))
        {
            if (xform.MapUid != surface || zone.Temporary || !_rift.ExitFree(shuttle, xform.Coordinates))
                continue;

            zones.Add((uid, Name(uid), _transform.GetWorldPosition(xform)));
        }

        return zones;
    }

    #region Посадка

    private void OnConsoleVerbs(Entity<DHLAVShuttleConsoleComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<DHLAVMemberComponent>(args.User) ||
            !_inAtmosphere || _landAt != null || Outcome != DHLAVOutcome.Active ||
            Transform(ent).GridUid is not { } shuttle || shuttle != _shuttle || _surface is not { } surface)
        {
            return;
        }

        var user = args.User;
        var priority = 0;
        foreach (var (zone, name, _) in FreeZones(shuttle, surface))
        {
            var target = zone;
            args.Verbs.Add(new Verb
            {
                Text = name,
                Category = LandCategory,
                Priority = priority--,
                Act = () => BeginLanding(target, user),
            });
        }
    }

    private void BeginLanding(EntityUid? zone, EntityUid? user)
    {
        if (!_inAtmosphere || _landAt != null || _shuttle is not { } shuttle)
            return;

        _landZone = zone;
        _landAt = _ticker.RoundDuration() + DescentTime;
        var name = zone is { } z && Exists(z) ? Name(z) : Loc.GetString("dh-lav-zone-edge");
        NotifySquad(Loc.GetString("dh-lav-descent", ("zone", name), ("seconds", (int) DescentTime.TotalSeconds)));
        _landing.Shake(shuttle, 0.4f);
        if (user != null)
            Log.Info($"LA-V: {ToPrettyString(user.Value)} started the descent to {name}");
    }

    private void UpdateLanding(TimeSpan now)
    {
        if (!_inAtmosphere || _shuttle is not { } shuttle || _surface is not { } surface)
            return;

        if (_landAt is { } at)
        {
            if (now >= at)
                Land(shuttle, surface);
            else if ((int) (at - now).TotalSeconds % 3 == 0)
                _landing.Shake(shuttle, 0.4f);
            return;
        }

        if (!_autoLandWarned && now >= _autoLandAt - TimeSpan.FromMinutes(1))
        {
            _autoLandWarned = true;
            NotifySquad(Loc.GetString("dh-lav-autoland-warning"));
        }

        if (now < _autoLandAt)
            return;

        var zones = FreeZones(shuttle, surface);
        BeginLanding(zones.Count > 0 ? _random.Pick(zones).Zone : null, null);
    }

    /// <summary>Посадка: на выбранную ЗП, занята — на другую свободную, нет свободных — у края карты.</summary>
    private void Land(EntityUid shuttle, EntityUid surface)
    {
        _landAt = null;
        Vector2? position = null;
        if (_landZone is { } zone && Exists(zone) && _rift.ExitFree(shuttle, Transform(zone).Coordinates))
        {
            position = _transform.GetWorldPosition(zone);
            _zoneName = Name(zone);
        }
        else if (FreeZones(shuttle, surface) is { Count: > 0 } zones)
        {
            var pick = _random.Pick(zones);
            position = pick.Position;
            _zoneName = pick.Name;
        }
        else if (_landing.RandomEdgePoint(surface) is { } edge &&
                 _rift.ExitFree(shuttle, new EntityCoordinates(surface, edge)))
        {
            position = edge;
            _zoneName = Loc.GetString("dh-lav-zone-edge");
        }

        if (position == null)
        {
            // Некуда — ждём и пробуем снова.
            _autoLandAt = _ticker.RoundDuration() + TimeSpan.FromSeconds(30);
            NotifySquad(Loc.GetString("dh-lav-descent-blocked"));
            return;
        }

        _inAtmosphere = false;
        _landing.PlaceShuttle(shuttle, new EntityCoordinates(surface, position.Value));
        _landing.Shake(shuttle, 1f);
        _audio.PlayPvs(LandSound, shuttle);
        NotifySquad(Loc.GetString("dh-lav-landed", ("zone", _zoneName)));
        _chat.DispatchGlobalAnnouncement(Loc.GetString("dh-lav-landed-announcement", ("zone", _zoneName)),
            Loc.GetString("dh-lav-announcement-sender"), true, null, LavColor);
    }

    /// <summary>Сообщение всем бойцам отряда.</summary>
    private void NotifySquad(string message)
    {
        var query = EntityQueryEnumerator<DHLAVMemberComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out _, out var actor))
        {
            _chatManager.DispatchServerMessage(actor.PlayerSession, message);
            _popup.PopupEntity(message, uid, uid, PopupType.Medium);
        }
    }

    #endregion

    #endregion

    #region Операция

    private void UpdateSquad()
    {
        var alive = 0;
        var query = EntityQueryEnumerator<DHLAVMemberComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_mobState.IsDead(uid))
            {
                _alerts.ClearAlert(uid, TrackerAlert);
                continue;
            }

            alive++;
            UpdateTracker(uid);
        }

        if (alive == 0)
        {
            Outcome = DHLAVOutcome.Wiped;
            _chat.DispatchGlobalAnnouncement(Loc.GetString("dh-lav-wiped-announcement"),
                Loc.GetString("dh-lav-announcement-sender"), true, null, LavColor);
        }
    }

    private (EntityUid Map, int? Stack, Vector2 Position)? TargetLocation()
    {
        if (_target is { } target && !TerminatingOrDeleted(target))
            return Locate(target);

        return _targetMap is { } map && Exists(map) ? (map, StackOf(map), _targetPosition) : null;
    }

    private void UpdateTracker(EntityUid uid)
    {
        short severity = 0;
        if (Locate(uid) is { } self && TargetLocation() is { } there &&
            (self.Map == there.Map || self.Stack != null && self.Stack == there.Stack))
        {
            severity = TrackerDirection(there.Position - self.Position);
        }

        _alerts.ShowAlert(uid, TrackerAlert, severity);
    }

    private int? StackOf(EntityUid map) => _zLevels.TryGetLevel(map, out var level) ? level.Stack : null;

    private (EntityUid Map, int? Stack, Vector2 Position)? Locate(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.MapUid is not { } map)
            return null;

        return (map, StackOf(map), _transform.GetWorldPosition(xform));
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

    private void OnStatus(Entity<DHLAVMemberComponent> ent, ref DHLAVStatusActionEvent args)
    {
        args.Handled = true;
        if (!TryComp<ActorComponent>(args.Performer, out var actor) || StatusText() is not { } status)
            return;

        _chatManager.DispatchServerMessage(actor.PlayerSession, status);
    }

    /// <summary>Задание, где шаттл и как идёт операция; пусто — события нет. Для «Задания» и планшета командира.</summary>
    public string? StatusText()
    {
        if (Outcome == null)
            return null;

        var task = Loc.GetString(_kind == DHLAVTargetKind.Sample ? "dh-lav-task-sample" : "dh-lav-task-hold",
            ("target", _targetName), ("radius", (int) TargetRadius()));
        if (_kind == DHLAVTargetKind.Hold && Outcome == DHLAVOutcome.Active)
            task += " " + Loc.GetString(Holding() ? "dh-lav-flag-standing" : "dh-lav-flag-missing");
        var state = Loc.GetString($"dh-lav-outcome-{(Outcome ?? DHLAVOutcome.Active).ToString().ToLowerInvariant()}");
        return Loc.GetString("dh-lav-status", ("task", task), ("zone", _zoneName), ("state", state));
    }

    #endregion

    #region Эвакуация

    private void OnConsoleActivate(Entity<DHLAVShuttleConsoleComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        if (!HasComp<DHLAVMemberComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("dh-lav-console-denied"), ent, args.User);
            return;
        }

        if (Outcome != DHLAVOutcome.Active || Transform(ent).GridUid != _shuttle)
        {
            _popup.PopupEntity(Loc.GetString("dh-lav-console-offline"), ent, args.User);
            return;
        }

        // В атмосфере пульт сажает шаттл: выбор ЗП — в контекстном меню.
        if (_inAtmosphere)
        {
            _popup.PopupEntity(Loc.GetString(_landAt != null ? "dh-lav-console-descending" : "dh-lav-console-choose-zone"),
                ent, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("dh-lav-console-start"), ent, PopupType.LargeCaution);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.EvacTime,
            new DHLAVEvacDoAfterEvent(), ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        });
    }

    private void OnEvacDoAfter(Entity<DHLAVShuttleConsoleComponent> ent, ref DHLAVEvacDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || Outcome != DHLAVOutcome.Active || _inAtmosphere)
            return;

        args.Handled = true;
        if (Transform(ent).GridUid is not { } shuttle || shuttle != _shuttle)
            return;

        Evacuate(shuttle);
    }

    /// <summary>
    /// Шаттл уходит: бойцы LA-V на борту — с ним (становятся наблюдателями), чужие высаживаются на землю. Образец на
    /// борту — вывезен (провал для обеих сторон «Штурма»).
    /// </summary>
    private void Evacuate(EntityUid shuttle)
    {
        var sampleAboard = false;
        var samples = EntityQueryEnumerator<DHExperimentalSampleComponent, TransformComponent>();
        while (samples.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == shuttle)
                sampleAboard = true;
        }

        var mapUid = Transform(shuttle).MapUid;
        var eject = new List<(EntityUid Uid, Vector2 Position)>();
        var mobs = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobs.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != shuttle)
                continue;

            if (HasComp<DHLAVMemberComponent>(uid))
            {
                if (!_mobState.IsDead(uid))
                    _evacuated++;
                continue;
            }

            // Пассажир не на борту, а в руках или внутри чего-то — уйдёт вместе с носителем.
            if (xform.ParentUid == shuttle)
                eject.Add((uid, _transform.GetWorldPosition(xform)));
        }

        if (mapUid is { } map)
        {
            foreach (var (uid, position) in eject)
            {
                _transform.SetCoordinates(uid, new EntityCoordinates(map, position));
                _transform.AttachToGridOrMap(uid);
            }
        }

        if (sampleAboard)
        {
            Outcome = DHLAVOutcome.Stolen;
            _assault.MarkSampleStolen();
        }
        else
        {
            Outcome = DHLAVOutcome.Withdrew;
        }

        _audio.PlayPvs(LaunchSound, shuttle);
        _chat.DispatchGlobalAnnouncement(Loc.GetString("dh-lav-departed-announcement"),
            Loc.GetString("dh-lav-announcement-sender"), true, null, LavColor);

        _shuttle = null;
        QueueDel(shuttle);
    }

    #endregion

    #region Итоги

    /// <summary>
    /// Удержание (решение автора 2026-11-26 — флагами): к концу операции в зоне объекта стоит флаг LA-V и нет флагов
    /// сторон «Осады».
    /// </summary>
    private bool Holding()
    {
        if (TargetLocation() is not { } there)
            return false;

        var (republic, empire, lav) = FlagsAround(new MapCoordinates(there.Position, Transform(there.Map).MapID),
            TargetRadius());
        return lav && !republic && !empire;
    }

    private float TargetRadius() => MathF.Max(_targetRadius, 4f);

    private (bool Republic, bool Empire, bool LAV) FlagsAround(MapCoordinates center, float radius)
    {
        var republic = false;
        var empire = false;
        var lav = false;
        foreach (var flag in _lookup.GetEntitiesInRange<DHPlantableFlagComponent>(center, radius))
        {
            if (!Transform(flag).Anchored)
                continue;

            if (flag.Comp.ThirdParty)
                lav = true;
            else if (flag.Comp.Faction == DHAssaultFaction.Republic)
                republic = true;
            else if (flag.Comp.Faction == DHAssaultFaction.Empire)
                empire = true;
        }

        return (republic, empire, lav);
    }

    private void OnRoundEndText(ref RoundEndTextAppendEvent args)
    {
        if (Outcome is not { } outcome)
            return;

        if (outcome == DHLAVOutcome.Active)
        {
            outcome = _kind == DHLAVTargetKind.Hold && Holding() ? DHLAVOutcome.Held : DHLAVOutcome.Failed;
            Outcome = outcome;
        }

        args.AddLine(string.Empty);
        args.AddLine(Loc.GetString("dh-lav-summary-title"));
        args.AddLine(Loc.GetString(_kind == DHLAVTargetKind.Sample ? "dh-lav-task-sample" : "dh-lav-task-hold",
            ("target", _targetName), ("radius", (int) TargetRadius())));
        args.AddLine(Loc.GetString("dh-lav-summary-outcome",
            ("outcome", Loc.GetString($"dh-lav-outcome-{outcome.ToString().ToLowerInvariant()}")),
            ("evacuated", _evacuated)));
        foreach (var line in _summaryNames)
        {
            args.AddLine(line);
        }
    }

    #endregion
}

/// <summary>Админ: открыть набор отряда LA-V сейчас (на 15 минут). Необязательный аргумент — минимум бойцов.</summary>
[Content.Server.Administration.AdminCommand(Content.Shared.Administration.AdminFlags.Admin)]
public sealed partial class DHCallLAVCommand : Robust.Shared.Console.LocalizedEntityCommands
{
    [Dependency] private DHLAVSystem _lav = default!;

    public override string Command => "dhcalllavsquad";

    public override void Execute(Robust.Shared.Console.IConsoleShell shell, string argStr, string[] args)
    {
        var min = DHLAVSystem.MinSquad;
        if (args.Length > 0 && !int.TryParse(args[0], out min))
        {
            shell.WriteError(Loc.GetString("dh-lav-admin-usage"));
            return;
        }

        if (_lav.AdminOpen(min))
            shell.WriteLine(Loc.GetString("dh-lav-admin-opened", ("min", min)));
        else
            shell.WriteError(Loc.GetString("dh-lav-admin-busy"));
    }
}
