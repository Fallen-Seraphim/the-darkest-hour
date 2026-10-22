using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Clothing;

/// <summary>
/// Оружие, встроенное в скафандр: действием выдвигается в руку и убирается обратно. Из руки не выбрасывается и не
/// отдаётся; при снятии скафандра само возвращается внутрь.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHSuitWeaponComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Weapon;

    [DataField]
    public EntProtoId Action = "DHActionToggleSuitWeapon";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? WeaponEntity;

    [DataField]
    public string ContainerId = "dh_suit_weapon";

    /// <summary>Слот, в котором должен быть скафандр, чтобы оружием можно было пользоваться.</summary>
    [DataField]
    public SlotFlags RequiredSlot = SlotFlags.OUTERCLOTHING;
}

/// <summary>Метка встроенного оружия: к какому скафандру оно относится.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHSuitWeaponItemComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Suit;
}

public sealed partial class DHToggleSuitWeaponEvent : InstantActionEvent;

public sealed partial class DHSuitWeaponSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHSuitWeaponComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DHSuitWeaponComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<DHSuitWeaponComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<DHSuitWeaponComponent, DHToggleSuitWeaponEvent>(OnToggle);
        SubscribeLocalEvent<DHSuitWeaponComponent, ClothingGotUnequippedEvent>(OnUnequipped);
    }

    private void OnMapInit(Entity<DHSuitWeaponComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);

        var slot = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.ContainerId);
        if (_net.IsServer && ent.Comp.WeaponEntity == null)
        {
            var weapon = Spawn(ent.Comp.Weapon, Transform(ent).Coordinates);
            EnsureComp<DHSuitWeaponItemComponent>(weapon).Suit = ent;
            _container.Insert(weapon, slot, force: true);
            ent.Comp.WeaponEntity = weapon;
        }

        Dirty(ent);
    }

    private void OnShutdown(Entity<DHSuitWeaponComponent> ent, ref ComponentShutdown args)
    {
        // Оружие без скафандра не живёт: скафандр уничтожен — пропадает и оружие (даже если было в руке).
        if (_net.IsServer && ent.Comp.WeaponEntity is { } weapon && !TerminatingOrDeleted(weapon))
            QueueDel(weapon);
    }

    private void OnGetActions(Entity<DHSuitWeaponComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags is not { } slots || (slots & ent.Comp.RequiredSlot) == 0)
            return;

        args.AddAction(ent.Comp.ActionEntity);
    }

    private void OnToggle(Entity<DHSuitWeaponComponent> ent, ref DHToggleSuitWeaponEvent args)
    {
        if (args.Handled || ent.Comp.WeaponEntity is not { } weapon || TerminatingOrDeleted(weapon))
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        var user = args.Performer;
        if (!_container.TryGetContainer(ent, ent.Comp.ContainerId, out var slot))
            return;

        if (slot.Contains(weapon))
        {
            // выдвинуть в руку
            _container.Remove(weapon, slot, force: true);
            if (!_hands.TryPickupAnyHand(user, weapon, checkActionBlocker: false))
            {
                _container.Insert(weapon, slot, force: true);
                _popup.PopupEntity(Loc.GetString("dh-suit-weapon-no-hand"), user, user);
            }
            return;
        }

        Retract(ent, weapon, slot);
    }

    private void OnUnequipped(Entity<DHSuitWeaponComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (_net.IsClient ||
            ent.Comp.WeaponEntity is not { } weapon ||
            TerminatingOrDeleted(weapon) ||
            !_container.TryGetContainer(ent, ent.Comp.ContainerId, out var slot))
        {
            return;
        }

        Retract(ent, weapon, slot);
    }

    private void Retract(Entity<DHSuitWeaponComponent> ent, EntityUid weapon, BaseContainer slot)
    {
        if (slot.Contains(weapon))
            return;

        if (_container.TryGetContainingContainer(weapon, out var current))
            _container.Remove(weapon, current, force: true);

        _container.Insert(weapon, slot, force: true);
    }
}
