using Content.Shared.Examine;
using Content.Shared.Inventory.Events;
using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Clothing.ModularArmor;

/// <summary>
/// Наплечник / наколенник (решение автора 2026-11-06): надевается в слот руки (armLeft/armRight) или ноги
/// (legLeft/legRight) — те же слоты, что заняты модулями у модульной брони — и защищает эту руку или ногу своим
/// классом (<see cref="DHPartArmorComponent"/>: часть тела выставляется по слоту при надевании).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHLimbPadsComponent : Component
{
    [DataField]
    public int ArmorClass = 1;
}

public sealed partial class DHLimbPadsSystem : EntitySystem
{
    private static readonly Dictionary<string, string> SlotParts = new()
    {
        ["armLeft"] = "ArmLeft",
        ["armRight"] = "ArmRight",
        ["legLeft"] = "LegLeft",
        ["legRight"] = "LegRight",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHLimbPadsComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<DHLimbPadsComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<DHLimbPadsComponent, ExaminedEvent>(OnExamined);
    }

    private void OnEquipped(Entity<DHLimbPadsComponent> ent, ref GotEquippedEvent args)
    {
        if (!SlotParts.TryGetValue(args.Slot, out var part))
            return;

        var armor = EnsureComp<DHPartArmorComponent>(ent);
        armor.Parts.Clear();
        armor.Classes.Clear();
        armor.Parts.Add(part);
        armor.ArmorClass = ent.Comp.ArmorClass;
        Dirty(ent.Owner, armor);
    }

    private void OnUnequipped(Entity<DHLimbPadsComponent> ent, ref GotUnequippedEvent args)
    {
        if (!TryComp<DHPartArmorComponent>(ent, out var armor))
            return;

        armor.Parts.Clear();
        armor.Classes.Clear();
        Dirty(ent.Owner, armor);
    }

    private void OnExamined(Entity<DHLimbPadsComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("dh-limb-pads-examine", ("level", ent.Comp.ArmorClass)));
    }
}
