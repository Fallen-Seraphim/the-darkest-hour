using Content.Shared._DarkestHour.Weapons.Ballistics;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._DarkestHour.Clothing.ModularArmor;

/// <summary>
/// DarkestHour (решение автора 2026-11-01): вместо значка «контрабанда» при осмотре — значок класса брони у брони и
/// класса бронепробития у патронов, снарядов и магазинов. Наведи на значок — подробности.
/// </summary>
public sealed partial class DHArmorClassExamineSystem : EntitySystem
{
    [Dependency] private DHModularArmorSystem _armor = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private const string ArmorIcon = "/Textures/Interface/VerbIcons/lock-red.svg.192dpi.png";
    private const string PenetrationIcon = "/Textures/Interface/VerbIcons/dot.svg.192dpi.png";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHPartArmorComponent, GetVerbsEvent<ExamineVerb>>(OnPartArmorVerb);
        SubscribeLocalEvent<DHArmorModuleComponent, GetVerbsEvent<ExamineVerb>>(OnModuleVerb);
        SubscribeLocalEvent<DHModularArmorComponent, GetVerbsEvent<ExamineVerb>>(OnSuitVerb);
        SubscribeLocalEvent<DHArmorLimbCoverComponent, GetVerbsEvent<ExamineVerb>>(OnCoverVerb);

        SubscribeLocalEvent<DHBallisticsComponent, GetVerbsEvent<ExamineVerb>>(OnProjectileVerb);
        SubscribeLocalEvent<CartridgeAmmoComponent, GetVerbsEvent<ExamineVerb>>(OnCartridgeVerb);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, GetVerbsEvent<ExamineVerb>>(OnBallisticProviderVerb);
    }

    #region Броня

    private void OnPartArmorVerb(Entity<DHPartArmorComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || ent.Comp.ArmorClass <= 0)
            return;

        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("dh-armor-verb-class", ("level", ent.Comp.ArmorClass)));
        msg.PushNewline();
        msg.AddMarkupOrThrow(Loc.GetString("dh-armor-verb-parts", ("parts", PartsText(ent.Comp.Parts))));
        AddArmorVerb(args, ent.Comp, msg);
    }

    private void OnModuleVerb(Entity<DHArmorModuleComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || ent.Comp.Slot == DHArmorSlot.Special)
            return;

        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(ModuleLine(ent.Comp.Slot, ent.Comp));
        AddArmorVerb(args, ent.Comp, msg);
    }

    private void OnSuitVerb(Entity<DHModularArmorComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract)
            return;

        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("dh-armor-verb-suit"));
        foreach (var slot in DHModularArmor.Slots)
        {
            if (slot == DHArmorSlot.Special)
                continue;

            msg.PushNewline();
            var module = _armor.GetModule(ent, slot);
            msg.AddMarkupOrThrow(module is { } m && TryComp<DHArmorModuleComponent>(m, out var comp)
                ? ModuleLine(slot, comp)
                : Loc.GetString("dh-armor-verb-slot-empty", ("slot", SlotName(slot))));
        }

        AddArmorVerb(args, ent.Comp, msg);
    }

    private void OnCoverVerb(Entity<DHArmorLimbCoverComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || ent.Comp.Suit is not { } suit)
            return;

        var msg = new FormattedMessage();
        var first = true;
        foreach (var slot in ent.Comp.Slots)
        {
            if (!first)
                msg.PushNewline();
            first = false;

            var module = _armor.GetModule(suit, slot);
            msg.AddMarkupOrThrow(module is { } m && TryComp<DHArmorModuleComponent>(m, out var comp)
                ? ModuleLine(slot, comp)
                : Loc.GetString("dh-armor-verb-slot-empty", ("slot", SlotName(slot))));
        }

        AddArmorVerb(args, ent.Comp, msg);
    }

    private string ModuleLine(DHArmorSlot slot, DHArmorModuleComponent module)
    {
        if (module.Broken)
            return Loc.GetString("dh-armor-verb-module-broken", ("slot", SlotName(slot)));

        return Loc.GetString("dh-armor-verb-module", ("slot", SlotName(slot)),
            ("level", DHModularArmor.Level(module.Class)),
            ("class", Loc.GetString("dh-armor-class-" + module.Class.ToString().ToLowerInvariant())),
            ("percent", (int) MathF.Round(module.Integrity / MathF.Max(1f, module.MaxIntegrity) * 100f)));
    }

    private string SlotName(DHArmorSlot slot)
    {
        return Loc.GetString("dh-armor-slot-" + slot.ToString().ToLowerInvariant());
    }

    private string PartsText(List<string> parts)
    {
        var names = new List<string>();
        foreach (var part in parts)
        {
            names.Add(Loc.GetString("dh-armor-part-" + part.ToLowerInvariant()));
        }

        return string.Join(", ", names);
    }

    private void AddArmorVerb(GetVerbsEvent<ExamineVerb> args, Component component, FormattedMessage msg)
    {
        _examine.AddHoverExamineVerb(args, component, Loc.GetString("dh-armor-verb-text"), msg.ToMarkup(), ArmorIcon);
    }

    #endregion

    #region Бронепробитие

    private void OnProjectileVerb(Entity<DHBallisticsComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (args.CanInteract)
            AddPenetrationVerb(args, ent.Comp, ent.Comp.PenetrationClass);
    }

    private void OnCartridgeVerb(Entity<CartridgeAmmoComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (args.CanInteract && PenetrationOf(ent.Comp.Prototype) is { } level)
            AddPenetrationVerb(args, ent.Comp, level);
    }

    private void OnBallisticProviderVerb(Entity<BallisticAmmoProviderComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || ent.Comp.Proto is not { } cartridge ||
            !_proto.TryIndex(cartridge, out var proto) ||
            !proto.TryGetComponent<CartridgeAmmoComponent>(out var ammo, EntityManager.ComponentFactory) ||
            PenetrationOf(ammo.Prototype) is not { } level)
        {
            return;
        }

        AddPenetrationVerb(args, ent.Comp, level);
    }

    /// <summary>Класс бронепробития пули/снаряда по прототипу; null — не пуля DH.</summary>
    private int? PenetrationOf(EntProtoId projectile)
    {
        return _proto.TryIndex(projectile, out var proto) &&
               proto.TryGetComponent<DHBallisticsComponent>(out var ballistics, EntityManager.ComponentFactory)
            ? ballistics.PenetrationClass
            : null;
    }

    private void AddPenetrationVerb(GetVerbsEvent<ExamineVerb> args, Component component, int level)
    {
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("dh-penetration-verb-class", ("level", level)));
        msg.PushNewline();
        msg.AddMarkupOrThrow(Loc.GetString("dh-penetration-verb-hint", ("level", level)));
        _examine.AddHoverExamineVerb(args, component, Loc.GetString("dh-penetration-verb-text"), msg.ToMarkup(),
            PenetrationIcon);
    }

    #endregion
}
