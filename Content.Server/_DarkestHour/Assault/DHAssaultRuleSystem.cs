using System.Linq;
using System.Numerics;
using Content.Server._DarkestHour.Shuttles.Rift;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Shared.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Antag;
using Content.Shared.Cuffs.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking.Rules;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Station;
using Robust.Server.Player;
using Content.Shared.Chat;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Assault;

/// <summary>
/// Режим «Штурм». Гарнизон Республики (станция на планете) и Империя (авианосец в соседней системе). Фаза
/// подготовки: разрывной генератор авианосца заблокирован. Потом авианосец прыгает в систему базы и штурмует её;
/// задача — вывезти экспериментальный образец.
/// <list type="bullet">
/// <item>Авианосец ушёл из системы базы: образец на борту — победа Империи (крупная, если гарнизон уничтожен,
/// иначе малая); без образца — малая победа Империи, если гарнизон уничтожен, иначе малая победа Республики.</item>
/// <item>Авианосец сбит (прочность на нуле) — падает на планету: крупная победа Республики, поисковая операция.</item>
/// <item>Все силы Империи уничтожены — малая победа Республики. Лимит времени — то же, если гарнизон жив.</item>
/// </list>
/// Погибшие возвращаются волнами подкреплений, пока у стороны есть тикеты (Империя — пока цел авианосец).
/// </summary>
public sealed partial class DHAssaultRuleSystem : GameRuleSystem<DHAssaultRuleComponent>
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DHCelestialSystem _celestial = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private DHLandingSystem _landing = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DHRiftSystem _rift = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private ServerGameTicker _serverTicker = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private ServerStationSystem _station = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public static readonly Color RepublicGreen = Color.FromHex("#7FAE46");
    public static readonly Color ImperialYellow = Color.FromHex("#F2C230");
    private static readonly SoundSpecifier AlarmSound = new SoundPathSpecifier("/Audio/Announcements/war.ogg");
    private static readonly SoundSpecifier AnnounceSound = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan CrashDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ImpactDelay = TimeSpan.FromSeconds(9);

    public override void Initialize()
    {
        base.Initialize();

        // Карты звёздных систем создаёт DHRiftSystem на старте раунда — авианосец грузим после.
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting, after: new[] { typeof(DHRiftSystem) });
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<DHAssaultFighterComponent, MobStateChangedEvent>(OnFighterMobState);
    }

    #region Старт

    protected override void Started(Entity<DHAssaultRuleComponent, GameRuleComponent> rule, ref GameRuleStartedEvent args)
    {
        base.Started(rule, ref args);

        var now = Timing.CurTime;
        rule.Comp1.PrepEnd = now + rule.Comp1.PrepTime;
        rule.Comp1.RoundLimit = now + rule.Comp1.TimeLimit;
        foreach (var (faction, interval) in rule.Comp1.WaveInterval)
        {
            rule.Comp1.NextWave[faction] = now + interval;
        }

        // Правило добавили посреди раунда — карты систем уже есть.
        if (GameTicker.RunLevel == GameRunLevel.InRound)
            SetupCarrier((rule.Owner, rule.Comp1));
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        var query = EntityQueryEnumerator<DHAssaultRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (GameTicker.IsGameRuleActive((uid, gameRule)))
                SetupCarrier((uid, comp));
        }
    }

    /// <summary>Авианосец в соседней системе — вторая станция раунда с должностями Империи.</summary>
    private void SetupCarrier(Entity<DHAssaultRuleComponent> rule)
    {
        if (rule.Comp.Carrier != null)
            return;

        if (_celestial.GetSystemMap(rule.Comp.StartSystem) is not { } map ||
            !_celestial.TryGetSystem(map, out _, out var origin) ||
            !TryComp<MapComponent>(map, out var mapComp))
        {
            Log.Error($"Assault: no map for the carrier start system {rule.Comp.StartSystem}");
            return;
        }

        if (!_loader.TryLoadGrid(mapComp.MapId, rule.Comp.CarrierPath, out var grid,
                offset: origin + rule.Comp.StartPosition))
        {
            Log.Error($"Assault: failed to load the carrier {rule.Comp.CarrierPath}");
            return;
        }

        var name = Loc.GetString(rule.Comp.CarrierName);
        _meta.SetEntityName(grid.Value, name);

        var carrier = EnsureComp<DHAssaultCarrierComponent>(grid.Value);
        carrier.Hull = rule.Comp.CarrierHull;
        carrier.MaxHull = rule.Comp.CarrierHull;
        rule.Comp.Carrier = grid.Value;

        var config = new StationConfig
        {
            StationPrototype = rule.Comp.CarrierStation,
            StationComponentOverrides = new ComponentRegistry(),
        };
        rule.Comp.CarrierStationUid = _station.InitializeNewStation(config, new[] { grid.Value.Owner }, name);

        // Фаза подготовки: разрывной генератор заблокирован до её конца.
        var generators = EntityQueryEnumerator<DHRiftGeneratorComponent, TransformComponent>();
        while (generators.MoveNext(out _, out var generator, out var xform))
        {
            if (xform.GridUid == grid.Value.Owner)
                generator.NextJump = rule.Comp.PrepEnd;
        }
    }

    #endregion

    #region Бойцы

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (!TryComp<DHAssaultFactionStationComponent>(ev.Station, out var factionStation))
            return;

        var faction = factionStation.Faction;
        EnsureComp<DHAssaultFighterComponent>(ev.Mob).Faction = faction;

        foreach (var rule in ActiveRules())
        {
            rule.Comp.Players[ev.Player.UserId] = faction;

            var minutes = Math.Max(0, (int) Math.Ceiling((rule.Comp.PrepEnd - Timing.CurTime).TotalMinutes));
            var briefing = faction == DHAssaultFaction.Republic
                ? Loc.GetString("dh-assault-briefing-republic", ("minutes", minutes))
                : Loc.GetString("dh-assault-briefing-empire", ("minutes", minutes));
            _antag.SendBriefing(ev.Player, briefing, FactionColor(faction), null);
        }
    }

    private void OnFighterMobState(Entity<DHAssaultFighterComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead ||
            !_mind.TryGetMind(ent.Owner, out _, out var mind) ||
            !_player.TryGetSessionById(mind.UserId, out var session))
        {
            return;
        }

        foreach (var rule in ActiveRules())
        {
            var faction = ent.Comp.Faction;
            var tickets = rule.Comp.Tickets.GetValueOrDefault(faction);
            if (tickets <= 0 || !CanReinforce(rule.Comp, faction))
            {
                _chatManager.DispatchServerMessage(session, Loc.GetString("dh-assault-no-reinforcements"));
                continue;
            }

            var wave = rule.Comp.NextWave.GetValueOrDefault(faction);
            var seconds = Math.Max(0, (int) (wave - Timing.CurTime).TotalSeconds);
            _chatManager.DispatchServerMessage(session, Loc.GetString("dh-assault-next-wave",
                ("minutes", seconds / 60), ("seconds", seconds % 60), ("tickets", tickets)));
        }
    }

    /// <summary>Живые и не связанные бойцы стороны.</summary>
    private int ActiveFighters(DHAssaultFaction faction)
    {
        var count = 0;
        var query = EntityQueryEnumerator<DHAssaultFighterComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var fighter, out var mobState))
        {
            if (fighter.Faction != faction || mobState.CurrentState == MobState.Dead)
                continue;

            if (TryComp<CuffableComponent>(uid, out var cuffable) && cuffable.CuffedHandCount > 0)
                continue;

            count++;
        }

        return count;
    }

    /// <summary>Сторона уничтожена: живых бойцов нет и вернуть некого.</summary>
    private bool FactionDestroyed(DHAssaultRuleComponent rule, DHAssaultFaction faction)
    {
        if (ActiveFighters(faction) > 0)
            return false;

        return rule.Tickets.GetValueOrDefault(faction) <= 0 ||
               !CanReinforce(rule, faction) ||
               WaitingForWave(rule, faction).Count == 0;
    }

    private bool CanReinforce(DHAssaultRuleComponent rule, DHAssaultFaction faction)
    {
        if (faction == DHAssaultFaction.Republic)
            return FactionStation(DHAssaultFaction.Republic) != null;

        return rule.CarrierStationUid != null &&
               rule.Carrier is { } carrier &&
               TryComp<DHAssaultCarrierComponent>(carrier, out var carrierComp) &&
               !carrierComp.Downed;
    }

    /// <summary>Погибшие игроки стороны, которых можно вернуть волной (сейчас призраки или мертвы).</summary>
    private List<ICommonSession> WaitingForWave(DHAssaultRuleComponent rule, DHAssaultFaction faction)
    {
        var result = new List<ICommonSession>();
        foreach (var (user, playerFaction) in rule.Players)
        {
            if (playerFaction != faction ||
                !_player.TryGetSessionById(user, out var session) ||
                session.AttachedEntity is not { } attached)
            {
                continue;
            }

            var dead = HasComp<GhostComponent>(attached) ||
                       TryComp<MobStateComponent>(attached, out var mobState) && mobState.CurrentState == MobState.Dead;
            if (dead)
                result.Add(session);
        }

        return result;
    }

    private EntityUid? FactionStation(DHAssaultFaction faction)
    {
        var query = EntityQueryEnumerator<DHAssaultFactionStationComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Faction == faction)
                return uid;
        }

        return null;
    }

    private void Wave(DHAssaultRuleComponent rule, DHAssaultFaction faction)
    {
        if (!CanReinforce(rule, faction) ||
            FactionStation(faction) is not { } station ||
            !rule.ReinforcementJob.TryGetValue(faction, out var job))
        {
            return;
        }

        var returned = 0;
        foreach (var session in WaitingForWave(rule, faction))
        {
            if (rule.Tickets.GetValueOrDefault(faction) <= 0)
                break;

            rule.Tickets[faction]--;
            var profile = _serverTicker.GetPlayerProfile(session);
            _mind.WipeMind(session);
            _serverTicker.DoSpawn(session, profile, station, job, true, out var mob, out _, out _);
            EnsureComp<DHAssaultFighterComponent>(mob).Faction = faction;
            _antag.SendBriefing(session, Loc.GetString("dh-assault-reinforced",
                ("tickets", rule.Tickets[faction])), FactionColor(faction), null);
            returned++;
        }

        if (returned > 0)
            Announce(faction, Loc.GetString("dh-assault-wave-arrived", ("count", returned), ("tickets", rule.Tickets[faction])));
    }

    #endregion

    #region Ход раунда

    protected override void ActiveTick(EntityUid uid, DHAssaultRuleComponent component, GameRuleComponent gameRule,
        float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        var now = Timing.CurTime;
        if (now < component.NextCheck || component.Outcome != DHAssaultOutcome.None && component.SearchEnd == null)
            return;

        component.NextCheck = now + CheckInterval;

        if (!component.PrepAnnounced && now >= component.PrepEnd)
        {
            component.PrepAnnounced = true;
            Announce(DHAssaultFaction.Empire, "dh-assault-prep-over-empire");
            Announce(DHAssaultFaction.Republic, "dh-assault-prep-over-republic");
        }

        if (component.Carrier is { } carrier && TryComp<DHAssaultCarrierComponent>(carrier, out var carrierComp))
        {
            UpdateCrash(component, (carrier, carrierComp), now);

            var system = SystemOfMap(Transform(carrier).MapUid);
            if (!component.Arrived && system == component.TargetSystem.Id)
            {
                component.Arrived = true;
                Announce(DHAssaultFaction.Republic, "dh-assault-carrier-arrived", AlarmSound);
                Announce(DHAssaultFaction.Empire, "dh-assault-carrier-arrived-empire");
            }

            // Авианосец ушёл из системы базы — итог по образцу и гарнизону.
            if (component.Arrived && !carrierComp.Downed && system != null && system != component.TargetSystem.Id)
            {
                var sample = SampleAboard(carrier);
                var republicDown = FactionDestroyed(component, DHAssaultFaction.Republic);
                component.SampleExtracted = sample;
                Finish(component, sample
                    ? (republicDown ? DHAssaultOutcome.ImperialMajor : DHAssaultOutcome.ImperialMinor)
                    : (republicDown ? DHAssaultOutcome.ImperialMinor : DHAssaultOutcome.RepublicMinor));
                return;
            }
        }

        // Поисковая операция после падения авианосца.
        if (component.SearchEnd is { } searchEnd)
        {
            if (now >= searchEnd || ActiveFighters(DHAssaultFaction.Empire) == 0)
            {
                component.SearchEnd = null;
                Finish(component, DHAssaultOutcome.RepublicMajor);
            }

            return;
        }

        if (component.Arrived)
        {
            if (!component.RepublicDestroyedAnnounced && FactionDestroyed(component, DHAssaultFaction.Republic))
            {
                component.RepublicDestroyedAnnounced = true;
                Announce(DHAssaultFaction.Empire, "dh-assault-garrison-destroyed");
            }

            var downed = component.Carrier is { } c && TryComp<DHAssaultCarrierComponent>(c, out var cc) && cc.Downed;
            if (!downed && FactionDestroyed(component, DHAssaultFaction.Empire))
            {
                Finish(component, DHAssaultOutcome.RepublicMinor);
                return;
            }
        }

        if (now >= component.RoundLimit)
        {
            Finish(component, FactionDestroyed(component, DHAssaultFaction.Republic)
                ? DHAssaultOutcome.ImperialMinor
                : DHAssaultOutcome.RepublicMinor);
            return;
        }

        foreach (var faction in Enum.GetValues<DHAssaultFaction>())
        {
            if (!component.WaveInterval.TryGetValue(faction, out var interval) ||
                now < component.NextWave.GetValueOrDefault(faction))
            {
                continue;
            }

            component.NextWave[faction] = now + interval;
            Wave(component, faction);
        }
    }

    private void Finish(DHAssaultRuleComponent rule, DHAssaultOutcome outcome)
    {
        if (rule.Outcome != DHAssaultOutcome.None && outcome != DHAssaultOutcome.RepublicMajor)
            return;

        rule.Outcome = outcome;
        _chat.DispatchGlobalAnnouncement(Loc.GetString(OutcomeText(outcome)), Loc.GetString("dh-assault-outcome-sender"),
            true, null, outcome is DHAssaultOutcome.ImperialMajor or DHAssaultOutcome.ImperialMinor
                ? ImperialYellow
                : RepublicGreen);
        _roundEnd.EndRound(TimeSpan.FromMinutes(1));
    }

    private static string OutcomeText(DHAssaultOutcome outcome)
    {
        return outcome switch
        {
            DHAssaultOutcome.ImperialMajor => "dh-assault-outcome-imperial-major",
            DHAssaultOutcome.ImperialMinor => "dh-assault-outcome-imperial-minor",
            DHAssaultOutcome.RepublicMajor => "dh-assault-outcome-republic-major",
            DHAssaultOutcome.RepublicMinor => "dh-assault-outcome-republic-minor",
            _ => "dh-assault-outcome-none",
        };
    }

    protected override void AppendRoundEndText(Entity<DHAssaultRuleComponent> rule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(rule, ref args);

        args.AddLine(Loc.GetString(OutcomeText(rule.Comp.Outcome)));
        args.AddLine(Loc.GetString(rule.Comp.SampleExtracted ? "dh-assault-end-sample-taken" : "dh-assault-end-sample-kept"));
        args.AddLine(Loc.GetString("dh-assault-end-tickets",
            ("republic", rule.Comp.Tickets.GetValueOrDefault(DHAssaultFaction.Republic)),
            ("empire", rule.Comp.Tickets.GetValueOrDefault(DHAssaultFaction.Empire))));
        args.AddLine("");
    }

    #endregion

    #region Авианосец

    /// <summary>Урон авианосцу (ПКО). На нуле — падение на планету.</summary>
    public bool DamageCarrier(float amount)
    {
        foreach (var rule in ActiveRules())
        {
            if (rule.Comp.Carrier is not { } carrier || !TryComp<DHAssaultCarrierComponent>(carrier, out var comp))
                continue;

            if (comp.Downed)
                return false;

            comp.Hull = MathF.Max(0f, comp.Hull - amount);
            if (comp.Hull <= 0f)
                Down(rule.Comp, (carrier, comp));

            return true;
        }

        return false;
    }

    private void Down(DHAssaultRuleComponent rule, Entity<DHAssaultCarrierComponent> carrier)
    {
        carrier.Comp.Downed = true;
        carrier.Comp.CrashAt = Timing.CurTime + CrashDelay;
        Announce(DHAssaultFaction.Empire, "dh-assault-carrier-falling", AlarmSound);
        Announce(DHAssaultFaction.Republic, "dh-assault-carrier-hit");
    }

    private void UpdateCrash(DHAssaultRuleComponent rule, Entity<DHAssaultCarrierComponent> carrier, TimeSpan now)
    {
        if (carrier.Comp.CrashAt is { } crashAt && now >= crashAt)
        {
            carrier.Comp.CrashAt = null;
            carrier.Comp.ImpactAt = now + ImpactDelay;

            // Не на поверхности — рушится на неё рядом с базой.
            if (!HasComp<DHPlanetSurfaceMapComponent>(Transform(carrier).MapUid) &&
                CrashSite(carrier) is { } site &&
                TryComp<ShuttleComponent>(carrier, out var shuttle))
            {
                _shuttle.FTLToCoordinates(carrier, shuttle, site, _random.NextAngle(), 2f, 5f);
            }
        }

        if (carrier.Comp.ImpactAt is { } impactAt && now >= impactAt)
        {
            carrier.Comp.ImpactAt = null;
            Impact(carrier);
            rule.Outcome = DHAssaultOutcome.RepublicMajor;
            rule.SearchEnd = now + rule.SearchTime;
            Announce(DHAssaultFaction.Republic, "dh-assault-search-operation");
            Announce(DHAssaultFaction.Empire, "dh-assault-crashed");
        }
    }

    /// <summary>Точка падения: на поверхности тела базы, в стороне от неё.</summary>
    private EntityCoordinates? CrashSite(EntityUid carrier)
    {
        var bases = EntityQueryEnumerator<DHPlanetBaseComponent>();
        while (bases.MoveNext(out var station, out var planetBase))
        {
            if (_landing.GetSurface(planetBase.System, planetBase.Body) is not { } surface ||
                _station.GetLargestGrid(station) is not { } baseGrid)
            {
                continue;
            }

            var center = _transform.GetWorldPosition(baseGrid);
            for (var i = 0; i < 12; i++)
            {
                var offset = _random.NextAngle().ToVec() * _random.NextFloat(90f, 160f);
                var coordinates = new EntityCoordinates(surface, center + offset);
                if (_rift.ExitFree(carrier, coordinates))
                    return coordinates;
            }
        }

        return null;
    }

    /// <summary>Удар о поверхность: взрывы по корпусу, шаттл больше не летает.</summary>
    private void Impact(EntityUid carrier)
    {
        if (TryComp<ShuttleComponent>(carrier, out var shuttle))
            shuttle.Enabled = false;

        if (!TryComp<MapGridComponent>(carrier, out var grid))
            return;

        var tiles = _map.GetAllTiles(carrier, grid).ToList();
        if (tiles.Count == 0)
            return;

        for (var i = 0; i < 6; i++)
        {
            var tile = _random.Pick(tiles);
            var coordinates = _map.GridTileToWorld(carrier, grid, tile.GridIndices);
            _explosion.QueueExplosion(coordinates, "Default", 140f, 4f, 18f, null);
        }
    }

    private bool SampleAboard(EntityUid carrier)
    {
        var query = EntityQueryEnumerator<DHExperimentalSampleComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == carrier)
                return true;
        }

        return false;
    }

    #endregion

    #region Помощники

    private IEnumerable<Entity<DHAssaultRuleComponent>> ActiveRules()
    {
        var result = new List<Entity<DHAssaultRuleComponent>>();
        var query = EntityQueryEnumerator<DHAssaultRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (GameTicker.IsGameRuleActive((uid, gameRule)))
                result.Add((uid, comp));
        }

        return result;
    }

    /// <summary>Звёздная система карты: карта системы или поверхность тела; null — в пути (FTL).</summary>
    private string? SystemOfMap(EntityUid? map)
    {
        if (TryComp<DHStarSystemMapComponent>(map, out var systemMap) && systemMap.System is { } system)
            return system.Id;

        if (TryComp<DHPlanetSurfaceMapComponent>(map, out var surface))
            return surface.System;

        return null;
    }

    /// <summary>
    /// Объявление только своей стороне (живым бойцам и погибшим игрокам стороны). Станционное объявление не
    /// годится: его слышат все в 32 м от гридов станции — и штурмующие базу имперцы тоже.
    /// </summary>
    private void Announce(DHAssaultFaction faction, string message, SoundSpecifier? sound = null)
    {
        var sessions = new HashSet<ICommonSession>();
        var fighters = EntityQueryEnumerator<DHAssaultFighterComponent, ActorComponent>();
        while (fighters.MoveNext(out _, out var fighter, out var actor))
        {
            if (fighter.Faction == faction)
                sessions.Add(actor.PlayerSession);
        }

        foreach (var rule in ActiveRules())
        {
            foreach (var (user, playerFaction) in rule.Comp.Players)
            {
                if (playerFaction == faction && _player.TryGetSessionById(user, out var session))
                    sessions.Add(session);
            }
        }

        if (sessions.Count == 0)
            return;

        var filter = Filter.Empty().AddPlayers(sessions);
        var text = Loc.TryGetString(message, out var localized) ? localized : message;
        var wrapped = Loc.GetString("chat-manager-sender-announcement-wrap-message",
            ("sender", Loc.GetString(SenderOf(faction))), ("message", FormattedMessage.EscapeText(text)));
        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Radio, text, wrapped, EntityUid.Invalid, false, true,
            FactionColor(faction));
        _audio.PlayGlobal(sound ?? AnnounceSound, filter, true, AudioParams.Default.WithVolume(-2f));
    }

    private static string SenderOf(DHAssaultFaction faction)
    {
        return faction == DHAssaultFaction.Republic ? "dh-assault-sender-republic" : "dh-assault-sender-empire";
    }

    private static Color FactionColor(DHAssaultFaction faction)
    {
        return faction == DHAssaultFaction.Republic ? RepublicGreen : ImperialYellow;
    }

    #endregion
}
