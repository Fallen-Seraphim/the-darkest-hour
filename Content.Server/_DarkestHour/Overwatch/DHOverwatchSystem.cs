using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Overwatch;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Damage.Components;
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
/// (подписка зрителя на его глаз — работает и между картами: с авианосца видно десант на поверхности), приказы стороне.
/// </summary>
public sealed partial class DHOverwatchSystem : EntitySystem
{
    [Dependency] private DHAssaultRuleSystem _assault = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private ViewSubscriberSystem _view = default!;

    private static readonly SoundSpecifier OrderSound = new SoundPathSpecifier("/Audio/Misc/notice2.ogg");
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
        });

        SubscribeLocalEvent<DHOverwatchConsoleComponent, ComponentShutdown>(OnShutdown);
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
            TryComp<DHAssaultFighterComponent>(target, out var fighter) &&
            fighter.Faction == ent.Comp.Faction &&
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
        if (text.Length == 0)
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

        ent.Comp.NextOrder = now + ent.Comp.OrderCooldown;
        var sender = Loc.GetString("dh-overwatch-order-sender", ("name", Name(args.Actor)));
        _assault.AnnounceToFaction(ent.Comp.Faction, sender, text, OrderSound);
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
        var query = EntityQueryEnumerator<DHAssaultFighterComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var fighter, out var mobState, out var xform))
        {
            if (fighter.Faction != ent.Comp.Faction)
                continue;

            fighters.Add(Entry(uid, mobState, xform));
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

        _ui.SetUiState(ent.Owner, DHOverwatchUiKey.Key, new DHOverwatchState(ent.Comp.Faction, fighters, watching));
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
            var left = 1f - damageable.TotalDamage.Float() / incap.Value.Float();
            entry.Health = Math.Clamp((int) MathF.Round(left * 100f), 0, 100);
        }

        return entry;
    }
}
