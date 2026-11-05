using Content.Shared.ActionBlocker;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._DarkestHour.Movement;

// Ползание в критическом состоянии (решение автора 2026-11-01, по образцу RussianCM / CMU): раненый, только что
// упавший в крит (не глубже окна), через несколько секунд может очень медленно отползти. Новое попадание — перестаёт.

[RegisterComponent, NetworkedComponent]
public sealed partial class DHCrawlWhileCritComponent : Component
{
    /// <summary>Насколько урон может превышать порог крита, чтобы ещё ползти.</summary>
    [DataField]
    public FixedPoint2 CrawlWindow = 30;

    [DataField]
    public TimeSpan ActivationDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public float SpeedModifier = 0.15f;

    [DataField]
    public List<ProtoId<DamageGroupPrototype>> AbortOnDamageGroups = new() { "Brute", "Burn" };
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHActiveCrawlComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan StartAt;

    [DataField, AutoNetworkedField]
    public bool Started;
}

public sealed partial class DHCrawlWhileCritSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHCrawlWhileCritComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<DHCrawlWhileCritComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<DHActiveCrawlComponent, UpdateCanMoveEvent>(OnUpdateCanMove,
            after: new[] { typeof(MobStateSystem) });
        SubscribeLocalEvent<DHActiveCrawlComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnMobStateChanged(Entity<DHCrawlWhileCritComponent> ent, ref MobStateChangedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (args.NewMobState == MobState.Critical)
        {
            if (InWindow(ent))
                Begin(ent);
            else
                ServerPopup(ent, "dh-crawl-too-hurt", PopupType.SmallCaution);
            return;
        }

        Stop(ent);
    }

    private void OnBeforeDamage(Entity<DHCrawlWhileCritComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (_timing.ApplyingState || args.Cancelled ||
            !TryComp<DHActiveCrawlComponent>(ent, out var active) || !active.Started)
        {
            return;
        }

        foreach (var groupId in ent.Comp.AbortOnDamageGroups)
        {
            if (!_proto.TryIndex(groupId, out var group))
                continue;

            foreach (var type in group.DamageTypes)
            {
                if (args.Damage.DamageDict.TryGetValue(type, out var amount) && amount > FixedPoint2.Zero)
                {
                    ServerPopup(ent, "dh-crawl-stopped-hit", PopupType.SmallCaution);
                    Stop(ent);
                    return;
                }
            }
        }
    }

    private bool InWindow(Entity<DHCrawlWhileCritComponent> ent)
    {
        if (!TryComp<DamageableComponent>(ent, out var damageable) ||
            !_thresholds.TryGetThresholdForState(ent.Owner, MobState.Critical, out var crit))
        {
            return false;
        }

        return _damageable.GetTotalDamage((ent.Owner, damageable)) - crit.Value <= ent.Comp.CrawlWindow;
    }

    private void Begin(Entity<DHCrawlWhileCritComponent> ent)
    {
        if (HasComp<DHActiveCrawlComponent>(ent))
            return;

        var active = EnsureComp<DHActiveCrawlComponent>(ent);
        active.StartAt = _timing.CurTime + ent.Comp.ActivationDelay;
        active.Started = false;
        Dirty(ent.Owner, active);
    }

    private void Stop(EntityUid uid)
    {
        if (!RemComp<DHActiveCrawlComponent>(uid))
            return;

        _blocker.UpdateCanMove(uid);
        _speed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnUpdateCanMove(Entity<DHActiveCrawlComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (!ent.Comp.Started || ent.Comp.LifeStage > ComponentLifeStage.Running ||
            !TryComp<MobStateComponent>(ent, out var state) || state.CurrentState != MobState.Critical ||
            TryComp<BuckleComponent>(ent, out var buckle) && buckle.Buckled || Blocked(ent))
        {
            return;
        }

        args.Uncancel();
    }

    private void OnRefreshSpeed(Entity<DHActiveCrawlComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!ent.Comp.Started || ent.Comp.LifeStage > ComponentLifeStage.Running || Blocked(ent) ||
            !TryComp<DHCrawlWhileCritComponent>(ent, out var crawl))
        {
            return;
        }

        args.ModifySpeed(crawl.SpeedModifier, crawl.SpeedModifier);
    }

    /// <summary>Оглушён, сбит с ног или без сознания (боль, сон) — не ползёт.</summary>
    private bool Blocked(EntityUid uid)
    {
        return HasComp<StunnedComponent>(uid) || HasComp<KnockedDownComponent>(uid) || HasComp<SleepingComponent>(uid);
    }

    private void ServerPopup(EntityUid uid, string key, PopupType type)
    {
        if (_net.IsServer)
            _popup.PopupEntity(Loc.GetString(key), uid, uid, type);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHActiveCrawlComponent, DHCrawlWhileCritComponent>();
        while (query.MoveNext(out var uid, out var active, out var crawl))
        {
            if (!InWindow((uid, crawl)))
            {
                Stop(uid);
                continue;
            }

            if (active.Started || now < active.StartAt)
                continue;

            active.Started = true;
            Dirty(uid, active);
            _blocker.UpdateCanMove(uid);
            _speed.RefreshMovementSpeedModifiers(uid);
            ServerPopup(uid, "dh-crawl-started", PopupType.Small);
        }
    }
}
