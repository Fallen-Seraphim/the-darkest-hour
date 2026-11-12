using System.Numerics;
using Content.Shared._DarkestHour.Hearing;
using Content.Shared.Armor;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Hearing;

/// <summary>Взрыв состоялся (ExplosionSystem.SpawnExplosion): центр, полная мощность и радиус в тайлах.</summary>
[ByRefEvent]
public readonly record struct DHExplosionSpawnedEvent(MapCoordinates Epicenter, float TotalIntensity, int Radius);

/// <summary>
/// Близкий разрыв (решение автора 2026-11-12): звон в ушах и ненадолго вспышка и муть в глазах. Сила — от мощности
/// взрыва и расстояния до него (полная в центре, к краю зоны сходит на нет). Гарнитура и шлем ослабляют, но не гасят:
/// гарнитура больше бережёт слух, шлем — глаза.
/// </summary>
public sealed partial class DHConcussionSystem : EntitySystem
{
    [Dependency] private DHHearingSystem _hearing = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>Ниже этой силы эффекта нет.</summary>
    private const float MinEffect = 0.08f;

    // Защита (множители силы): слух / глаза.
    private const float HeadsetHearing = 0.6f;
    private const float HeadsetVision = 0.85f;
    private const float HelmetHearing = 0.75f;
    private const float HelmetVision = 0.65f;

    /// <summary>Самые плотные наушники глушат не сильнее этого (защита не гасит эффект целиком).</summary>
    private const float MaxEarProtection = 0.45f;

    private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextCleanup;
    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private readonly List<EntityUid> _done = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHExplosionSpawnedEvent>(OnExplosion);
    }

    private void OnExplosion(ref DHExplosionSpawnedEvent args)
    {
        if (args.Epicenter.MapId == MapId.Nullspace || args.TotalIntensity <= 0f)
            return;

        // Зона контузии шире зоны поражения: граната (~5 тайлов поражения) глушит метров на 12–14.
        var range = Math.Clamp(args.Radius * 2.5f + 2f, 3f, 18f);
        // Мощность: граната ~0,6–0,8, снаряд ~1, большая бомба — до 1,5.
        var power = Math.Clamp(MathF.Sqrt(args.TotalIntensity) / 10f, 0.25f, 1.5f);

        _mobs.Clear();
        _lookup.GetEntitiesInRange(args.Epicenter, range, _mobs);
        foreach (var mob in _mobs)
        {
            if (_mobState.IsDead(mob, mob.Comp))
                continue;

            var distance = Vector2.Distance(_transform.GetWorldPosition(mob), args.Epicenter.Position);
            var k = 1f - distance / range;
            if (k <= 0f)
                continue;

            var strength = power * MathF.Pow(k, 1.5f);
            var (hearing, vision) = Protection(mob);
            Deafen(mob, strength * hearing);
            Blind(mob, strength * vision);
        }

        _mobs.Clear();
    }

    /// <summary>Звон в ушах: 2–20 с.</summary>
    private void Deafen(EntityUid uid, float strength)
    {
        if (strength < MinEffect)
            return;

        _hearing.Ring(uid, TimeSpan.FromSeconds(Math.Clamp(2f + strength * 14f, 2f, 20f)));
    }

    /// <summary>Вспышка и муть в глазах: 1–8 с.</summary>
    private void Blind(EntityUid uid, float strength)
    {
        if (strength < MinEffect)
            return;

        strength = MathF.Min(strength, 1f);
        var now = _timing.CurTime;
        var duration = TimeSpan.FromSeconds(Math.Clamp(1f + strength * 7f, 1f, 8f));
        var concussion = EnsureComp<DHConcussionComponent>(uid);

        // Уже мутит — новая вспышка с силой не слабее того, что осталось от прежней.
        var left = concussion.EndTime > now
            ? concussion.Strength * (float) ((concussion.EndTime - now) / (concussion.EndTime - concussion.StartTime))
            : 0f;

        concussion.StartTime = now;
        concussion.EndTime = now + duration;
        concussion.Strength = MathF.Max(strength, left);
        Dirty(uid, concussion);
    }

    /// <summary>Множители силы для слуха и глаз от гарнитуры (ухо) и шлема (голова с бронёй).</summary>
    private (float Hearing, float Vision) Protection(EntityUid uid)
    {
        var hearing = 1f;
        var vision = 1f;

        if (_inventory.TryGetSlotEntity(uid, "ears", out var ears))
        {
            var ear = HeadsetHearing;
            if (TryComp<DHHearingProtectionComponent>(ears, out var protection))
                ear = MathF.Min(ear, MathF.Max(protection.Factor, MaxEarProtection));

            hearing *= ear;
            vision *= HeadsetVision;
        }

        if (_inventory.TryGetSlotEntity(uid, "head", out var head) && HasComp<ArmorComponent>(head))
        {
            hearing *= HelmetHearing;
            vision *= HelmetVision;
        }

        return (hearing, vision);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextCleanup)
            return;

        _nextCleanup = now + CleanupInterval;
        _done.Clear();
        var query = EntityQueryEnumerator<DHConcussionComponent>();
        while (query.MoveNext(out var uid, out var concussion))
        {
            if (now >= concussion.EndTime)
                _done.Add(uid);
        }

        foreach (var uid in _done)
        {
            RemComp<DHConcussionComponent>(uid);
        }
    }
}
