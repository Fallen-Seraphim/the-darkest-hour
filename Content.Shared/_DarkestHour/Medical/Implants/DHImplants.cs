using Content.Shared.Body;
using Content.Shared.EntityEffects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._DarkestHour.Medical.Implants;

// Импланты и отторжение (план осады п. 29, решения автора 2026-11-18 и 2026-11-20). Имплант ставится хирургически в
// голову или торс. Пока он в теле, медленно растёт отторжение; работающий имплант (сработал, держит, глушит) — быстрее.
// Три имплантa в активной работе доводят отторжение до предела ~за 20 минут, один без дела почти не опасен. Отторжение
// не лечится; Цитрасин и диализ (жизнеобеспечение) замедляют вдвое. На пределе — минута агонии и смерть.

[Serializable, NetSerializable]
public enum DHImplantKind : byte
{
    /// <summary>Служебный базовый имплант: основа для других и для летальной инъекции (п. 28).</summary>
    Base,
    /// <summary>«Пульсар» — сердечный стимулятор: один раз запускает остановившееся сердце.</summary>
    Pulsar,
    /// <summary>«Эхо» — слуховой: защита от контузии, дальние звуки громче.</summary>
    Echo,
    /// <summary>«Чешуя» — подкожная броня: режущий и колющий урон, осколки.</summary>
    Scale,
    /// <summary>«Тишина» — подавитель боли.</summary>
    Silence,
    /// <summary>«Фильтр» — лёгочный: яды, газы, облучение.</summary>
    Filter,
    /// <summary>«Гемостат» — свёртывающий: кровотечение.</summary>
    Hemostat,
    /// <summary>«Сокол» — глазной: приближение и ночное зрение.</summary>
    Falcon,
    /// <summary>«Тиски» — кибернетическая рука.</summary>
    Vise,
    /// <summary>«Опора» — кибернетическая нога.</summary>
    Support,
    /// <summary>«Тактик» — нейролинк.</summary>
    Tactic,
}

/// <summary>Имплант-предмет: ставится операцией «Установка импланта» в свою часть тела.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHImplantComponent : Component
{
    [DataField(required: true)]
    public DHImplantKind Kind;

    /// <summary>Куда ставится: голова или торс.</summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype> Part = "Torso";

    /// <summary>Отторжение в секунду, пока имплант просто в теле (из 100).</summary>
    [DataField]
    public float PassiveRate = 0.004f;

    /// <summary>Отторжение в секунду, пока имплант работает.</summary>
    [DataField]
    public float ActiveRate = 0.028f;

    /// <summary>Разовая прибавка за срабатывание («Пульсар» запустил сердце).</summary>
    [DataField]
    public float TriggerCost = 15f;

    /// <summary>Перезарядка срабатывания.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    [DataField]
    public TimeSpan NextTrigger;

    /// <summary>Работал в последнюю секунду (растёт ActiveRate).</summary>
    [ViewVariables]
    public bool Active;
}

/// <summary>Тело с имплантами: отторжение, агония.</summary>
[RegisterComponent]
public sealed partial class DHImplantedComponent : Component
{
    public const string ContainerId = "dh_implants";

    /// <summary>Отторжение, 0…100.</summary>
    [DataField]
    public float Rejection;

    /// <summary>Отторжение замедлено (Цитрасин) до этого времени.</summary>
    [DataField]
    public TimeSpan SlowedUntil;

    /// <summary>Началась агония (отторжение 100%); через <see cref="AgonyTime"/> — смерть.</summary>
    [DataField]
    public TimeSpan? AgonySince;

    [DataField]
    public TimeSpan AgonyTime = TimeSpan.FromSeconds(60);

    /// <summary>«Пульсар» запустит сердце в это время (после остановки).</summary>
    [DataField]
    public TimeSpan? PulsarAt;

    [DataField]
    public TimeSpan NextUpdate;
}

/// <summary>Цитрасин: замедляет отторжение имплантов, пока препарат в крови.</summary>
/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
public sealed partial class DHSlowRejectionEntityEffectSystem : EntityEffectSystem<DHImplantedComponent, DHSlowRejection>
{
    [Dependency] private IGameTiming _timing = default!;

    protected override void Effect(Entity<DHImplantedComponent> entity, ref EntityEffectEvent<DHSlowRejection> args)
    {
        entity.Comp.SlowedUntil = _timing.CurTime + args.Effect.Duration;
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class DHSlowRejection : EntityEffectBase<DHSlowRejection>
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(4);

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("dh-entity-effect-guidebook-slow-rejection", ("chance", Probability));
}
