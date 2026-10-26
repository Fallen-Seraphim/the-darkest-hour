using System.Numerics;
using Content.Shared._DarkestHour.Levels;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
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

    private static readonly SoundSpecifier ClimbSound = new SoundCollectionSpecifier("FootstepCatwalk");
    private static readonly SoundSpecifier LiftAlarm = new SoundPathSpecifier("/Audio/Machines/alarm.ogg",
        AudioParams.Default.WithVolume(-6f));
    private static readonly SoundSpecifier LiftMove = new SoundPathSpecifier("/Audio/Machines/airlock_creaking.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHLadderComponent, InteractHandEvent>(OnLadderInteract);
        SubscribeLocalEvent<DHLadderComponent, DHLadderDoAfterEvent>(OnLadderDoAfter);
        SubscribeLocalEvent<DHLadderComponent, ExaminedEvent>(OnLadderExamined);

        SubscribeLocalEvent<DHLiftPanelComponent, InteractHandEvent>(OnPanelInteract);
        SubscribeLocalEvent<DHLiftPanelComponent, GetVerbsEvent<AlternativeVerb>>(OnPanelVerbs);
    }

    #region Лестницы

    private EntityUid? OtherLadder(Entity<DHLadderComponent> ent)
    {
        var query = EntityQueryEnumerator<DHLadderComponent>();
        while (query.MoveNext(out var uid, out var ladder))
        {
            if (uid != ent.Owner && ladder.Link == ent.Comp.Link)
                return uid;
        }

        return null;
    }

    private void OnLadderInteract(Entity<DHLadderComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (OtherLadder(ent) == null)
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
        if (args.Cancelled || args.Handled || OtherLadder(ent) is not { } other)
            return;

        args.Handled = true;
        var user = args.User;
        var destination = Transform(other).Coordinates;

        // Раненого, которого тащишь, берёшь с собой.
        EntityUid? pulled = TryComp<PullerComponent>(user, out var puller) ? puller.Pulling : null;

        _audio.PlayPvs(ClimbSound, ent);
        _transform.SetCoordinates(user, destination);
        if (pulled != null && Exists(pulled))
            _transform.SetCoordinates(pulled.Value, destination);

        _audio.PlayPvs(ClimbSound, other);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Down ? "dh-ladder-arrived-down" : "dh-ladder-arrived-up",
            ("user", user)), other, PopupType.Small);
    }

    private void OnLadderExamined(Entity<DHLadderComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || OtherLadder(ent) is not { } other)
            return;

        var where = Transform(other).GridUid is { } grid ? Name(grid) : Loc.GetString("dh-ladder-somewhere");
        args.PushMarkup(Loc.GetString(ent.Comp.Down ? "dh-ladder-examine-down" : "dh-ladder-examine-up",
            ("where", FormattedMessage.EscapeText(where))));
    }

    #endregion

    #region Лифт

    private Entity<DHLiftComponent>? LiftNear(EntityUid panel, string link)
    {
        var map = Transform(panel).MapID;
        var pos = _transform.GetWorldPosition(panel);
        Entity<DHLiftComponent>? best = null;
        var bestDistance = float.MaxValue;

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
        var query = EntityQueryEnumerator<DHLiftComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (uid != lift.Owner && comp.Link == lift.Comp.Link)
                return (uid, comp);
        }

        return null;
    }

    private void OnPanelInteract(Entity<DHLiftPanelComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        StartLift(ent, args.User, send: true);
    }

    private void OnPanelVerbs(Entity<DHLiftPanelComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dh-lift-verb-call"),
            Act = () => StartLift(ent, user, send: false),
        });
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
            if (OtherLift(lift) is not { } target)
                continue;

            target.Comp.NextUse = lift.Comp.NextUse;
            Carry(lift, target);
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
