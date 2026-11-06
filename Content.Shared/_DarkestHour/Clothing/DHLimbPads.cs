using Content.Shared._DarkestHour.Clothing.ModularArmor;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Clothing.ModularArmor;

/// <summary>
/// Наплечники / наколенники (решение автора 2026-11-06): использовать в руке — за несколько секунд пристегнуть к
/// надетой форме. Форма получает класс брони на руки (наплечники) или ноги (наколенники) — как немодульная броня
/// (<see cref="DHPartArmorComponent.Classes"/>), класс не понижается, если на форме уже выше.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHLimbPadsComponent : Component
{
    [DataField(required: true)]
    public List<string> Parts = new();

    [DataField]
    public int ArmorClass = 1;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);
}

[Serializable, NetSerializable]
public sealed partial class DHLimbPadsDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class DHLimbPadsSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHLimbPadsComponent, UseInHandEvent>(OnUse);
        SubscribeLocalEvent<DHLimbPadsComponent, DHLimbPadsDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<DHLimbPadsComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<DHLimbPadsComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("dh-limb-pads-examine", ("level", ent.Comp.ArmorClass)));
    }

    private void OnUse(Entity<DHLimbPadsComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!_inventory.TryGetSlotEntity(args.User, "jumpsuit", out _))
        {
            _popup.PopupClient(Loc.GetString("dh-limb-pads-no-uniform"), args.User, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay,
            new DHLimbPadsDoAfterEvent(), ent.Owner, used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnDoAfter(Entity<DHLimbPadsComponent> ent, ref DHLimbPadsDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (!_inventory.TryGetSlotEntity(args.User, "jumpsuit", out var uniform))
            return;

        if (_net.IsClient)
            return;

        var armor = EnsureComp<DHPartArmorComponent>(uniform.Value);
        foreach (var part in ent.Comp.Parts)
        {
            if (armor.ClassFor(part) < ent.Comp.ArmorClass)
                armor.Classes[part] = ent.Comp.ArmorClass;
        }

        Dirty(uniform.Value, armor);
        _popup.PopupEntity(Loc.GetString("dh-limb-pads-attached", ("pads", ent.Owner), ("uniform", uniform.Value)),
            args.User, args.User);
        QueueDel(ent);
    }
}
