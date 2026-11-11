using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._DarkestHour.Medical.Omnireazin;

/// <summary>
/// Омниреазин — универсальное средство Ассоциации, стандарт с МБ-2М (автоаптечка модульной брони).
/// Сращивает лёгкие и средние повреждения, но не лечит тяжёлые раны и переломы; притупляет боль, и она
/// возвращается, когда действие кончается. Ветераны, много раз воевавшие «на омниреазине» с тяжёлыми ранами,
/// получают неснимаемые фантомные боли, а потом — ощущение, что тело чужое.
/// </summary>
[RegisterComponent]
public sealed partial class DHOmnireazinComponent : Component
{
    /// <summary>Препарат в крови: эффект срабатывал недавно.</summary>
    [DataField]
    public bool Active;

    /// <summary>До какого времени действие считается продолжающимся (после — откат боли).</summary>
    [DataField]
    public TimeSpan ActiveUntil;

    /// <summary>Накопленная боль, которая вернётся после окончания действия.</summary>
    [DataField]
    public float Rebound;

    /// <summary>За текущий курс препарат «держал в бою» с тяжёлым ранением.</summary>
    [DataField]
    public bool HeavyThisCourse;

    /// <summary>Сколько раз препарат принимали с тяжёлыми ранениями.</summary>
    [DataField]
    public int HeavyCourses;

    [DataField]
    public TimeSpan NextPhantom;

    [DataField]
    public TimeSpan NextDepersonalization;
}

/// <inheritdoc cref="EntityEffectSystem{T, TEffect}"/>
public sealed partial class DHOmnireazinEntityEffectSystem : EntityEffectSystem<DHTraumaComponent, DHOmnireazin>
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<DamageGroupPrototype> Brute = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> Burn = "Burn";

    protected override void Effect(Entity<DHTraumaComponent> entity, ref EntityEffectEvent<DHOmnireazin> args)
    {
        var effect = args.Effect;
        var scale = args.Scale;
        var omni = EnsureComp<DHOmnireazinComponent>(entity);
        omni.Active = true;
        omni.ActiveUntil = _timing.CurTime + effect.Linger;
        omni.Rebound = MathF.Min(effect.ReboundMax, omni.Rebound + effect.Rebound * scale);

        // Тяжёлые раны (часть тела повреждена на HeavyFraction прочности или сломана) препарат не трогает.
        var lightBrute = 0f;
        var lightBurn = 0f;
        foreach (var (category, part) in entity.Comp.Parts)
        {
            if (IsHeavy(entity.Comp, category, part, effect.HeavyFraction))
            {
                omni.HeavyThisCourse = true;
                continue;
            }

            lightBrute += part.Brute;
            lightBurn += part.Burn;
        }

        var healBrute = MathF.Min(effect.Heal * scale, lightBrute);
        var healBurn = MathF.Min(effect.Heal * effect.BurnFactor * scale, lightBurn);
        if (healBrute <= 0.01f && healBurn <= 0.01f)
            return;

        // Сначала снимаем урон с лёгких частей, затем — с общего урона: синхронизация травм тогда ничего
        // не перераспределит на тяжёлые части.
        foreach (var (category, part) in entity.Comp.Parts)
        {
            if (IsHeavy(entity.Comp, category, part, effect.HeavyFraction))
                continue;

            if (lightBrute > 0f)
                part.Brute = MathF.Max(0f, part.Brute - part.Brute / lightBrute * healBrute);
            if (lightBurn > 0f)
                part.Burn = MathF.Max(0f, part.Burn - part.Burn / lightBurn * healBurn);
        }

        if (healBrute > 0.01f)
            _damageable.HealDistributed(entity.Owner, -healBrute, Brute);
        if (healBurn > 0.01f)
            _damageable.HealDistributed(entity.Owner, -healBurn, Burn);
    }

    private static bool IsHeavy(DHTraumaComponent comp, string category, DHPartTrauma part, float fraction)
    {
        var max = comp.PartMax.TryGetValue(category, out var m) ? m : 60f;
        return part.Fracture || part.Brute + part.Burn >= max * fraction;
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class DHOmnireazin : EntityEffectBase<DHOmnireazin>
{
    /// <summary>Сколько механического урона сращивается за срабатывание (только на лёгких/средних частях).</summary>
    [DataField]
    public float Heal = 2f;

    /// <summary>Ожоги заживают медленнее: доля от <see cref="Heal"/>.</summary>
    [DataField]
    public float BurnFactor = 0.5f;

    /// <summary>Часть тела с уроном не меньше этой доли прочности — тяжёлая рана, её препарат не лечит.</summary>
    [DataField]
    public float HeavyFraction = 0.6f;

    /// <summary>Сколько боли «копится» за срабатывание и вернётся после окончания действия.</summary>
    [DataField]
    public float Rebound = 1.5f;

    [DataField]
    public float ReboundMax = 40f;

    /// <summary>Сколько действие держится после последнего срабатывания.</summary>
    [DataField]
    public TimeSpan Linger = TimeSpan.FromSeconds(3);

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("dh-entity-effect-guidebook-omnireazin", ("chance", Probability), ("amount", Heal));
}
