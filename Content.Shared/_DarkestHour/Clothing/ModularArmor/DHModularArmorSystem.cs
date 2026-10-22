using System.Diagnostics.CodeAnalysis;
using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._DarkestHour.Clothing.ModularArmor;

/// <summary>
/// Модульная броня: слоты модулей, установка и снятие (с задержкой; тяжёлые — у стойки), скорость, спецсистема
/// (оружие, хранилище; автоаптечка — на сервере), ремонт сваркой. Защита по частям тела — на сервере.
/// </summary>
public sealed partial class DHModularArmorSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    private static readonly ProtoId<ToolQualityPrototype> Welding = "Welding";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHModularArmorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DHModularArmorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DHModularArmorComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<DHModularArmorComponent, GetVerbsEvent<EquipmentVerb>>(OnGetEquipmentVerbs);
        SubscribeLocalEvent<DHModularArmorComponent, DHArmorModuleInsertDoAfterEvent>(OnInsertDoAfter);
        SubscribeLocalEvent<DHModularArmorComponent, DHArmorModuleRemoveDoAfterEvent>(OnRemoveDoAfter);
        SubscribeLocalEvent<DHModularArmorComponent, InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshSpeed);
        SubscribeLocalEvent<DHModularArmorComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<DHModularArmorComponent, DHArmorSpecialActionEvent>(OnSpecialAction);
        SubscribeLocalEvent<DHModularArmorComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<DHModularArmorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DHModularArmorComponent, EntInsertedIntoContainerMessage>(OnContentsChanged);
        SubscribeLocalEvent<DHModularArmorComponent, EntRemovedFromContainerMessage>(OnContentsRemoved);

        SubscribeLocalEvent<DHArmorModuleComponent, AfterInteractEvent>(OnModuleAfterInteract);
        SubscribeLocalEvent<DHArmorModuleComponent, InteractUsingEvent>(OnModuleInteractUsing);
        SubscribeLocalEvent<DHArmorModuleComponent, DHArmorModuleRepairDoAfterEvent>(OnRepairDoAfter);
        SubscribeLocalEvent<DHArmorModuleComponent, ExaminedEvent>(OnModuleExamined);

        SubscribeLocalEvent<DHArmorSpecialComponent, MapInitEvent>(OnSpecialMapInit);
        SubscribeLocalEvent<DHArmorSpecialComponent, ComponentShutdown>(OnSpecialShutdown);
    }

    #region Корпус и модули

    private void OnMapInit(Entity<DHModularArmorComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.SpecialActionEntity, ent.Comp.SpecialAction);

        foreach (var slot in DHModularArmor.Slots)
        {
            var container = _container.EnsureContainer<ContainerSlot>(ent, DHModularArmor.ContainerId(slot));
            if (_net.IsClient || container.ContainedEntity != null ||
                !ent.Comp.StartingModules.TryGetValue(slot, out var proto))
            {
                continue;
            }

            var module = Spawn(proto, Transform(ent).Coordinates);
            if (!_container.Insert(module, container, force: true))
                QueueDel(module);
        }

        Dirty(ent);
    }

    public EntityUid? GetModule(EntityUid suit, DHArmorSlot slot)
    {
        return _container.TryGetContainer(suit, DHModularArmor.ContainerId(slot), out var container) &&
               container is ContainerSlot { ContainedEntity: { } module }
            ? module
            : null;
    }

    public IEnumerable<(DHArmorSlot Slot, EntityUid Module, DHArmorModuleComponent? Comp)> GetModules(EntityUid suit)
    {
        foreach (var slot in DHModularArmor.Slots)
        {
            if (GetModule(suit, slot) is { } module)
                yield return (slot, module, CompOrNull<DHArmorModuleComponent>(module));
        }
    }

    /// <summary>Кто носит корпус (null — лежит или в руках).</summary>
    public EntityUid? GetWearer(EntityUid suit)
    {
        if (!_container.TryGetContainingContainer((suit, null, null), out var container))
            return null;

        return _inventory.TryGetSlotEntity(container.Owner, "outerClothing", out var worn) && worn == suit
            ? container.Owner
            : null;
    }

    private bool NearRack(EntityUid suit, EntityUid user)
    {
        foreach (var rack in _lookup.GetEntitiesInRange<DHArmorRackComponent>(Transform(user).Coordinates, 2.5f))
        {
            return true;
        }

        return false;
    }

    private bool CanSwap(Entity<DHModularArmorComponent> ent, DHArmorModuleComponent module, EntityUid user,
        [NotNullWhen(false)] out string? reason)
    {
        if (module.Faction != string.Empty && module.Faction != ent.Comp.Faction)
        {
            reason = Loc.GetString("dh-armor-module-wrong-faction");
            return false;
        }

        if (module.Class == DHArmorClass.Heavy && !NearRack(ent, user))
        {
            reason = Loc.GetString("dh-armor-module-needs-rack");
            return false;
        }

        reason = null;
        return true;
    }

    private float SwapTime(Entity<DHModularArmorComponent> ent, DHArmorModuleComponent module)
    {
        return ent.Comp.SwapTime.TryGetValue(module.Class, out var time) ? time : 4f;
    }

    private void TryStartInsert(Entity<DHModularArmorComponent> ent, Entity<DHArmorModuleComponent> module, EntityUid user)
    {
        var slot = module.Comp.Slot;
        if (GetModule(ent, slot) != null)
        {
            _popup.PopupClient(Loc.GetString("dh-armor-module-slot-taken"), ent, user);
            return;
        }

        if (!CanSwap(ent, module.Comp, user, out var reason))
        {
            _popup.PopupClient(reason, ent, user);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, SwapTime(ent, module.Comp),
            new DHArmorModuleInsertDoAfterEvent(slot), ent, target: ent, used: module)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnInteractUsing(Entity<DHModularArmorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<DHArmorModuleComponent>(args.Used, out var module))
            return;

        args.Handled = true;
        TryStartInsert(ent, (args.Used, module), args.User);
    }

    /// <summary>Модуль можно ставить, щёлкнув по себе или напарнику в этой броне.</summary>
    private void OnModuleAfterInteract(Entity<DHArmorModuleComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!_inventory.TryGetSlotEntity(target, "outerClothing", out var suit) ||
            !TryComp<DHModularArmorComponent>(suit, out var armor))
        {
            return;
        }

        args.Handled = true;
        TryStartInsert((suit.Value, armor), ent, args.User);
    }

    private void OnInsertDoAfter(Entity<DHModularArmorComponent> ent, ref DHArmorModuleInsertDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used ||
            !TryComp<DHArmorModuleComponent>(used, out var module) ||
            !_container.TryGetContainer(ent, DHModularArmor.ContainerId(args.Slot), out var container) ||
            GetModule(ent, args.Slot) != null ||
            !CanSwap(ent, module, args.User, out _))
        {
            return;
        }

        args.Handled = true;
        if (_container.TryGetContainingContainer((used, null, null), out var current))
            _container.Remove(used, current, force: true);
        _container.Insert(used, container, force: true);
        _popup.PopupClient(Loc.GetString("dh-armor-module-installed", ("module", Name(used))), ent, args.User);
    }

    private void StartRemove(Entity<DHModularArmorComponent> ent, DHArmorSlot slot, EntityUid user)
    {
        if (GetModule(ent, slot) is not { } module)
            return;

        if (TryComp<DHArmorModuleComponent>(module, out var comp) && !CanSwap(ent, comp, user, out var reason))
        {
            _popup.PopupClient(reason, ent, user);
            return;
        }

        var time = comp != null ? SwapTime(ent, comp) : 3f;
        var doAfter = new DoAfterArgs(EntityManager, user, time, new DHArmorModuleRemoveDoAfterEvent(slot), ent,
            target: ent, used: module)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnRemoveDoAfter(Entity<DHModularArmorComponent> ent, ref DHArmorModuleRemoveDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || GetModule(ent, args.Slot) is not { } module ||
            !_container.TryGetContainer(ent, DHModularArmor.ContainerId(args.Slot), out var container))
        {
            return;
        }

        args.Handled = true;
        RetractWeapon(module);
        _container.Remove(module, container, force: true);
        _hands.PickupOrDrop(args.User, module);
    }

    private void AddRemoveVerbs(Entity<DHModularArmorComponent> ent, EntityUid user, List<Verb> verbs)
    {
        foreach (var (slot, module, _) in GetModules(ent))
        {
            var s = slot;
            verbs.Add(new Verb
            {
                Text = Loc.GetString("dh-armor-verb-remove", ("module", Name(module))),
                Category = VerbCategory.Eject,
                Act = () => StartRemove(ent, s, user),
                Priority = -1,
            });
        }
    }

    private void OnGetVerbs(Entity<DHModularArmorComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        AddRemoveVerbs(ent, args.User, args.Verbs);
    }

    private void OnGetEquipmentVerbs(Entity<DHModularArmorComponent> ent, ref GetVerbsEvent<EquipmentVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        foreach (var (slot, module, _) in GetModules(ent))
        {
            var s = slot;
            var user = args.User;
            args.Verbs.Add(new EquipmentVerb
            {
                Text = Loc.GetString("dh-armor-verb-remove", ("module", Name(module))),
                Act = () => StartRemove(ent, s, user),
            });
        }
    }

    private void OnContentsChanged(Entity<DHModularArmorComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        Refresh(ent);
    }

    private void OnContentsRemoved(Entity<DHModularArmorComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        Refresh(ent);
    }

    /// <summary>Модули поменялись: скорость и внешний вид носителя.</summary>
    public void Refresh(EntityUid suit)
    {
        _item.VisualsChanged(suit);
        if (GetWearer(suit) is { } wearer)
            _movement.RefreshMovementSpeedModifiers(wearer);
    }

    private void OnRefreshSpeed(Entity<DHModularArmorComponent> ent, ref InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        var speed = 1f;
        foreach (var (_, _, module) in GetModules(ent))
        {
            if (module != null)
                speed *= module.Speed;
        }

        args.Args.ModifySpeed(speed, speed);
    }

    private void OnExamined(Entity<DHModularArmorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(DHModularArmorComponent)))
        {
            var any = false;
            foreach (var (slot, module, comp) in GetModules(ent))
            {
                any = true;
                args.PushMarkup(comp == null || slot == DHArmorSlot.Special
                    ? Loc.GetString("dh-armor-examine-special", ("module", Name(module)))
                    : Loc.GetString("dh-armor-examine-module", ("module", Name(module)),
                        ("percent", (int) MathF.Round(comp.Integrity / MathF.Max(1f, comp.MaxIntegrity) * 100f))));
            }

            if (!any)
                args.PushMarkup(Loc.GetString("dh-armor-examine-empty"));
        }
    }

    #endregion

    #region Модуль: осмотр и ремонт

    private void OnModuleExamined(Entity<DHArmorModuleComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("dh-armor-module-examine-class", ("class", Loc.GetString("dh-armor-class-" + ent.Comp.Class.ToString().ToLowerInvariant()))));
        if (ent.Comp.Slot == DHArmorSlot.Special)
            return;

        args.PushMarkup(ent.Comp.Broken
            ? Loc.GetString("dh-armor-module-examine-broken")
            : Loc.GetString("dh-armor-module-examine-integrity",
                ("percent", (int) MathF.Round(ent.Comp.Integrity / MathF.Max(1f, ent.Comp.MaxIntegrity) * 100f))));
    }

    private void OnModuleInteractUsing(Entity<DHArmorModuleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Integrity >= ent.Comp.MaxIntegrity)
            return;

        if (_tool.UseTool(args.Used, args.User, ent, 4f, Welding, new DHArmorModuleRepairDoAfterEvent(), fuel: 5f))
            args.Handled = true;
    }

    private void OnRepairDoAfter(Entity<DHArmorModuleComponent> ent, ref DHArmorModuleRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Integrity = MathF.Min(ent.Comp.MaxIntegrity, ent.Comp.Integrity + ent.Comp.RepairAmount);
        Dirty(ent);
        _popup.PopupClient(Loc.GetString("dh-armor-module-repaired"), ent, args.User);
    }

    #endregion

    #region Спецсистема

    private void OnSpecialMapInit(Entity<DHArmorSpecialComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Kind != DHArmorSpecialKind.Weapon || ent.Comp.Weapon == null)
            return;

        var slot = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.WeaponContainer);
        if (_net.IsClient || ent.Comp.WeaponEntity != null)
            return;

        var weapon = Spawn(ent.Comp.Weapon.Value, Transform(ent).Coordinates);
        EnsureComp<DHArmorWeaponItemComponent>(weapon).Module = ent;
        _container.Insert(weapon, slot, force: true);
        ent.Comp.WeaponEntity = weapon;
        Dirty(ent);
    }

    private void OnSpecialShutdown(Entity<DHArmorSpecialComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsServer && ent.Comp.WeaponEntity is { } weapon && !TerminatingOrDeleted(weapon))
            QueueDel(weapon);
    }

    private void OnGetActions(Entity<DHModularArmorComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags is not { } slots || (slots & SlotFlags.OUTERCLOTHING) == 0)
            return;

        if (GetModule(ent, DHArmorSlot.Special) != null)
            args.AddAction(ent.Comp.SpecialActionEntity);
    }

    private void OnSpecialAction(Entity<DHModularArmorComponent> ent, ref DHArmorSpecialActionEvent args)
    {
        if (args.Handled || GetModule(ent, DHArmorSlot.Special) is not { } module ||
            !TryComp<DHArmorSpecialComponent>(module, out var special))
        {
            return;
        }

        args.Handled = true;
        var user = args.Performer;

        switch (special.Kind)
        {
            case DHArmorSpecialKind.Storage:
                if (_net.IsServer)
                    _storage.OpenStorageUI(module, user, silent: false);
                break;
            case DHArmorSpecialKind.Weapon:
                ToggleWeapon((module, special), user);
                break;
            case DHArmorSpecialKind.AutoMed:
                _popup.PopupClient(Loc.GetString("dh-armor-automed-status"), user, user);
                break;
        }
    }

    private void ToggleWeapon(Entity<DHArmorSpecialComponent> module, EntityUid user)
    {
        if (_net.IsClient || module.Comp.WeaponEntity is not { } weapon || TerminatingOrDeleted(weapon) ||
            !_container.TryGetContainer(module, module.Comp.WeaponContainer, out var slot))
        {
            return;
        }

        if (slot.Contains(weapon))
        {
            _container.Remove(weapon, slot, force: true);
            if (!_hands.TryPickupAnyHand(user, weapon, checkActionBlocker: false))
            {
                _container.Insert(weapon, slot, force: true);
                _popup.PopupEntity(Loc.GetString("dh-suit-weapon-no-hand"), user, user);
            }

            return;
        }

        RetractWeapon(module);
    }

    /// <summary>Убирает встроенное оружие спецсистемы обратно в модуль.</summary>
    public void RetractWeapon(EntityUid module)
    {
        if (_net.IsClient || !TryComp<DHArmorSpecialComponent>(module, out var special) ||
            special.WeaponEntity is not { } weapon || TerminatingOrDeleted(weapon) ||
            !_container.TryGetContainer(module, special.WeaponContainer, out var slot) ||
            slot.Contains(weapon))
        {
            return;
        }

        if (_container.TryGetContainingContainer((weapon, null, null), out var current))
            _container.Remove(weapon, current, force: true);
        _container.Insert(weapon, slot, force: true);
    }

    private void OnUnequipped(Entity<DHModularArmorComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (GetModule(ent, DHArmorSlot.Special) is { } module)
            RetractWeapon(module);

        _movement.RefreshMovementSpeedModifiers(args.Wearer);
    }

    #endregion
}
