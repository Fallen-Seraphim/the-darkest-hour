using Content.Shared.Body;
using Content.Shared.EntityEffects;
using Content.Shared.Movement.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._DarkestHour.Medical.Trauma;

/// <summary>
/// Общая часть травм: выбор зоны попадания (запрос клиента), замедление от травм (предсказуемое).
/// </summary>
public sealed partial class DHTraumaSharedSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<DHSetTargetRequestEvent>(OnSetTargetRequest);
        SubscribeLocalEvent<DHTraumaComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnSetTargetRequest(DHSetTargetRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid)
            return;

        SetTarget(uid, ev.Category);
    }

    public void SetTarget(EntityUid uid, string category)
    {
        if (!TryComp<DHTargetingComponent>(uid, out var targeting) ||
            !_proto.HasIndex<OrganCategoryPrototype>(category) ||
            targeting.Target.Id == category)
        {
            return;
        }

        targeting.Target = category;
        Dirty(uid, targeting);
    }

    private void OnRefreshSpeed(Entity<DHTraumaComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.SpeedModifier < 1f)
            args.ModifySpeed(ent.Comp.SpeedModifier, ent.Comp.SpeedModifier);
    }
}

/// <summary>
/// Обезболивание: подавляет боль на время действия препарата.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
public sealed partial class DHSuppressPainEntityEffectSystem : EntityEffectSystem<DHTraumaComponent, DHSuppressPain>
{
    [Dependency] private IGameTiming _timing = default!;

    protected override void Effect(Entity<DHTraumaComponent> entity, ref EntityEffectEvent<DHSuppressPain> args)
    {
        var amount = args.Effect.Amount * args.Scale;
        var now = _timing.CurTime;
        if (now > entity.Comp.PainSuppressionUntil || amount > entity.Comp.PainSuppression)
            entity.Comp.PainSuppression = amount;

        entity.Comp.PainSuppressionUntil = now + args.Effect.Duration;
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class DHSuppressPain : EntityEffectBase<DHSuppressPain>
{
    /// <summary>На сколько единиц снижается боль.</summary>
    [DataField]
    public float Amount = 40f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(4);

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("dh-entity-effect-guidebook-suppress-pain", ("chance", Probability), ("amount", Amount));
}

/// <summary>
/// Замедляет внутреннее кровотечение (транексамовая кислота), пока препарат в крови.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
public sealed partial class DHSlowInternalBleedingEntityEffectSystem : EntityEffectSystem<DHTraumaComponent, DHSlowInternalBleeding>
{
    [Dependency] private IGameTiming _timing = default!;

    protected override void Effect(Entity<DHTraumaComponent> entity, ref EntityEffectEvent<DHSlowInternalBleeding> args)
    {
        entity.Comp.BleedingSlowedUntil = _timing.CurTime + args.Effect.Duration;
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class DHSlowInternalBleeding : EntityEffectBase<DHSlowInternalBleeding>
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(4);

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("dh-entity-effect-guidebook-slow-internal-bleeding", ("chance", Probability));
}
