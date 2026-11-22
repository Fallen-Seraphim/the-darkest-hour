using System.Numerics;
using Content.Shared._DarkestHour.Recon;
using Content.Shared.Actions;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server._DarkestHour.Recon;

/// <summary>
/// Маскхалат снайпера, как в RMC (план осады п. 13): способность — замереть на <see cref="DHGhillieComponent.PrepareTime"/>,
/// после чего носитель почти невидим. Шаг, выстрел, ранение, снятие халата или повторное нажатие раскрывают; после
/// этого — перезарядка.
/// </summary>
public sealed partial class DHGhillieSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHGhillieComponent, DHGhillieActionEvent>(OnAction);
        SubscribeLocalEvent<DHGhillieComponent, DHGhillieDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<DHGhillieComponent, GotUnequippedEvent>(OnUnequipped);

        SubscribeLocalEvent<DHGhillieHiddenComponent, ShotAttemptedEvent>(OnShot);
        SubscribeLocalEvent<DHGhillieHiddenComponent, DamageDealtEvent>(OnDamaged);
        SubscribeLocalEvent<DHGhillieHiddenComponent, MobStateChangedEvent>(OnMobState);
    }

    private void OnAction(Entity<DHGhillieComponent> ent, ref DHGhillieActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.Performer;

        // Повторное нажатие — выйти из маскировки самому.
        if (TryComp<DHGhillieHiddenComponent>(user, out var hidden))
        {
            Reveal((user, hidden), "dh-ghillie-revealed-self");
            return;
        }

        if (HasComp<StealthComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("dh-ghillie-already-hidden"), user, user);
            return;
        }

        if (!_inventory.TryGetContainingSlot(ent.Owner, out var slot) || (slot.SlotFlags & ent.Comp.RequiredSlot) == 0)
            return;

        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.PrepareTime,
            new DHGhillieDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        });

        if (started)
            _popup.PopupEntity(Loc.GetString("dh-ghillie-preparing"), user, user);
    }

    private void OnDoAfter(Entity<DHGhillieComponent> ent, ref DHGhillieDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var user = args.User;
        if (HasComp<StealthComponent>(user) || !_inventory.TryGetContainingSlot(ent.Owner, out var slot) ||
            (slot.SlotFlags & ent.Comp.RequiredSlot) == 0)
        {
            return;
        }

        var stealth = AddComp<StealthComponent>(user);
        stealth.ExamineThreshold = 0.5f;
        _stealth.SetVisibility(user, ent.Comp.Visibility, stealth);

        var hidden = AddComp<DHGhillieHiddenComponent>(user);
        hidden.Suit = ent;
        hidden.Anchor = _transform.GetWorldPosition(user);
        Dirty(user, hidden);

        _popup.PopupEntity(Loc.GetString("dh-ghillie-hidden"), user, user);
    }

    private void OnUnequipped(Entity<DHGhillieComponent> ent, ref GotUnequippedEvent args)
    {
        if (TryComp<DHGhillieHiddenComponent>(args.EquipTarget, out var hidden) && hidden.Suit == ent.Owner)
            Reveal((args.EquipTarget, hidden), null);
    }

    private void OnShot(Entity<DHGhillieHiddenComponent> ent, ref ShotAttemptedEvent args)
    {
        Reveal(ent, "dh-ghillie-revealed-shot");
    }

    private void OnDamaged(Entity<DHGhillieHiddenComponent> ent, ref DamageDealtEvent args)
    {
        if (args.Damage.GetTotal() > 0)
            Reveal(ent, "dh-ghillie-revealed-hit");
    }

    private void OnMobState(Entity<DHGhillieHiddenComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            Reveal(ent, null);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toReveal = new List<Entity<DHGhillieHiddenComponent>>();
        var query = EntityQueryEnumerator<DHGhillieHiddenComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var hidden, out var xform))
        {
            var tolerance = TryComp<DHGhillieComponent>(hidden.Suit, out var suit) ? suit.MoveTolerance : 0.3f;
            if (Vector2.Distance(_transform.GetWorldPosition(xform), hidden.Anchor) > tolerance)
                toReveal.Add((uid, hidden));
        }

        foreach (var ent in toReveal)
        {
            Reveal(ent, "dh-ghillie-revealed-move");
        }
    }

    /// <summary>Снять маскировку и запустить перезарядку способности.</summary>
    public void Reveal(Entity<DHGhillieHiddenComponent> ent, string? message)
    {
        if (TerminatingOrDeleted(ent) || !HasComp<DHGhillieHiddenComponent>(ent))
            return;

        var suit = ent.Comp.Suit;
        RemComp<DHGhillieHiddenComponent>(ent);
        RemComp<StealthComponent>(ent);

        if (TryComp<DHGhillieComponent>(suit, out var ghillie) && ghillie.ActionEntity is { } action)
            _actions.SetCooldown(action, ghillie.Cooldown);

        if (message != null)
            _popup.PopupEntity(Loc.GetString(message), ent, ent, PopupType.MediumCaution);
    }
}
