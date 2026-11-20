using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Squads;
using Content.Shared.Actions;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Audio;

namespace Content.Server._DarkestHour.Squads;

/// <summary>
/// Приказы ком-модуля (план осады п. 26): отдавший приказ и свои живые бойцы в радиусе получают его на
/// <see cref="DHOrdersModuleComponent.Duration"/>; все три приказа уходят на общую перезарядку.
/// </summary>
public sealed class DHOrdersServerSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DHOrdersSystem _orders = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly SoundSpecifier OrderSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHOrdersModuleComponent, DHOrderActionEvent>(OnOrder);
    }

    private void OnOrder(Entity<DHOrdersModuleComponent> ent, ref DHOrderActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.Performer;
        if (!_mobState.IsAlive(user))
            return;

        var faction = CompOrNull<DHAssaultFighterComponent>(user)?.Faction;
        var shout = Loc.GetString("dh-order-shout-" + args.Order.ToString().ToLowerInvariant());
        _popup.PopupEntity(Loc.GetString("dh-order-issued", ("user", user), ("order", shout)), user, PopupType.Large);
        _audio.PlayPvs(OrderSound, user);

        _orders.Apply(user, args.Order, ent.Comp.Duration);
        foreach (var ally in _lookup.GetEntitiesInRange<DHAssaultFighterComponent>(Transform(user).Coordinates, ent.Comp.Radius))
        {
            if (ally.Owner == user || ally.Comp.Faction != faction || !_mobState.IsAlive(ally))
                continue;

            _orders.Apply(ally, args.Order, ent.Comp.Duration);
            _popup.PopupEntity(Loc.GetString("dh-order-received-" + args.Order.ToString().ToLowerInvariant()), ally,
                ally, PopupType.Medium);
        }

        foreach (var action in ent.Comp.ActionEntities)
        {
            _actions.SetCooldown(action, ent.Comp.Cooldown);
        }
    }
}
