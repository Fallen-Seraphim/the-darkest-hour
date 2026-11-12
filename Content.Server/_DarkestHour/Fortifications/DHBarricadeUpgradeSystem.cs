using Content.Server.Stack;
using Content.Shared._DarkestHour.Fortifications;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Fortifications;

/// <summary>
/// Улучшения металлических и пласталевых баррикад (по образцу RMCUpgradeSystem RMC-14, MIT; решение автора
/// 2026-11-12). Два листа стали в руке — действия «Улучшить: …» на закреплённой баррикаде; баррикада заменяется
/// улучшенной (урон и колючка переносятся). Мультитулом улучшение снимается, листы выпадают.
/// </summary>
public sealed partial class DHBarricadeUpgradeSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DHWireableSystem _wire = default!;

    private static readonly ProtoId<ToolQualityPrototype> PulsingQuality = "Pulsing";
    private static readonly VerbCategory UpgradeCategory = new("dh-barricade-upgrade-category", null);
    private static readonly SoundSpecifier UpgradeSound = new SoundPathSpecifier("/Audio/Items/ratchet.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHBarricadeUpgradeableComponent, GetVerbsEvent<Verb>>(OnUpgradeVerbs);
        SubscribeLocalEvent<DHBarricadeUpgradeableComponent, DHBarricadeUpgradeDoAfterEvent>(OnUpgradeDoAfter);
        SubscribeLocalEvent<DHBarricadeUpgradedComponent, GetVerbsEvent<Verb>>(OnDowngradeVerbs);
        SubscribeLocalEvent<DHBarricadeUpgradedComponent, DHBarricadeDowngradeDoAfterEvent>(OnDowngradeDoAfter);
    }

    private EntityUid? HeldMaterial(EntityUid user, DHBarricadeUpgradeableComponent comp)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (TryComp<StackComponent>(held, out var stack) && stack.StackTypeId == comp.Material)
                return held;
        }

        return null;
    }

    private void OnUpgradeVerbs(Entity<DHBarricadeUpgradeableComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Upgrades.Count == 0 || !Transform(ent).Anchored ||
            HeldMaterial(args.User, ent.Comp) is not { } material)
        {
            return;
        }

        var user = args.User;
        var enough = _stack.GetCount(material) >= ent.Comp.Amount;
        for (var i = 0; i < ent.Comp.Upgrades.Count; i++)
        {
            var index = i;
            var upgrade = ent.Comp.Upgrades[i];
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString(upgrade.Name),
                Category = UpgradeCategory,
                Disabled = !enough,
                Message = enough ? null : Loc.GetString("dh-barricade-upgrade-no-material", ("amount", ent.Comp.Amount)),
                Act = () => _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.Delay,
                    new DHBarricadeUpgradeDoAfterEvent(index), ent, ent, material)
                {
                    BreakOnMove = true,
                    NeedHand = true,
                }),
                Priority = -i,
            });
        }
    }

    private void OnUpgradeDoAfter(Entity<DHBarricadeUpgradeableComponent> ent, ref DHBarricadeUpgradeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } material ||
            args.Index < 0 || args.Index >= ent.Comp.Upgrades.Count || !Transform(ent).Anchored)
        {
            return;
        }

        args.Handled = true;
        if (!_stack.TryUse(material, ent.Comp.Amount))
        {
            _popup.PopupEntity(Loc.GetString("dh-barricade-upgrade-no-material", ("amount", ent.Comp.Amount)), ent,
                args.User);
            return;
        }

        var upgrade = ent.Comp.Upgrades[args.Index];
        Replace(ent, upgrade.Result, args.User, Loc.GetString(upgrade.Popup));
    }

    private void OnDowngradeVerbs(Entity<DHBarricadeUpgradedComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using is not { } tool ||
            !_tool.HasQuality(tool, PulsingQuality.Id))
        {
            return;
        }

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("dh-barricade-downgrade-verb"),
            Act = () => _tool.UseTool(tool, user, ent, (float) ent.Comp.Delay.TotalSeconds, PulsingQuality,
                new DHBarricadeDowngradeDoAfterEvent()),
        });
    }

    private void OnDowngradeDoAfter(Entity<DHBarricadeUpgradedComponent> ent, ref DHBarricadeDowngradeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var refund = Spawn(ent.Comp.Refund, _transform.GetMapCoordinates(ent));
        _stack.SetCount(refund, ent.Comp.RefundAmount);
        Replace(ent, ent.Comp.Base, args.User, Loc.GetString("dh-barricade-downgraded"));
    }

    /// <summary>Заменить баррикаду другой: то же место и направление, урон и колючка переносятся.</summary>
    private void Replace(EntityUid old, EntProtoId result, EntityUid user, string popup)
    {
        var xform = Transform(old);
        var coords = xform.Coordinates;
        var rotation = xform.LocalRotation;

        DamageSpecifier? damage = null;
        if (TryComp<DamageableComponent>(old, out var damageable))
            damage = _damageable.GetPositiveDamage((old, damageable));

        var wired = TryComp<DHWireableComponent>(old, out var wire) && wire.Wired;

        // Сначала убрать старую, иначе новая не встанет на занятое место.
        QueueDel(old);
        var spawned = Spawn(result, coords);
        var newXform = Transform(spawned);
        _transform.SetLocalRotation(spawned, rotation, newXform);
        if (!newXform.Anchored)
            _transform.AnchorEntity(spawned, newXform);

        if (damage != null)
            _damageable.SetDamage(spawned, damage);

        if (wired && TryComp<DHWireableComponent>(spawned, out var newWire))
            _wire.SetWired((spawned, newWire), true);

        _audio.PlayPvs(UpgradeSound, spawned);
        _popup.PopupEntity(popup, spawned, user);
    }
}
