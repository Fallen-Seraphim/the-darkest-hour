using Content.Server.Stack;
using Content.Shared._DarkestHour.Fortifications;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Fortifications;

/// <summary>
/// Складная баррикада MB-6 (порт RMCFoldingBarricadeSystem RMC-14, MIT; решение автора 2026-11-12).
/// Стопка до 3 штук в руке: использовать — баррикада встаёт на твоей клетке лицом туда, куда смотришь (1 с).
/// Сложить — альтернативное действие или перетащить баррикаду на себя (3 с), ломом — 1,5 с; сложенная запоминает
/// урон. Сваркой по стопке чинятся все повреждённые (по 10 с за каждую). Стопки складываются друг в друга.
/// </summary>
public sealed partial class DHFoldingBarricadeSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    private static readonly ProtoId<ToolQualityPrototype> WeldingQuality = "Welding";
    private static readonly ProtoId<ToolQualityPrototype> PryingQuality = "Prying";
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly SoundSpecifier DeploySound = new SoundPathSpecifier("/Audio/Items/ratchet.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHFoldingBarricadeStackComponent, ComponentStartup>(OnStackStartup);
        SubscribeLocalEvent<DHFoldingBarricadeStackComponent, StackCountChangedEvent>(OnStackCountChanged);
        SubscribeLocalEvent<DHFoldingBarricadeStackComponent, StackSplitEvent>(OnStackSplit);
        SubscribeLocalEvent<DHFoldingBarricadeStackComponent, StackMergeEvent>(OnStackMerge);
        SubscribeLocalEvent<DHFoldingBarricadeStackComponent, UseInHandEvent>(OnStackUseInHand);
        SubscribeLocalEvent<DHFoldingBarricadeStackComponent, InteractUsingEvent>(OnStackInteractUsing,
            before: new[] { typeof(StackSystem) });
        SubscribeLocalEvent<DHFoldingBarricadeStackComponent, DHFoldingBarricadeDeployDoAfterEvent>(OnDeployDoAfter);
        SubscribeLocalEvent<DHFoldingBarricadeStackComponent, DHFoldingBarricadeRepairDoAfterEvent>(OnRepairDoAfter);
        SubscribeLocalEvent<DHFoldingBarricadeStackComponent, ExaminedEvent>(OnStackExamined);

        SubscribeLocalEvent<DHFoldingBarricadeComponent, InteractUsingEvent>(OnBarricadeInteractUsing,
            before: new[] { typeof(StackSystem) });
        SubscribeLocalEvent<DHFoldingBarricadeComponent, GetVerbsEvent<AlternativeVerb>>(OnBarricadeVerbs);
        SubscribeLocalEvent<DHFoldingBarricadeComponent, CanDragEvent>(OnCanDrag);
        SubscribeLocalEvent<DHFoldingBarricadeComponent, CanDropDraggedEvent>(OnCanDropDragged);
        SubscribeLocalEvent<DHFoldingBarricadeComponent, DragDropDraggedEvent>(OnDragDropDragged);
        SubscribeLocalEvent<DHFoldingBarricadeComponent, DHFoldingBarricadeCollapseDoAfterEvent>(OnCollapseDoAfter);
        SubscribeLocalEvent<DHFoldingBarricadeComponent, ExaminedEvent>(OnBarricadeExamined);
    }

    #region Стопка

    private void OnStackStartup(Entity<DHFoldingBarricadeStackComponent> ent, ref ComponentStartup args)
    {
        Normalize(ent, _stack.GetCount(ent.Owner));
    }

    private void OnStackUseInHand(Entity<DHFoldingBarricadeStackComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!CanDeploy(args.User, out _, out var popup))
        {
            _popup.PopupEntity(popup, args.User, args.User, PopupType.SmallCaution);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.DeployDelay,
            new DHFoldingBarricadeDeployDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnStackInteractUsing(Entity<DHFoldingBarricadeStackComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, WeldingQuality.Id))
            return;

        args.Handled = true;
        var damaged = DamagedCount(ent);
        if (damaged == 0)
        {
            _popup.PopupEntity(Loc.GetString("dh-folding-barricade-not-damaged"), ent, args.User);
            return;
        }

        _tool.UseTool(args.Used, args.User, ent, (float) (ent.Comp.RepairDelayPerDamaged.TotalSeconds * damaged),
            WeldingQuality, new DHFoldingBarricadeRepairDoAfterEvent(), ent.Comp.RepairFuel);
    }

    private void OnRepairDoAfter(Entity<DHFoldingBarricadeStackComponent> ent, ref DHFoldingBarricadeRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        for (var i = 0; i < ent.Comp.StoredDamage.Count; i++)
        {
            ent.Comp.StoredDamage[i] = Math.Clamp(ent.Comp.StoredDamage[i] - ent.Comp.RepairAmount, 0, ent.Comp.MaxDamage);
        }

        _popup.PopupEntity(Loc.GetString("dh-folding-barricade-repaired"), ent, args.User);
    }

    private void OnStackExamined(Entity<DHFoldingBarricadeStackComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("dh-folding-barricade-stack-examine"));
        if (DamagedCount(ent) > 0)
            args.PushMarkup(Loc.GetString("dh-folding-barricade-stack-damaged", ("count", DamagedCount(ent))));
    }

    /// <summary>Убавили стопку — снятые сверху баррикады ждут, не уйдут ли они в отделённую стопку.</summary>
    private void OnStackCountChanged(Entity<DHFoldingBarricadeStackComponent> ent, ref StackCountChangedEvent args)
    {
        if (ent.Comp.SuppressCountChange)
            return;

        if (args.NewCount < args.OldCount)
        {
            Normalize(ent, args.OldCount);
            for (var i = 0; i < args.OldCount - args.NewCount; i++)
            {
                var last = ent.Comp.StoredDamage.Count > 0 ? ent.Comp.StoredDamage[^1] : 0f;
                if (ent.Comp.StoredDamage.Count > 0)
                    ent.Comp.StoredDamage.RemoveAt(ent.Comp.StoredDamage.Count - 1);
                ent.Comp.PendingSplitDamage.Insert(0, last);
            }
        }

        Normalize(ent, args.NewCount);
    }

    private void OnStackSplit(Entity<DHFoldingBarricadeStackComponent> ent, ref StackSplitEvent args)
    {
        if (!TryComp<DHFoldingBarricadeStackComponent>(args.NewId, out var split))
            return;

        var count = _stack.GetCount(args.NewId);
        split.StoredDamage.Clear();
        for (var i = 0; i < count; i++)
        {
            if (ent.Comp.PendingSplitDamage.Count > 0)
            {
                split.StoredDamage.Add(Math.Clamp(ent.Comp.PendingSplitDamage[0], 0, ent.Comp.MaxDamage));
                ent.Comp.PendingSplitDamage.RemoveAt(0);
            }
            else
            {
                split.StoredDamage.Add(0);
            }
        }

        ent.Comp.PendingSplitDamage.Clear();
    }

    /// <summary>Стопку вложили в другую: урон переезжает вместе с баррикадами (до смены счётчиков).</summary>
    private void OnStackMerge(Entity<DHFoldingBarricadeStackComponent> ent, ref StackMergeEvent args)
    {
        if (!TryComp<DHFoldingBarricadeStackComponent>(args.Donor, out var donor))
            return;

        Normalize((args.Donor, donor), _stack.GetCount(args.Donor));
        Normalize(ent, _stack.GetCount(ent.Owner));
        for (var i = 0; i < args.Amount && donor.StoredDamage.Count > 0; i++)
        {
            ent.Comp.StoredDamage.Add(donor.StoredDamage[^1]);
            donor.StoredDamage.RemoveAt(donor.StoredDamage.Count - 1);
        }

        // Дальше стопочная система поменяет счётчики: списки уже нужной длины, нормализация их не тронет.
    }

    #endregion

    #region Развернуть

    private bool CanDeploy(EntityUid user, out Direction direction, out string popup)
    {
        popup = string.Empty;
        var xform = Transform(user);
        direction = xform.LocalRotation.GetCardinalDir();
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid) ||
            !_map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tile) || _turf.IsSpace(tile))
        {
            popup = Loc.GetString("dh-folding-barricade-no-ground");
            return false;
        }

        var facing = direction.ToAngle();
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile.GridIndices);
        while (anchored.MoveNext(out var uid))
        {
            if (_tag.HasTag(uid.Value, WallTag) ||
                HasComp<DHBarricadeComponent>(uid) &&
                (Transform(uid.Value).LocalRotation - facing).Reduced().EqualsApprox(Angle.Zero, 0.1))
            {
                popup = Loc.GetString("dh-folding-barricade-blocked");
                return false;
            }
        }

        return true;
    }

    private void OnDeployDoAfter(Entity<DHFoldingBarricadeStackComponent> ent, ref DHFoldingBarricadeDeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var count = _stack.GetCount(ent.Owner);
        if (count <= 0)
            return;

        if (!CanDeploy(args.User, out var direction, out var popup))
        {
            _popup.PopupEntity(popup, args.User, args.User, PopupType.SmallCaution);
            return;
        }

        Normalize(ent, count);
        var damage = ent.Comp.StoredDamage.Count > 0 ? ent.Comp.StoredDamage[^1] : 0f;
        if (ent.Comp.StoredDamage.Count > 0)
            ent.Comp.StoredDamage.RemoveAt(ent.Comp.StoredDamage.Count - 1);

        ent.Comp.SuppressCountChange = true;
        _stack.SetCount(ent.Owner, count - 1);
        ent.Comp.SuppressCountChange = false;

        var coords = Transform(args.User).Coordinates.SnapToGrid(EntityManager);
        var barricade = Spawn(ent.Comp.Deployed, coords);
        var xform = Transform(barricade);
        _transform.SetLocalRotation(barricade, direction.ToAngle(), xform);
        if (!xform.Anchored)
            _transform.AnchorEntity(barricade, xform);

        ApplyDamage(barricade, damage);
        _audio.PlayPvs(DeploySound, barricade);
        _popup.PopupEntity(Loc.GetString("dh-folding-barricade-deployed"), barricade, args.User);
    }

    private void ApplyDamage(EntityUid target, float damage)
    {
        if (damage <= 0)
            return;

        var spec = new DamageSpecifier();
        spec.DamageDict["Blunt"] = FixedPoint2.New(damage);
        _damageable.TryChangeDamage(target, spec, ignoreResistances: true);
    }

    #endregion

    #region Сложить

    private void OnBarricadeInteractUsing(Entity<DHFoldingBarricadeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, PryingQuality.Id))
            return;

        args.Handled = true;
        TryStartCollapse(ent, args.User, ent.Comp.CrowbarCollapseDelay, args.Used);
    }

    private void OnBarricadeVerbs(Entity<DHFoldingBarricadeComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dh-folding-barricade-collapse-verb"),
            Act = () => TryStartCollapse(ent, user, ent.Comp.CollapseDelay, null),
            Priority = 1,
        });
    }

    private void OnCanDrag(Entity<DHFoldingBarricadeComponent> ent, ref CanDragEvent args)
    {
        args.Handled = true;
    }

    private void OnCanDropDragged(Entity<DHFoldingBarricadeComponent> ent, ref CanDropDraggedEvent args)
    {
        if (args.User != args.Target)
            return;

        args.CanDrop = true;
        args.Handled = true;
    }

    private void OnDragDropDragged(Entity<DHFoldingBarricadeComponent> ent, ref DragDropDraggedEvent args)
    {
        if (args.User != args.Target)
            return;

        args.Handled = true;
        TryStartCollapse(ent, args.User, ent.Comp.CollapseDelay, null);
    }

    private void TryStartCollapse(Entity<DHFoldingBarricadeComponent> ent, EntityUid user, TimeSpan delay, EntityUid? used)
    {
        if (TryComp<DHWireableComponent>(ent, out var wire) && wire.Wired)
        {
            _popup.PopupEntity(Loc.GetString("dh-folding-barricade-wired"), ent, user, PopupType.SmallCaution);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, delay, new DHFoldingBarricadeCollapseDoAfterEvent(),
            ent, ent, used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnCollapseDoAfter(Entity<DHFoldingBarricadeComponent> ent, ref DHFoldingBarricadeCollapseDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || TerminatingOrDeleted(ent))
            return;

        args.Handled = true;
        var damage = TryComp<DamageableComponent>(ent, out var damageable)
            ? Math.Clamp(_damageable.GetTotalDamage((ent.Owner, damageable)).Float(), 0, ent.Comp.MaxDamage)
            : 0f;

        // В стопку в руках, если есть место, иначе — новая сложенная баррикада в руки.
        var merged = false;
        foreach (var held in _hands.EnumerateHeld(args.User))
        {
            if (!TryComp<DHFoldingBarricadeStackComponent>(held, out var stack) || !TryComp<StackComponent>(held, out var stackComp) ||
                stackComp.Count >= _stack.GetMaxCount(stackComp))
            {
                continue;
            }

            Normalize((held, stack), stackComp.Count);
            stack.StoredDamage.Add(damage);
            stack.SuppressCountChange = true;
            _stack.SetCount(held, stackComp.Count + 1);
            stack.SuppressCountChange = false;
            merged = true;
            break;
        }

        if (!merged)
        {
            var folded = Spawn(ent.Comp.Folded, Transform(ent).Coordinates);
            if (TryComp<DHFoldingBarricadeStackComponent>(folded, out var stack))
            {
                stack.StoredDamage.Clear();
                stack.StoredDamage.Add(damage);
            }

            _hands.PickupOrDrop(args.User, folded);
        }

        _popup.PopupEntity(Loc.GetString("dh-folding-barricade-collapsed"), args.User, args.User);
        QueueDel(ent);
    }

    private void OnBarricadeExamined(Entity<DHFoldingBarricadeComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("dh-folding-barricade-examine"));
    }

    #endregion

    private void Normalize(Entity<DHFoldingBarricadeStackComponent> ent, int count)
    {
        var list = ent.Comp.StoredDamage;
        while (list.Count > count)
        {
            list.RemoveAt(list.Count - 1);
        }

        while (list.Count < count)
        {
            list.Add(0);
        }
    }

    private int DamagedCount(Entity<DHFoldingBarricadeStackComponent> ent)
    {
        var n = 0;
        foreach (var d in ent.Comp.StoredDamage)
        {
            if (d > 0)
                n++;
        }

        return n;
    }
}
