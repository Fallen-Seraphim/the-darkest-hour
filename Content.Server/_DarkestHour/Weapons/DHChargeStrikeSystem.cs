using System.Numerics;
using Content.Shared._DarkestHour.Clothing.ModularArmor;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Weapons;

/// <summary>
/// Молот или кувалда: удар с разбега и шанс уронить (план осады п. 25, решение автора 2026-11-20). Сила зависит от
/// экзоскелета бойца.
/// </summary>
[RegisterComponent]
public sealed partial class DHChargeStrikeComponent : Component
{
    /// <summary>Сколько пробежать по прямой перед ударом, м.</summary>
    [DataField]
    public float RunUp = 3f;

    /// <summary>С какой скорости считается бег, м/с.</summary>
    [DataField]
    public float RunSpeed = 3f;

    /// <summary>Без экзоскелета, лёгкий, средний, тяжёлый: множитель урона удара с разбега.</summary>
    [DataField]
    public List<float> DamageMultipliers = new() { 1.2f, 1.35f, 1.5f, 1.75f };

    /// <summary>Шанс уронить при ударе с разбега (без разбега — вдвое меньше).</summary>
    [DataField]
    public List<float> KnockdownChances = new() { 0.1f, 0.25f, 0.4f, 0.6f };

    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(2);

    /// <summary>Отбросить цель (только с тяжёлым экзоскелетом и с разбега), клеток.</summary>
    [DataField]
    public float HeavyKnockback = 1.5f;

    // --- разбег (считается, пока молот в руках)
    [ViewVariables]
    public EntityUid? Holder;

    [ViewVariables]
    public Vector2 LastPosition;

    [ViewVariables]
    public Vector2 RunDirection;

    [ViewVariables]
    public float RunDistance;
}

public sealed class DHChargeStrikeSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHChargeStrikeComponent, MeleeHitEvent>(OnMeleeHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DHChargeStrikeComponent, TransformComponent>();
        while (query.MoveNext(out _, out var strike, out var xform))
        {
            // Молот в руках — родитель молота это боец (рука — контейнер на нём).
            var holder = xform.ParentUid;
            if (!HasComp<MobStateComponent>(holder))
            {
                strike.Holder = null;
                strike.RunDistance = 0f;
                continue;
            }

            var position = _transform.GetWorldPosition(holder);
            if (strike.Holder != holder)
            {
                strike.Holder = holder;
                strike.LastPosition = position;
                strike.RunDistance = 0f;
                continue;
            }

            var delta = position - strike.LastPosition;
            strike.LastPosition = position;

            var running = TryComp<PhysicsComponent>(holder, out var physics) &&
                          physics.LinearVelocity.Length() >= strike.RunSpeed;
            if (!running || delta.LengthSquared() < 0.0001f)
            {
                strike.RunDistance = 0f;
                continue;
            }

            var direction = Vector2.Normalize(delta);
            // По прямой: поворот больше ~30° сбрасывает разбег.
            if (strike.RunDistance > 0f && Vector2.Dot(direction, strike.RunDirection) < 0.86f)
                strike.RunDistance = 0f;

            strike.RunDirection = direction;
            strike.RunDistance += delta.Length();
        }
    }

    private void OnMeleeHit(Entity<DHChargeStrikeComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        var charged = ent.Comp.Holder == args.User && ent.Comp.RunDistance >= ent.Comp.RunUp;
        ent.Comp.RunDistance = 0f;

        var tier = ExoTier(args.User);
        if (charged)
        {
            args.BonusDamage += args.BaseDamage * (ent.Comp.DamageMultipliers[tier] - 1f);
            _popup.PopupEntity(Loc.GetString("dh-charge-strike"), args.User, args.User, PopupType.Medium);
        }

        var chance = ent.Comp.KnockdownChances[tier] * (charged ? 1f : 0.5f);
        var userPosition = _transform.GetWorldPosition(args.User);
        foreach (var target in args.HitEntities)
        {
            if (target == args.User || !HasComp<MobStateComponent>(target) || !_random.Prob(chance))
                continue;

            _stun.TryKnockdown(target, ent.Comp.KnockdownTime);

            // Тяжёлый экзоскелет с разбега ещё и отбрасывает.
            if (charged && tier == 3)
            {
                var away = _transform.GetWorldPosition(target) - userPosition;
                if (away.LengthSquared() > 0.0001f)
                {
                    var to = _transform.GetMapCoordinates(target).Offset(Vector2.Normalize(away) * ent.Comp.HeavyKnockback);
                    _throwing.TryThrow(target, _transform.ToCoordinates(to), 6f, args.User, recoil: false, doSpin: false);
                }
            }
        }
    }

    /// <summary>0 — без экзоскелета, 1 — лёгкий, 2 — средний, 3 — тяжёлый.</summary>
    private int ExoTier(EntityUid user)
    {
        if (!_inventory.TryGetSlotEntity(user, "outerClothing", out var outer) ||
            !TryComp<DHExoskeletonComponent>(outer, out var exo))
        {
            return 0;
        }

        return exo.Class switch
        {
            DHExoClass.Light => 1,
            DHExoClass.Medium => 2,
            DHExoClass.Heavy => 3,
            _ => 0,
        };
    }
}
