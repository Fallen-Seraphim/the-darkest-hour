using Content.Shared.Actions;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._DarkestHour.Squads;

// Ком-модуль связи и приказы (план осады п. 26, решение автора 2026-11-20 — как в RMC): спецсистема модульной брони
// даёт носителю три приказа. Приказ действует 10 с на своих в радиусе 7 клеток: «Держать позицию» — меньше урона,
// «В атаку» — точнее стрельба, «Двигаться» — быстрее бег. Перезарядка общая — 80 с.

[Serializable, NetSerializable]
public enum DHOrderType : byte
{
    Hold,
    Attack,
    Move,
}

/// <summary>Ком-модуль: способности приказов (выдаются носителю брони, в которую вставлен модуль).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHOrdersModuleComponent : Component
{
    [DataField]
    public List<EntProtoId> Actions = new() { "DHActionOrderHold", "DHActionOrderAttack", "DHActionOrderMove" };

    [DataField, AutoNetworkedField]
    public List<EntityUid> ActionEntities = new();

    /// <summary>Кому сейчас выданы приказы.</summary>
    [ViewVariables]
    public EntityUid? GrantedTo;

    [DataField]
    public float Radius = 7f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(80);
}

/// <summary>На бойце действуют приказы (до указанного времени).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHOrderedComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan HoldUntil;

    [DataField, AutoNetworkedField]
    public TimeSpan AttackUntil;

    [DataField, AutoNetworkedField]
    public TimeSpan MoveUntil;

    /// <summary>«Держать позицию»: доля получаемого урона.</summary>
    [DataField]
    public float HoldDamage = 0.8f;

    /// <summary>«В атаку»: разброс оружия.</summary>
    [DataField]
    public float AttackSpread = 0.7f;

    /// <summary>«Двигаться»: скорость.</summary>
    [DataField]
    public float MoveSpeed = 1.15f;

    /// <summary>Сколько приказов действовало при последнем обновлении (сервер: истёк один — обновить бойца).</summary>
    [ViewVariables]
    public int LastActive;
}

public sealed partial class DHOrderActionEvent : InstantActionEvent
{
    [DataField]
    public DHOrderType Order;
}

public sealed partial class DHOrdersSystem : EntitySystem
{
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHOrdersModuleComponent, MapInitEvent>(OnModuleMapInit);

        SubscribeLocalEvent<DHOrderedComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<DHOrderedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<GunComponent, GunRefreshModifiersEvent>(OnGunRefresh);
    }

    private void OnModuleMapInit(Entity<DHOrdersModuleComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        ent.Comp.ActionEntities.Clear();
        foreach (var proto in ent.Comp.Actions)
        {
            if (_actionContainer.AddAction(ent, proto.Id) is { } action)
                ent.Comp.ActionEntities.Add(action);
        }

        Dirty(ent);
    }

    /// <summary>Модуль вставлен в надетую броню (или снят с неё): приказы — носителю или забрать.</summary>
    public void Sync(EntityUid module, EntityUid? wearer)
    {
        if (_net.IsClient || !TryComp<DHOrdersModuleComponent>(module, out var comp) || comp.GrantedTo == wearer)
            return;

        if (comp.GrantedTo is { } previous && !TerminatingOrDeleted(previous))
            _actions.RemoveProvidedActions(previous, module);

        comp.GrantedTo = wearer;
        if (wearer is { } user)
            _actions.GrantActions(user, comp.ActionEntities, module);
    }

    public bool Active(DHOrderedComponent comp, DHOrderType order)
    {
        var now = _timing.CurTime;
        return order switch
        {
            DHOrderType.Hold => now < comp.HoldUntil,
            DHOrderType.Attack => now < comp.AttackUntil,
            DHOrderType.Move => now < comp.MoveUntil,
            _ => false,
        };
    }

    /// <summary>Приказ на бойца: продлить, обновить скорость и оружие в руках.</summary>
    public void Apply(EntityUid target, DHOrderType order, TimeSpan duration)
    {
        var comp = EnsureComp<DHOrderedComponent>(target);
        var until = _timing.CurTime + duration;
        switch (order)
        {
            case DHOrderType.Hold:
                comp.HoldUntil = until;
                break;
            case DHOrderType.Attack:
                comp.AttackUntil = until;
                break;
            case DHOrderType.Move:
                comp.MoveUntil = until;
                break;
        }

        Dirty(target, comp);
        Refresh(target);
    }

    private void Refresh(EntityUid target)
    {
        _movement.RefreshMovementSpeedModifiers(target);
        foreach (var held in _hands.EnumerateHeld(target))
        {
            if (HasComp<GunComponent>(held))
                _gun.RefreshModifiers(held);
        }
    }

    private void OnDamageModify(Entity<DHOrderedComponent> ent, ref DamageModifyEvent args)
    {
        if (Active(ent.Comp, DHOrderType.Hold))
            args.Damage *= ent.Comp.HoldDamage;
    }

    private void OnRefreshSpeed(Entity<DHOrderedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (Active(ent.Comp, DHOrderType.Move))
            args.ModifySpeed(ent.Comp.MoveSpeed, ent.Comp.MoveSpeed);
    }

    /// <summary>«В атаку»: у оружия в руках бойца меньше разброс.</summary>
    private void OnGunRefresh(Entity<GunComponent> ent, ref GunRefreshModifiersEvent args)
    {
        var holder = Transform(ent).ParentUid;

        // Повреждённый нерв руки — стреляет медленнее (план осады п. 18).
        if (TryComp<Content.Shared._DarkestHour.Medical.Trauma.DHTraumaComponent>(holder, out var trauma) &&
            trauma.FireRateModifier < 1f)
        {
            args.FireRate *= trauma.FireRateModifier;
        }

        // Кибер-рука «Тиски» держит оружие твёрже (план осады п. 29).
        if (trauma != null && trauma.GunSpreadModifier < 1f)
        {
            args.MinAngle *= trauma.GunSpreadModifier;
            args.MaxAngle *= trauma.GunSpreadModifier;
            args.AngleIncrease *= trauma.GunSpreadModifier;
        }

        if (!TryComp<DHOrderedComponent>(holder, out var ordered) || !Active(ordered, DHOrderType.Attack))
            return;

        args.MinAngle *= ordered.AttackSpread;
        args.MaxAngle *= ordered.AttackSpread;
        args.AngleIncrease *= ordered.AttackSpread;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_net.IsClient)
            return;

        // Истёк приказ — вернуть скорость и разброс; не осталось ни одного — снять компонент.
        var changed = new List<(EntityUid Uid, bool None)>();
        var query = EntityQueryEnumerator<DHOrderedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var active = (Active(comp, DHOrderType.Hold) ? 1 : 0) + (Active(comp, DHOrderType.Attack) ? 1 : 0) +
                         (Active(comp, DHOrderType.Move) ? 1 : 0);
            if (active == comp.LastActive)
                continue;

            comp.LastActive = active;
            changed.Add((uid, active == 0));
        }

        foreach (var (uid, none) in changed)
        {
            if (none)
                RemComp<DHOrderedComponent>(uid);
            Refresh(uid);
        }
    }
}
