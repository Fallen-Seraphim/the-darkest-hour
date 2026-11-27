using Content.Server._DarkestHour.Assault;
using Content.Server._DarkestHour.ControlPoints;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Overwatch;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Overwatch;

/// <summary>
/// Консоль наблюдения «Штурма»: бойцы своей стороны с состоянием и координатами, вид глазами бойца
/// (подписка зрителя на его глаз — работает и между картами: с авианосца видно десант на поверхности), приказы.
/// По отрядам (решение автора 2026-11-26, как в RMC/CMU): приказ уходит выбранному отряду (или бойцам вне отрядов),
/// всей стороне — только с командной консоли. Планшет командира LA-V — то же для отряда ЧВК.
/// </summary>
public sealed partial class DHOverwatchSystem : EntitySystem
{
    [Dependency] private DHAssaultRuleSystem _assault = default!;
    [Dependency] private DHTacticalMapSystem _tacmap = default!;
    [Dependency] private Content.Server._DarkestHour.ThirdParty.DHLAVSystem _lav = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ViewSubscriberSystem _view = default!;
    [Dependency] private Robust.Shared.Prototypes.IPrototypeManager _proto = default!;

    private static readonly SoundSpecifier OrderSound = new SoundPathSpecifier("/Audio/_DarkestHour/Announcements/overwatch_order.ogg"); // решение автора 2026-11-26
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<DHOverwatchConsoleComponent>(DHOverwatchUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<BoundUIClosedEvent>(OnClosed);
            subs.Event<DHOverwatchWatchMessage>(OnWatch);
            subs.Event<DHOverwatchOrderMessage>(OnOrder);
            subs.Event<DHOverwatchOpenAnnouncerMessage>(OnOpenAnnouncer);
            subs.Event<DHTacticalMapRequestMessage>(OnTacticalMapRequest);
        });

        SubscribeLocalEvent<DHOverwatchConsoleComponent, ComponentShutdown>(OnShutdown);
    }

    /// <summary>Командный планшет командующего — он же планшет объявлений: кнопка в окне открывает объявления.</summary>
    private void OnOpenAnnouncer(Entity<DHOverwatchConsoleComponent> ent, ref DHOverwatchOpenAnnouncerMessage args)
    {
        if (!HasComp<Content.Shared._DarkestHour.Announcements.DHFactionAnnouncerComponent>(ent))
            return;

        _ui.OpenUi(ent.Owner, Content.Shared._DarkestHour.Announcements.DHFactionAnnouncerUiKey.Key, args.Actor);
    }

    private void OnOpened(Entity<DHOverwatchConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnClosed(Entity<DHOverwatchConsoleComponent> ent, ref BoundUIClosedEvent args)
    {
        StopWatching(ent, args.Actor);
    }

    private void OnShutdown(Entity<DHOverwatchConsoleComponent> ent, ref ComponentShutdown args)
    {
        foreach (var viewer in new List<EntityUid>(ent.Comp.Watching.Keys))
        {
            StopWatching(ent, viewer);
        }
    }

    private void OnWatch(Entity<DHOverwatchConsoleComponent> ent, ref DHOverwatchWatchMessage args)
    {
        var viewer = args.Actor;
        StopWatching(ent, viewer);

        if (args.Target is { } netTarget &&
            TryGetEntity(netTarget, out var target) &&
            Watchable(ent, target.Value) &&
            target != viewer &&
            TryComp<ActorComponent>(viewer, out var actor))
        {
            ent.Comp.Watching[viewer] = target.Value;
            _view.AddViewSubscriber(target.Value, actor.PlayerSession);
        }

        UpdateUi(ent);
    }

    private void OnOrder(Entity<DHOverwatchConsoleComponent> ent, ref DHOverwatchOrderMessage args)
    {
        var text = args.Text.Trim();
        if (text.Length == 0 || !ent.Comp.CanOrder)
            return;

        if (text.Length > ent.Comp.MaxOrderLength)
            text = text[..ent.Comp.MaxOrderLength];

        var now = _timing.CurTime;
        if (now < ent.Comp.NextOrder)
        {
            _popup.PopupEntity(Loc.GetString("dh-overwatch-order-cooldown",
                ("seconds", (int) Math.Ceiling((ent.Comp.NextOrder - now).TotalSeconds))), ent, args.Actor);
            return;
        }

        var senderKey = ent.Comp.OrderSender is { } key ? key.Id : "dh-overwatch-order-sender";
        var sender = Loc.GetString(senderKey, ("name", Name(args.Actor)));

        // Отряд ЧВК LA-V: всем бойцам отряда.
        if (ent.Comp.LAV)
        {
            ent.Comp.NextOrder = now + ent.Comp.OrderCooldown;
            var lav = new List<ICommonSession>();
            var members = EntityQueryEnumerator<Content.Server._DarkestHour.ThirdParty.DHLAVMemberComponent, ActorComponent>();
            while (members.MoveNext(out _, out _, out var actor))
            {
                lav.Add(actor.PlayerSession);
            }

            _assault.AnnounceTo(lav, sender, text, OrderSound, LAVColor, onScreen: true);
            return;
        }

        // Всей стороне — только с командной консоли.
        if (args.Squad == null)
        {
            if (!ent.Comp.FactionOrders)
            {
                _popup.PopupEntity(Loc.GetString("dh-overwatch-order-no-faction"), ent, args.Actor);
                return;
            }

            ent.Comp.NextOrder = now + ent.Comp.OrderCooldown;
            _assault.AnnounceToFaction(ent.Comp.Faction, sender, text, OrderSound, ent.Comp.OrderColor, onScreen: true);
            return;
        }

        // Отряду или бойцам вне отрядов.
        string squadName;
        Color squadColor;
        if (args.Squad == DHOverwatchOrderMessage.NoSquad)
        {
            squadName = Loc.GetString("dh-overwatch-squad-none");
            squadColor = ent.Comp.OrderColor ?? Color.White;
        }
        else if (_proto.TryIndex<Content.Shared._DarkestHour.Squads.DHSquadPrototype>(args.Squad, out var squad) &&
                 squad.Faction == ent.Comp.Faction)
        {
            squadName = Loc.GetString(squad.Name);
            squadColor = squad.Color;
        }
        else
        {
            return;
        }

        ent.Comp.NextOrder = now + ent.Comp.OrderCooldown;
        var sessions = new List<ICommonSession>();
        var fighters = EntityQueryEnumerator<DHAssaultFighterComponent, ActorComponent>();
        while (fighters.MoveNext(out var uid, out var fighter, out var actor))
        {
            if (fighter.Faction != ent.Comp.Faction)
                continue;

            var memberSquad = TryComp<Content.Shared._DarkestHour.Squads.DHSquadMemberComponent>(uid, out var member)
                ? member.Squad.Id
                : DHOverwatchOrderMessage.NoSquad;
            if (memberSquad == args.Squad)
                sessions.Add(actor.PlayerSession);
        }

        // Отправителю — копия, чтобы видел, что ушло.
        if (TryComp<ActorComponent>(args.Actor, out var self) && !sessions.Contains(self.PlayerSession))
            sessions.Add(self.PlayerSession);

        _assault.AnnounceTo(sessions, Loc.GetString("dh-overwatch-order-sender-squad", ("sender", sender),
            ("squad", squadName)), text, OrderSound, squadColor, onScreen: true);
    }

    private static readonly Color LAVColor = Color.FromHex("#8A6AD0");

    /// <summary>Кого можно смотреть с этой консоли: бойцов её стороны или отряда LA-V.</summary>
    private bool Watchable(Entity<DHOverwatchConsoleComponent> ent, EntityUid target)
    {
        if (ent.Comp.LAV)
            return HasComp<Content.Server._DarkestHour.ThirdParty.DHLAVMemberComponent>(target);

        return TryComp<DHAssaultFighterComponent>(target, out var fighter) && fighter.Faction == ent.Comp.Faction;
    }

    private void OnTacticalMapRequest(Entity<DHOverwatchConsoleComponent> ent, ref DHTacticalMapRequestMessage args)
    {
        if (_tacmap.GetSurfaceMap() is not { } surface || !TryComp<ActorComponent>(args.Actor, out var actor))
            return;

        // Уровень — только из стопки поверхности.
        var map = surface;
        if (args.Map is { } netMap && TryGetEntity(netMap, out var requested))
        {
            foreach (var (levelMap, _) in _tacmap.Levels(surface))
            {
                if (levelMap == requested)
                    map = levelMap;
            }
        }

        _tacmap.SendTerrain(map, actor.PlayerSession);
    }

    private void StopWatching(Entity<DHOverwatchConsoleComponent> ent, EntityUid viewer)
    {
        if (!ent.Comp.Watching.Remove(viewer, out var target))
            return;

        if (Exists(target) && TryComp<ActorComponent>(viewer, out var actor))
            _view.RemoveViewSubscriber(target, actor.PlayerSession);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextRefresh)
            return;

        _nextRefresh = now + RefreshInterval;

        var query = EntityQueryEnumerator<DHOverwatchConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            // Цель удалена, зритель отошёл или вышел — отключаем.
            foreach (var (viewer, target) in new Dictionary<EntityUid, EntityUid>(console.Watching))
            {
                if (!Exists(target) || !_ui.IsUiOpen(uid, DHOverwatchUiKey.Key, viewer))
                    StopWatching((uid, console), viewer);
            }

            if (_ui.IsUiOpen(uid, DHOverwatchUiKey.Key))
                UpdateUi((uid, console));
        }
    }

    private void UpdateUi(Entity<DHOverwatchConsoleComponent> ent)
    {
        var fighters = new List<DHOverwatchEntry>();
        var surface = _tacmap.GetSurfaceMap();
        var levels = surface is { } surfaceMap ? _tacmap.Levels(surfaceMap) : new List<(EntityUid Map, int Level)>();
        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mobState, out var xform))
        {
            if (!Watchable(ent, uid))
                continue;

            var entry = Entry(uid, mobState, xform);
            foreach (var (levelMap, level) in levels)
            {
                if (xform.MapUid != levelMap)
                    continue;

                entry.OnSurface = true;
                entry.Level = level;
            }

            fighters.Add(entry);
        }

        fighters.Sort((a, b) =>
        {
            var byStatus = a.Status.CompareTo(b.Status);
            return byStatus != 0 ? byStatus : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture);
        });

        var watching = new Dictionary<NetEntity, NetEntity>();
        foreach (var (viewer, target) in ent.Comp.Watching)
        {
            watching[GetNetEntity(viewer)] = GetNetEntity(target);
        }

        var markers = surface is { } map ? _tacmap.Markers(ent.Comp.Faction, map) : new List<DHTacticalMarker>();

        // LA-V: только общеизвестное — зоны захвата, ЗП, ориентиры.
        if (ent.Comp.LAV)
        {
            markers.RemoveAll(m => m.Kind is not (DHTacticalMarkerKind.ControlPoint or DHTacticalMarkerKind.LandingZone
                or DHTacticalMarkerKind.Beacon));
        }
        var version = surface is { } terrainMap ? _tacmap.TerrainVersion(terrainMap) : 0;

        var state = new DHOverwatchState(ent.Comp.Faction, fighters, watching, GetNetEntity(surface), version, markers)
        {
            FactionOrders = ent.Comp.FactionOrders && !ent.Comp.LAV,
            LAV = ent.Comp.LAV,
            Status = ent.Comp.LAV ? _lav.StatusText() : null,
        };

        if (!ent.Comp.LAV)
        {
            foreach (var squad in _proto.EnumeratePrototypes<Content.Shared._DarkestHour.Squads.DHSquadPrototype>())
            {
                if (squad.Faction != ent.Comp.Faction)
                    continue;

                state.Squads.Add(new DHOverwatchSquad { Id = squad.ID, Name = Loc.GetString(squad.Name), Color = squad.Color });
            }

            state.Squads.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        }

        if (levels.Count > 1)
        {
            foreach (var (levelMap, level) in levels)
            {
                state.Levels.Add(new DHTacticalLevel
                {
                    Level = level,
                    Map = GetNetEntity(levelMap),
                    Version = _tacmap.TerrainVersion(levelMap),
                });
            }
        }

        _ui.SetUiState(ent.Owner, DHOverwatchUiKey.Key, state);
    }

    private DHOverwatchEntry Entry(EntityUid uid, MobStateComponent mobState, TransformComponent xform)
    {
        var entry = new DHOverwatchEntry
        {
            Entity = GetNetEntity(uid),
            Name = Name(uid),
            Status = mobState.CurrentState switch
            {
                MobState.Critical => DHOverwatchStatus.Critical,
                MobState.Dead => DHOverwatchStatus.Dead,
                _ => DHOverwatchStatus.Alive,
            },
            Disconnected = !HasComp<ActorComponent>(uid),
        };

        entry.Job = _mind.TryGetMind(uid, out var mindId, out _) && _jobs.MindTryGetJobName(mindId, out var job)
            ? job
            : Loc.GetString("dh-overwatch-no-job");

        if (TryComp<Content.Shared._DarkestHour.Squads.DHSquadMemberComponent>(uid, out var member))
        {
            entry.Squad = member.Squad.Id;
            entry.SquadLeader = member.Leader;
        }

        var pos = _transform.GetWorldPosition(xform);
        entry.X = (int) MathF.Round(pos.X);
        entry.Y = (int) MathF.Round(pos.Y);

        if (xform.GridUid is { } grid)
            entry.Location = Name(grid);
        else if (xform.MapUid is { } map && HasComp<DHPlanetAtmosphereMapComponent>(map))
            entry.Location = Loc.GetString("dh-overwatch-location-air");
        else
            entry.Location = Loc.GetString("dh-overwatch-location-field");

        entry.Health = 0;
        if (entry.Status == DHOverwatchStatus.Alive &&
            TryComp<DamageableComponent>(uid, out var damageable) &&
            _thresholds.TryGetIncapThreshold(uid, out var incap) && incap.Value > FixedPoint2.Zero)
        {
            var left = 1f - _damageable.GetTotalDamage((uid, damageable)).Float() / incap.Value.Float();
            entry.Health = Math.Clamp((int) MathF.Round(left * 100f), 0, 100);
        }

        return entry;
    }
}
