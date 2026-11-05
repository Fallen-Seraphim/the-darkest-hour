using System.Numerics;
using Content.Shared._DarkestHour.Levels;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._DarkestHour.Levels;

/// <summary>
/// Переходы между уровнями: лестницы и люки (подъём/спуск с задержкой, тащимого бойца берёшь с собой) и грузовой
/// лифт (всё незакреплённое на платформе переезжает на платформу-пару через несколько секунд).
/// </summary>
public sealed partial class DHLevelSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private DHZLevelSystem _z = default!;
    [Dependency] private DHZTransitSystem _zTransit = default!;

    private static readonly SoundSpecifier ClimbSound = new SoundCollectionSpecifier("FootstepCatwalk");
    private static readonly SoundSpecifier LiftAlarm = new SoundPathSpecifier("/Audio/Machines/alarm.ogg",
        AudioParams.Default.WithVolume(-6f));
    private TimeSpan _nextPanelRefresh;

    private static readonly SoundSpecifier LiftMove = new SoundPathSpecifier("/Audio/Machines/airlock_creaking.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHLadderComponent, InteractHandEvent>(OnLadderInteract);
        SubscribeLocalEvent<DHLadderComponent, DHLadderDoAfterEvent>(OnLadderDoAfter);
        SubscribeLocalEvent<DHLadderComponent, ExaminedEvent>(OnLadderExamined);

        SubscribeLocalEvent<DHLiftPanelComponent, InteractHandEvent>(OnPanelInteract);
        SubscribeLocalEvent<DHLiftPanelComponent, GetVerbsEvent<AlternativeVerb>>(OnPanelVerbs);
        Subs.BuiEvents<DHLiftPanelComponent>(DHLiftPanelUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnPanelOpened);
            subs.Event<DHLiftSendMessage>(OnPanelSend);
            subs.Event<DHLiftCallMessage>(OnPanelCall);
        });
    }

    #region Лестницы

    private EntityUid? OtherLadder(Entity<DHLadderComponent> ent)
    {
        if (string.IsNullOrEmpty(ent.Comp.Link))
            return null;

        var query = EntityQueryEnumerator<DHLadderComponent>();
        while (query.MoveNext(out var uid, out var ladder))
        {
            if (uid != ent.Owner && ladder.Link == ent.Comp.Link)
                return uid;
        }

        return null;
    }

    /// <summary>Куда ведёт лестница: к паре по ключу или (без ключа) на соседний Z-уровень в той же точке.</summary>
    private MapCoordinates? LadderDestination(Entity<DHLadderComponent> ent)
    {
        if (!string.IsNullOrEmpty(ent.Comp.Link))
            return OtherLadder(ent) is { } other ? _transform.GetMapCoordinates(other) : null;

        var here = _transform.GetMapCoordinates(ent);
        if (!_z.TryGetLevel(here.MapId, out var level) ||
            _z.OnLevel(here, level.Level + (ent.Comp.Down ? -1 : 1)) is not { } destination ||
            !_zTransit.CanStand(destination))
        {
            return null;
        }

        return destination;
    }

    private void OnLadderInteract(Entity<DHLadderComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (LadderDestination(ent) == null)
        {
            _popup.PopupEntity(Loc.GetString("dh-ladder-blocked"), ent, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new DHLadderDoAfterEvent(),
            ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });

        _popup.PopupEntity(Loc.GetString(ent.Comp.Down ? "dh-ladder-start-down" : "dh-ladder-start-up",
            ("user", args.User)), ent, PopupType.Small);
    }

    private void OnLadderDoAfter(Entity<DHLadderComponent> ent, ref DHLadderDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || LadderDestination(ent) is not { } destination)
            return;

        args.Handled = true;
        var user = args.User;

        // Раненого, которого тащишь, берёшь с собой.
        _audio.PlayPvs(ClimbSound, ent);
        _zTransit.MoveTo(user, destination);

        _audio.PlayPvs(ClimbSound, user);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Down ? "dh-ladder-arrived-down" : "dh-ladder-arrived-up",
            ("user", user)), user, PopupType.Small);
    }

    private void OnLadderExamined(Entity<DHLadderComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || LadderDestination(ent) is not { } destination)
            return;

        string where;
        if (_z.TryGetLevel(destination.MapId, out var level) && string.IsNullOrEmpty(ent.Comp.Link))
            where = DHZTransitSystem.LevelName(level.Level);
        else if (OtherLadder(ent) is { } other && Transform(other).GridUid is { } grid)
            where = Name(grid);
        else
            where = Loc.GetString("dh-ladder-somewhere");

        args.PushMarkup(Loc.GetString(ent.Comp.Down ? "dh-ladder-examine-down" : "dh-ladder-examine-up",
            ("where", FormattedMessage.EscapeText(where))));
    }

    #endregion

    #region Лифт

    /// <summary>Ближе этого платформа к пульту — его платформа.</summary>
    private const float PanelReach = 6f;

    private Entity<DHLiftComponent>? LiftNear(EntityUid panel, string link)
    {
        var map = Transform(panel).MapID;
        var pos = _transform.GetWorldPosition(panel);
        Entity<DHLiftComponent>? best = null;
        var bestDistance = string.IsNullOrEmpty(link) ? PanelReach : float.MaxValue;

        var query = EntityQueryEnumerator<DHLiftComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var lift, out var xform))
        {
            if (lift.Link != link || xform.MapID != map)
                continue;

            var distance = Vector2.Distance(_transform.GetWorldPosition(xform), pos);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = (uid, lift);
            }
        }

        return best;
    }

    private Entity<DHLiftComponent>? OtherLift(Entity<DHLiftComponent> lift)
    {
        if (string.IsNullOrEmpty(lift.Comp.Link))
            return null;

        var query = EntityQueryEnumerator<DHLiftComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (uid != lift.Owner && comp.Link == lift.Comp.Link)
                return (uid, comp);
        }

        return null;
    }

    /// <summary>Шахта Z-лифта: платформы без ключа в той же точке XY на уровнях той же стопки (с уровнем).</summary>
    /// <summary>Допуск совпадения платформ шахты по XY, тайлов (уровни грузятся отдельными гридами).</summary>
    private const float ShaftTolerance = 2f;

    private List<(Entity<DHLiftComponent> Lift, int Level)> Shaft(Entity<DHLiftComponent> lift)
    {
        var result = new List<(Entity<DHLiftComponent>, int)>();
        var xform = Transform(lift);
        if (!_z.TryGetLevel(xform.MapUid, out var own))
            return result;

        // Ближайшая платформа на каждом уровне стопки.
        var pos = _transform.GetWorldPosition(xform);
        var best = new Dictionary<int, (Entity<DHLiftComponent> Lift, float Distance)>();
        var query = EntityQueryEnumerator<DHLiftComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var otherXform))
        {
            if (!string.IsNullOrEmpty(comp.Link) || !_z.TryGetLevel(otherXform.MapUid, out var level) ||
                level.Stack != own.Stack)
            {
                continue;
            }

            var distance = Vector2.Distance(_transform.GetWorldPosition(otherXform), pos);
            if (distance > ShaftTolerance ||
                best.TryGetValue(level.Level, out var known) && known.Distance <= distance)
            {
                continue;
            }

            best[level.Level] = ((uid, comp), distance);
        }

        foreach (var (level, (shaftLift, _)) in best)
        {
            result.Add((shaftLift, level));
        }

        result.Sort((a, b) => a.Item2.CompareTo(b.Item2));

        // Диагностика: дыра в шахте (между крайними этажами нет платформы) — один раз на платформу.
        if (result.Count > 1 && result[^1].Item2 - result[0].Item2 + 1 != result.Count && _shaftWarned.Add(lift.Owner))
        {
            var levels = string.Join(", ", result.ConvertAll(r => r.Item2));
            Log.Warning($"Lift shaft of {ToPrettyString(lift)} at {pos} has gaps: levels {levels}");
        }

        return result;
    }

    private readonly HashSet<EntityUid> _shaftWarned = new();

    private void OnPanelInteract(Entity<DHLiftPanelComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        // Z-лифт: окно с этажами откроется по активации (ActivatableUI).
        if (string.IsNullOrEmpty(ent.Comp.Link))
            return;

        args.Handled = true;
        StartLift(ent, args.User, send: true);
    }

    private void OnPanelVerbs(Entity<DHLiftPanelComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        if (!string.IsNullOrEmpty(ent.Comp.Link))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dh-lift-verb-call"),
                Act = () => StartLift(ent, user, send: false),
            });
        }
    }

    private void OnPanelOpened(Entity<DHLiftPanelComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdatePanelUi(ent);
    }

    private void OnPanelSend(Entity<DHLiftPanelComponent> ent, ref DHLiftSendMessage args)
    {
        if (LiftNear(ent, string.Empty) is not { } here || !TryGetEntity(args.To, out var to) ||
            !TryComp<DHLiftComponent>(to, out var toLift))
        {
            return;
        }

        StartZLift(ent, args.Actor, here, (to.Value, toLift));
        UpdatePanelUi(ent);
    }

    private void OnPanelCall(Entity<DHLiftPanelComponent> ent, ref DHLiftCallMessage args)
    {
        if (LiftNear(ent, string.Empty) is not { } here || !TryGetEntity(args.From, out var from) ||
            !TryComp<DHLiftComponent>(from, out var fromLift))
        {
            return;
        }

        StartZLift(ent, args.Actor, (from.Value, fromLift), here);
        UpdatePanelUi(ent);
    }

    private void UpdatePanelUi(Entity<DHLiftPanelComponent> ent)
    {
        var floors = new List<DHLiftFloor>();
        var busy = false;
        if (LiftNear(ent, string.Empty) is { } here)
        {
            var now = _timing.CurTime;
            foreach (var (lift, level) in Shaft(here))
            {
                busy |= lift.Comp.DepartAt != null || now < lift.Comp.NextUse;
                floors.Add(new DHLiftFloor(level, DHZTransitSystem.LevelName(level), GetNetEntity(lift),
                    lift.Owner == here.Owner, false));
            }
        }

        floors.Reverse(); // сверху вниз, как кнопки в лифте
        _ui.SetUiState(ent.Owner, DHLiftPanelUiKey.Key, new DHLiftPanelState(floors, busy));
    }

    /// <summary>Рейс Z-лифта: груз с платформы <paramref name="from"/> на платформу <paramref name="to"/>.</summary>
    private void StartZLift(Entity<DHLiftPanelComponent> panel, EntityUid user, Entity<DHLiftComponent> from,
        Entity<DHLiftComponent> to)
    {
        var shaft = Shaft(from);
        var now = _timing.CurTime;
        foreach (var (lift, _) in shaft)
        {
            if (lift.Comp.DepartAt != null || now < lift.Comp.NextUse)
            {
                _popup.PopupEntity(Loc.GetString("dh-lift-busy"), panel, user);
                return;
            }
        }

        var fromLevel = shaft.Find(l => l.Lift.Owner == from.Owner).Level;
        var toLevel = shaft.Find(l => l.Lift.Owner == to.Owner).Level;
        var travel = from.Comp.TravelTime + TimeSpan.FromSeconds(2 * Math.Max(0, Math.Abs(toLevel - fromLevel) - 1));

        from.Comp.DepartAt = now + travel;
        from.Comp.Destination = to;

        foreach (var end in new[] { from.Owner, to.Owner })
        {
            _audio.PlayPvs(LiftAlarm, end);
            _popup.PopupEntity(Loc.GetString(end == from.Owner ? "dh-lift-departing" : "dh-lift-arriving",
                ("seconds", (int) travel.TotalSeconds)), end, PopupType.MediumCaution);
        }
    }

    /// <summary>send — увезти груз с платформы у пульта; иначе — привезти с другого уровня.</summary>
    private void StartLift(Entity<DHLiftPanelComponent> panel, EntityUid user, bool send)
    {
        if (LiftNear(panel, panel.Comp.Link) is not { } here || OtherLift(here) is not { } there)
        {
            _popup.PopupEntity(Loc.GetString("dh-lift-broken"), panel, user);
            return;
        }

        var now = _timing.CurTime;
        if (here.Comp.DepartAt != null || there.Comp.DepartAt != null ||
            now < here.Comp.NextUse || now < there.Comp.NextUse)
        {
            _popup.PopupEntity(Loc.GetString("dh-lift-busy"), panel, user);
            return;
        }

        var from = send ? here : there;
        from.Comp.DepartAt = now + from.Comp.TravelTime;

        foreach (var end in new[] { here.Owner, there.Owner })
        {
            _audio.PlayPvs(LiftAlarm, end);
            _popup.PopupEntity(Loc.GetString(end == from.Owner ? "dh-lift-departing" : "dh-lift-arriving",
                ("seconds", (int) from.Comp.TravelTime.TotalSeconds)), end, PopupType.MediumCaution);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // Открытые пульты Z-лифтов — раз в секунду (занят / свободен).
        if (now >= _nextPanelRefresh)
        {
            _nextPanelRefresh = now + TimeSpan.FromSeconds(1);
            var panels = EntityQueryEnumerator<DHLiftPanelComponent>();
            while (panels.MoveNext(out var panelUid, out var panel))
            {
                if (string.IsNullOrEmpty(panel.Link) && _ui.IsUiOpen(panelUid, DHLiftPanelUiKey.Key))
                    UpdatePanelUi((panelUid, panel));
            }
        }

        var query = EntityQueryEnumerator<DHLiftComponent>();
        var due = new List<Entity<DHLiftComponent>>();
        while (query.MoveNext(out var uid, out var lift))
        {
            if (lift.DepartAt is { } at && now >= at)
                due.Add((uid, lift));
        }

        foreach (var lift in due)
        {
            lift.Comp.DepartAt = null;
            lift.Comp.NextUse = now + lift.Comp.Cooldown;
            var destination = lift.Comp.Destination;
            lift.Comp.Destination = null;

            Entity<DHLiftComponent>? target = OtherLift(lift);
            if (destination is { } dest && TryComp<DHLiftComponent>(dest, out var destComp))
                target = new Entity<DHLiftComponent>(dest, destComp);

            if (target == null)
                continue;

            target.Value.Comp.NextUse = lift.Comp.NextUse;
            Carry(lift, target.Value);
        }
    }

    /// <summary>Всё незакреплённое на платформе — на платформу-пару, с тем же смещением от центра.</summary>
    private void Carry(Entity<DHLiftComponent> from, Entity<DHLiftComponent> to)
    {
        var fromXform = Transform(from);
        var center = _transform.GetWorldPosition(fromXform);
        var box = Box2.CenteredAround(center, new Vector2(from.Comp.HalfSize * 2f));

        var cargo = new List<EntityUid>();
        foreach (var uid in _lookup.GetEntitiesIntersecting(fromXform.MapID, box, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (uid == from.Owner || !TryComp(uid, out TransformComponent? xform) || xform.Anchored)
                continue;

            // Только то, что лежит/стоит на платформе, а не вложено в другие сущности.
            if (xform.ParentUid != fromXform.ParentUid && xform.ParentUid != fromXform.MapUid)
                continue;

            cargo.Add(uid);
        }

        var toXform = Transform(to);
        var toCenter = toXform.LocalPosition;
        foreach (var uid in cargo)
        {
            var offset = _transform.GetWorldPosition(uid) - center;
            _transform.SetCoordinates(uid, new EntityCoordinates(toXform.ParentUid, toCenter + offset));
        }

        _audio.PlayPvs(LiftMove, from);
        _audio.PlayPvs(LiftMove, to);
    }

    #endregion
}
