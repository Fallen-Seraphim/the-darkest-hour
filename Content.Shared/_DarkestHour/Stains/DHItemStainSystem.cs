// Перенесено из RussianCM (CMU14, github.com/flex5hybrid/RussianCM, AGPL v3): CMUItemStainSystem.
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Shared.Network;

namespace Content.Shared._DarkestHour.Stains;

/// <summary>Пятна на предметах: состояние (сервер), осмотр, помощники «запачкать/отмыть» по слотам.</summary>
public sealed partial class DHItemStainSystem : EntitySystem
{
    public const string HeadSlot = "head";
    public const string JumpsuitSlot = "jumpsuit";
    public const string GlovesSlot = "gloves";
    public const string MaskSlot = "mask";
    public const string OuterClothingSlot = "outerClothing";
    public const string ShoesSlot = "shoes";

    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHItemStainComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<DHItemStainComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Color is not { } color)
            return;

        var message = ent.Comp.Kind == DHItemStainKind.Oil ? "dh-item-stain-examine-oil" : "dh-item-stain-examine-blood";
        args.PushMarkup(Loc.GetString(message, ("color", color.ToHex())));
    }

    /// <summary>Запачкать предмет (только сервер).</summary>
    public bool TryStain(EntityUid item, DHItemStainKind kind, Color color, DHItemStainComponent? component = null)
    {
        if (_net.IsClient || !HasComp<ItemComponent>(item))
            return false;

        component ??= EnsureComp<DHItemStainComponent>(item);
        if (!component.CanStain || component.Color == color && component.Kind == kind)
            return false;

        component.Kind = kind;
        component.Color = color;
        Dirty(item, component);
        return true;
    }

    /// <summary>Отмыть предмет (только сервер).</summary>
    public bool TryClean(EntityUid item, DHItemStainComponent? component = null)
    {
        if (_net.IsClient || !Resolve(item, ref component, false) || component.Color == null)
            return false;

        component.Color = null;
        Dirty(item, component);
        return true;
    }

    public bool TryStainSlot(EntityUid wearer, string slot, DHItemStainKind kind, Color color)
    {
        return _inventory.TryGetSlotEntity(wearer, slot, out var item) && TryStain(item.Value, kind, color);
    }

    public bool TryCleanSlot(EntityUid wearer, string slot)
    {
        return _inventory.TryGetSlotEntity(wearer, slot, out var item) && TryClean(item.Value);
    }

    /// <summary>Пачкает бронежилет/верхнюю одежду, а если её нет — форму.</summary>
    public bool TryStainOuterBody(EntityUid wearer, DHItemStainKind kind, Color color)
    {
        if (_inventory.TryGetSlotEntity(wearer, OuterClothingSlot, out var outer))
            return TryStain(outer.Value, kind, color);

        return TryStainSlot(wearer, JumpsuitSlot, kind, color);
    }

    /// <summary>Пачкает всё открытое снаряжение (брызги, касание реагентом).</summary>
    public bool StainExposedEquipment(EntityUid wearer, DHItemStainKind kind, Color color)
    {
        var changed = TryStainOuterBody(wearer, kind, color);
        changed |= TryStainSlot(wearer, HeadSlot, kind, color);
        changed |= TryStainSlot(wearer, MaskSlot, kind, color);
        changed |= TryStainSlot(wearer, GlovesSlot, kind, color);
        changed |= TryStainSlot(wearer, ShoesSlot, kind, color);
        return changed;
    }

    public bool CleanExposedEquipment(EntityUid wearer)
    {
        var changed = false;
        if (_inventory.TryGetSlotEntity(wearer, OuterClothingSlot, out var outer))
            changed |= TryClean(outer.Value);
        changed |= TryCleanSlot(wearer, JumpsuitSlot);
        changed |= TryCleanSlot(wearer, HeadSlot);
        changed |= TryCleanSlot(wearer, MaskSlot);
        changed |= TryCleanSlot(wearer, GlovesSlot);
        changed |= TryCleanSlot(wearer, ShoesSlot);
        return changed;
    }
}
