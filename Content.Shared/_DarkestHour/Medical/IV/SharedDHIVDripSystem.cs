using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._DarkestHour.Medical.IV;

/// <summary>DarkestHour: капельница и пакеты крови (порт RMC-14 SharedIVDripSystem без навыков и диализа).</summary>
public abstract partial class SharedDHIVDripSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _packsToUpdate = [];

    private EntityQuery<DHBloodPackComponent> _bloodPackQuery;

    public override void Initialize()
    {
        _bloodPackQuery = GetEntityQuery<DHBloodPackComponent>();

        SubscribeLocalEvent<DHIVDripComponent, EntInsertedIntoContainerMessage>(OnIVDripEntInserted);
        SubscribeLocalEvent<DHIVDripComponent, EntRemovedFromContainerMessage>(OnIVDripEntRemoved);
        SubscribeLocalEvent<DHIVDripComponent, AfterAutoHandleStateEvent>(OnIVDripAfterHandleState);
        SubscribeLocalEvent<DHIVDripComponent, CanDragEvent>(OnIVDripCanDrag);
        SubscribeLocalEvent<DHIVDripComponent, CanDropDraggedEvent>(OnIVDripCanDropDragged);
        SubscribeLocalEvent<DHIVDripComponent, DragDropDraggedEvent>(OnIVDripDragDropDragged);
        SubscribeLocalEvent<DHIVDripComponent, InteractHandEvent>(OnIVInteractHand);
        SubscribeLocalEvent<DHIVDripComponent, GetVerbsEvent<InteractionVerb>>(OnIVVerbs);
        SubscribeLocalEvent<DHIVDripComponent, ExaminedEvent>(OnIVExamine);

        SubscribeLocalEvent<BloodstreamComponent, CanDropTargetEvent>(OnTargetCanDropTarget);

        SubscribeLocalEvent<DHBloodPackComponent, MapInitEvent>(OnBloodPackMapInit);
        SubscribeLocalEvent<DHBloodPackComponent, AfterAutoHandleStateEvent>(OnBloodPackAfterState);
        SubscribeLocalEvent<DHBloodPackComponent, SolutionChangedEvent>(OnBloodPackSolutionChanged);
        SubscribeLocalEvent<DHBloodPackComponent, AfterInteractEvent>(OnBloodPackAfterInteract);
        SubscribeLocalEvent<DHBloodPackComponent, DHAttachBloodPackDoAfterEvent>(OnBloodPackAttachDoAfter);
        SubscribeLocalEvent<DHBloodPackComponent, GotUnequippedHandEvent>(OnBloodPackUnequippedHand);
        SubscribeLocalEvent<DHBloodPackComponent, GetVerbsEvent<InteractionVerb>>(OnBloodPackVerbs);
        SubscribeLocalEvent<DHBloodPackComponent, ExaminedEvent>(OnBloodPackExamine);
    }

    /// <summary>Подключать можно к тем, у кого есть кровь.</summary>
    protected bool IsTarget(EntityUid uid)
    {
        return HasComp<BloodstreamComponent>(uid);
    }

    #region Капельница

    private void OnIVDripEntInserted(Entity<DHIVDripComponent> iv, ref EntInsertedIntoContainerMessage args)
    {
        UpdateIVVisuals(iv);
    }

    private void OnIVDripEntRemoved(Entity<DHIVDripComponent> iv, ref EntRemovedFromContainerMessage args)
    {
        UpdateIVVisuals(iv);
    }

    private void OnIVDripAfterHandleState(Entity<DHIVDripComponent> iv, ref AfterAutoHandleStateEvent args)
    {
        UpdateIVAppearance(iv);
    }

    private void OnIVDripCanDrag(Entity<DHIVDripComponent> iv, ref CanDragEvent args)
    {
        args.Handled = true;
    }

    private void OnIVDripCanDropDragged(Entity<DHIVDripComponent> iv, ref CanDropDraggedEvent args)
    {
        if (!IsTarget(args.Target) || !InRange(iv, args.Target, iv.Comp.Range))
            return;

        args.Handled = true;
        args.CanDrop = true;
    }

    private void OnTargetCanDropTarget(Entity<BloodstreamComponent> patient, ref CanDropTargetEvent args)
    {
        if (!TryComp(args.Dragged, out DHIVDripComponent? iv) || !InRange(args.Dragged, patient, iv.Range))
            return;

        args.Handled = true;
        args.CanDrop = true;
    }

    private void OnIVDripDragDropDragged(Entity<DHIVDripComponent> iv, ref DragDropDraggedEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (iv.Comp.AttachedTo == null)
            AttachIV(iv, args.User, args.Target);
        else
            DetachIV(iv, args.User, false, true);
    }

    private void OnIVInteractHand(Entity<DHIVDripComponent> iv, ref InteractHandEvent args)
    {
        if (iv.Comp.AttachedTo == null)
            return;

        args.Handled = true;
        DetachIV(iv, args.User, false, true);
    }

    private void OnIVVerbs(Entity<DHIVDripComponent> iv, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () => ToggleInject(iv, user),
            Text = Loc.GetString("dh-iv-verb-toggle-inject"),
        });
    }

    private void OnIVExamine(Entity<DHIVDripComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(DHIVDripComponent)))
        {
            args.PushMarkup(Loc.GetString(ent.Comp.Injecting ? "dh-iv-examine-injecting" : "dh-iv-examine-drawing",
                ("iv", ent.Owner)));

            var chemicalsMsg = Loc.GetString("dh-iv-examine-chemicals-none");
            if (_containers.TryGetContainer(ent, ent.Comp.Slot, out var container) &&
                container.ContainedEntities.FirstOrDefault() is { Valid: true } packId &&
                TryComp(packId, out DHBloodPackComponent? pack) &&
                _solutionContainer.TryGetSolution(packId, pack.Solution, out _, out var solution))
            {
                chemicalsMsg = Loc.GetString("dh-iv-examine-chemicals",
                    ("attached", packId),
                    ("units", solution.Volume.Int()));
            }

            args.PushMarkup(chemicalsMsg);
            args.PushMarkup(ent.Comp.AttachedTo is { } attached
                ? Loc.GetString("dh-iv-examine-attached", ("attached", attached))
                : Loc.GetString("dh-iv-examine-attached-none"));
        }
    }

    private void AttachIV(Entity<DHIVDripComponent> iv, EntityUid user, EntityUid to)
    {
        if (!InRange(iv, to, iv.Comp.Range))
            return;

        iv.Comp.AttachedTo = to;
        Dirty(iv);
        AttachFeedback(iv, user, to, iv.Comp.Injecting);
    }

    protected void DetachIV(Entity<DHIVDripComponent> iv, EntityUid? user, bool rip, bool predict)
    {
        if (iv.Comp.AttachedTo is not { } target)
            return;

        iv.Comp.AttachedTo = null;
        Dirty(iv);

        if (rip)
            DoRip(iv.Comp.RipDamage, target, user, iv.Comp.RipEmote, predict);
        else
            DoDetachFeedback(iv, target, user, predict);
    }

    private void ToggleInject(Entity<DHIVDripComponent> iv, EntityUid user)
    {
        ToggleInject(iv, ref iv.Comp.Injecting, user);
        Dirty(iv);
    }

    #endregion

    #region Пакет крови

    private void OnBloodPackMapInit(Entity<DHBloodPackComponent> pack, ref MapInitEvent args)
    {
        _packsToUpdate.Add(pack);
    }

    private void OnBloodPackAfterState(Entity<DHBloodPackComponent> pack, ref AfterAutoHandleStateEvent args)
    {
        UpdatePackVisuals(pack);
    }

    private void OnBloodPackSolutionChanged(Entity<DHBloodPackComponent> pack, ref SolutionChangedEvent args)
    {
        UpdatePackVisuals(pack);
    }

    private void OnBloodPackAfterInteract(Entity<DHBloodPackComponent> pack, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        if (!InRange(pack, target, pack.Comp.Range) || !IsTarget(target))
            return;

        args.Handled = true;

        var user = args.User;
        if (pack.Comp.AttachedTo != null)
        {
            DetachPack(pack, user, false, true);
            return;
        }

        if (user == target)
        {
            _popup.PopupClient(Loc.GetString("dh-blood-pack-cannot-self"), user, user);
            return;
        }

        var delay = pack.Comp.AttachDelay;
        if (delay > TimeSpan.Zero)
        {
            var selfPoke = Loc.GetString("dh-blood-pack-poke-self", ("pack", pack.Owner), ("target", target));
            var othersPoke = Loc.GetString("dh-blood-pack-poke-others",
                ("user", user),
                ("pack", pack.Owner),
                ("target", target));
            _popup.PopupPredicted(selfPoke, othersPoke, target, user);
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, delay, new DHAttachBloodPackDoAfterEvent(),
            pack, target, pack)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        });
    }

    private void OnBloodPackAttachDoAfter(Entity<DHBloodPackComponent> pack, ref DHAttachBloodPackDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!InRange(pack, target, pack.Comp.Range))
            return;

        pack.Comp.AttachedTo = target;
        Dirty(pack);
        AttachFeedback(pack, args.User, target, pack.Comp.Injecting);
    }

    private void OnBloodPackUnequippedHand(Entity<DHBloodPackComponent> pack, ref GotUnequippedHandEvent args)
    {
        DetachPack(pack, args.User, true, true);
    }

    private void OnBloodPackVerbs(Entity<DHBloodPackComponent> pack, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () =>
            {
                ToggleInject(pack, ref pack.Comp.Injecting, user);
                Dirty(pack);
            },
            Text = Loc.GetString("dh-iv-verb-toggle-inject"),
        });
    }

    private void OnBloodPackExamine(Entity<DHBloodPackComponent> pack, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(DHBloodPackComponent)))
        {
            args.PushMarkup(Loc.GetString(pack.Comp.Injecting ? "dh-iv-examine-injecting" : "dh-iv-examine-drawing",
                ("iv", pack.Owner)));
            args.PushMarkup(pack.Comp.AttachedTo is { } attached
                ? Loc.GetString("dh-iv-examine-attached", ("attached", attached))
                : Loc.GetString("dh-iv-examine-attached-none"));

            if (_solutionContainer.TryGetSolution(pack.Owner, pack.Comp.Solution, out _, out var solution))
                args.PushMarkup(Loc.GetString("dh-blood-pack-contains", ("units", solution.Volume.Int())));
        }
    }

    protected void DetachPack(Entity<DHBloodPackComponent> pack, EntityUid? user, bool rip, bool predict)
    {
        if (pack.Comp.AttachedTo is not { } target)
            return;

        pack.Comp.AttachedTo = null;
        Dirty(pack);

        if (rip)
            DoRip(pack.Comp.RipDamage, target, user, pack.Comp.RipEmote, predict);
        else
            DoDetachFeedback(pack, target, user, predict);
    }

    #endregion

    protected bool InRange(EntityUid iv, EntityUid to, float range)
    {
        var ivPos = _transform.GetMapCoordinates(iv);
        var toPos = _transform.GetMapCoordinates(to);
        return ivPos.InRange(toPos, range);
    }

    private void ToggleInject(EntityUid iv, ref bool injecting, EntityUid user)
    {
        injecting = !injecting;
        _popup.PopupClient(Loc.GetString(injecting ? "dh-iv-now-injecting" : "dh-iv-now-taking"), iv, user);
    }

    protected void UpdatePackVisuals(Entity<DHBloodPackComponent> pack)
    {
        if (!_solutionContainer.TryGetSolution(pack.Owner, pack.Comp.Solution, out _, out var solution))
        {
            UpdatePackAppearance(pack);
            return;
        }

        if (_containers.TryGetContainingContainer((pack.Owner, null, null), out var container) &&
            TryComp(container.Owner, out DHIVDripComponent? iv))
        {
            iv.FillColor = solution.GetColor(_prototype);
            iv.FillPercentage = solution.MaxVolume > 0 ? (int) (solution.Volume / solution.MaxVolume * 100) : 0;
            Dirty(container.Owner, iv);
            UpdateIVAppearance((container.Owner, iv));
        }

        UpdatePackAppearance(pack);
    }

    protected void UpdateIVVisuals(Entity<DHIVDripComponent> iv)
    {
        // Клиент знает о растворах не всегда.
        if (_net.IsClient)
        {
            UpdateIVAppearance(iv);
            return;
        }

        if (!_containers.TryGetContainer(iv, iv.Comp.Slot, out var container))
            return;

        foreach (var entity in container.ContainedEntities)
        {
            if (!TryComp(entity, out DHBloodPackComponent? pack) ||
                !_solutionContainer.TryGetSolution(entity, pack.Solution, out _, out var solution))
            {
                continue;
            }

            iv.Comp.FillColor = solution.GetColor(_prototype);
            iv.Comp.FillPercentage = solution.MaxVolume > 0 ? (int) (solution.Volume / solution.MaxVolume * 100) : 0;
            Dirty(iv);
            UpdateIVAppearance(iv);
            return;
        }

        iv.Comp.FillColor = Color.White;
        iv.Comp.FillPercentage = 0;
        Dirty(iv);
        UpdateIVAppearance(iv);
    }

    protected virtual void UpdateIVAppearance(Entity<DHIVDripComponent> iv)
    {
    }

    protected virtual void UpdatePackAppearance(Entity<DHBloodPackComponent> pack)
    {
        if (_net.IsClient)
            return;

        if (_solutionContainer.TryGetSolution(pack.Owner, pack.Comp.Solution, out var solEnt))
        {
            var solution = solEnt.Value.Comp.Solution;
            pack.Comp.FillPercentage = solution.MaxVolume > 0 ? solution.Volume / solution.MaxVolume : FixedPoint2.Zero;
            pack.Comp.FillColor = solution.GetColor(_prototype);
        }
        else
        {
            pack.Comp.FillPercentage = FixedPoint2.Zero;
            pack.Comp.FillColor = Color.Transparent;
        }

        Dirty(pack);
    }

    protected virtual void DoRip(DamageSpecifier? damage,
        EntityUid attached,
        EntityUid? user,
        ProtoId<EmotePrototype> ripEmote,
        bool predict)
    {
        if (damage != null)
            _damageable.TryChangeDamage(attached, damage, true);

        if (!_timing.IsFirstTimePredicted)
            return;

        var message = Loc.GetString("dh-iv-rip", ("target", attached));
        if (predict)
        {
            _popup.PopupClient(message, attached, user);
            var others = user == null ? Filter.Pvs(attached) : Filter.PvsExcept(user.Value);
            _popup.PopupEntity(message, attached, others, true);
        }
        else
        {
            _popup.PopupEntity(message, attached);
        }
    }

    private void AttachFeedback(EntityUid iv, EntityUid user, EntityUid to, bool injecting)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var selfMessage = injecting ? "dh-iv-attach-self-injecting" : "dh-iv-attach-self-drawing";
        var othersMessage = injecting ? "dh-iv-attach-others-injecting" : "dh-iv-attach-others-drawing";

        _popup.PopupClient(Loc.GetString(selfMessage, ("iv", iv), ("target", to)), to, user);
        _popup.PopupEntity(Loc.GetString(othersMessage, ("iv", iv), ("user", user), ("target", to)), to,
            Filter.PvsExcept(user), true);
    }

    private void DoDetachFeedback(EntityUid iv, EntityUid attached, EntityUid? user, bool predict)
    {
        var selfMessage = Loc.GetString("dh-iv-detach-self", ("iv", iv), ("target", attached));
        if (predict)
            _popup.PopupClient(selfMessage, attached, user);
        else
            _popup.PopupEntity(selfMessage, attached);

        if (user == null)
            return;

        _popup.PopupEntity(Loc.GetString("dh-iv-detach-others", ("iv", iv), ("user", user), ("target", attached)),
            attached,
            Filter.PvsExcept(user.Value),
            true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var pack in _packsToUpdate)
        {
            if (_bloodPackQuery.TryComp(pack, out var comp))
                UpdatePackVisuals((pack, comp));
        }

        _packsToUpdate.Clear();
    }
}
