using System.Linq;
using Content.Server._DarkestHour.Medical.LifeSupport;
using Content.Shared._DarkestHour.Medical.Implants;
using Content.Shared._DarkestHour.Medical.LifeSupport;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Stunnable;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Medical.Implants;

/// <summary>
/// Импланты (план осады п. 29, решения автора 2026-11-18 и 2026-11-20): установка и извлечение операцией, отторжение,
/// агония и эффекты простых имплантов:
/// <list type="bullet">
/// <item>«Пульсар» — через 4 с после остановки сердца запускает его (как СЛР, но наверняка), перезарядка 10 минут;</item>
/// <item>«Чешуя» — режущий и колющий урон ×0,85;</item>
/// <item>«Фильтр» — яды ×0,6, облучение ×0,5;</item>
/// <item>«Тишина» — боль снижена на 60 (травмы спрашивают <see cref="PainSuppression"/>);</item>
/// <item>«Гемостат» — внешнее кровотечение останавливается, внутреннее ×0,4 (<see cref="BleedFactor"/>);</item>
/// <item>«Эхо» — контузия от взрывов слабее (<see cref="HearingFactor"/>), дальние звуки громче.</item>
/// </list>
/// </summary>
public sealed class DHImplantSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private DHLifeSupportSystem _lifeSupport = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PulsarDelay = TimeSpan.FromSeconds(4);

    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";
    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";
    private static readonly ProtoId<DamageTypePrototype> Cellular = "Cellular";

    private static readonly DamageModifierSet ScaleModifiers = new()
    {
        Coefficients = new() { ["Slash"] = 0.85f, ["Piercing"] = 0.85f },
    };

    private static readonly DamageModifierSet FilterModifiers = new()
    {
        Coefficients = new() { ["Poison"] = 0.6f, ["Radiation"] = 0.5f },
    };

    public const float SilencePain = 60f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<DHImplantedComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<DHImplantedComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnRejuvenate(Entity<DHImplantedComponent> ent, ref RejuvenateEvent args)
    {
        ent.Comp.Rejection = 0f;
        ent.Comp.AgonySince = null;
        ent.Comp.PulsarAt = null;
    }

    #region Установка и извлечение

    private Container GetContainer(EntityUid patient)
    {
        EnsureComp<DHImplantedComponent>(patient);
        return _container.EnsureContainer<Container>(patient, DHImplantedComponent.ContainerId);
    }

    public IEnumerable<Entity<DHImplantComponent>> Implants(EntityUid patient)
    {
        if (!_container.TryGetContainer(patient, DHImplantedComponent.ContainerId, out var container))
            yield break;

        foreach (var uid in container.ContainedEntities)
        {
            if (TryComp<DHImplantComponent>(uid, out var implant))
                yield return (uid, implant);
        }
    }

    public bool TryGetImplant(EntityUid patient, DHImplantKind kind, out Entity<DHImplantComponent> implant)
    {
        foreach (var found in Implants(patient))
        {
            if (found.Comp.Kind != kind)
                continue;

            implant = found;
            return true;
        }

        implant = default;
        return false;
    }

    public bool HasImplant(EntityUid patient, DHImplantKind kind) => TryGetImplant(patient, kind, out _);

    /// <summary>Можно ли поставить этот имплант в эту часть: он для неё, и такого ещё нет.</summary>
    public bool CanInstall(EntityUid patient, EntityUid item, string category, out string? reason)
    {
        reason = null;
        if (!TryComp<DHImplantComponent>(item, out var implant))
            return false;

        if (implant.Part.Id != category)
        {
            reason = "dh-implant-wrong-part";
            return false;
        }

        if (HasImplant(patient, implant.Kind))
        {
            reason = "dh-implant-already";
            return false;
        }

        return true;
    }

    public bool Install(EntityUid patient, EntityUid item)
    {
        if (!HasComp<DHImplantComponent>(item))
            return false;

        return _container.Insert(item, GetContainer(patient), force: true);
    }

    public bool HasImplantsIn(EntityUid patient, string category) => Implants(patient).Any(i => i.Comp.Part.Id == category);

    /// <summary>Извлечь первый имплант из части тела хирургу в руку. Отторжение остаётся.</summary>
    public bool Remove(EntityUid patient, string category, EntityUid surgeon)
    {
        foreach (var implant in Implants(patient).ToList())
        {
            if (implant.Comp.Part.Id != category)
                continue;

            implant.Comp.Active = false;
            _container.Remove(implant.Owner, _container.GetContainer(patient, DHImplantedComponent.ContainerId));
            _hands.PickupOrDrop(surgeon, implant);
            return true;
        }

        return false;
    }

    #endregion

    #region Эффекты для других систем

    /// <summary>Насколько «Тишина» снижает боль; работает — растёт отторжение.</summary>
    public float PainSuppression(EntityUid patient, float pain)
    {
        if (!TryGetImplant(patient, DHImplantKind.Silence, out var silence))
            return 0f;

        if (pain > 0f)
            silence.Comp.Active = true;

        return SilencePain;
    }

    /// <summary>Множитель внутреннего кровотечения («Гемостат»).</summary>
    public float BleedFactor(EntityUid patient, bool bleeding)
    {
        if (!TryGetImplant(patient, DHImplantKind.Hemostat, out var hemostat))
            return 1f;

        if (bleeding)
            hemostat.Comp.Active = true;

        return 0.4f;
    }

    /// <summary>Множитель контузии слуха («Эхо»).</summary>
    public float HearingFactor(EntityUid patient)
    {
        if (!TryGetImplant(patient, DHImplantKind.Echo, out var echo))
            return 1f;

        echo.Comp.Active = true;
        return 0.2f;
    }

    private void OnDamageModify(Entity<DHImplantedComponent> ent, ref DamageModifyEvent args)
    {
        if (TryGetImplant(ent, DHImplantKind.Scale, out var scale) &&
            (args.Damage.DamageDict.ContainsKey("Slash") || args.Damage.DamageDict.ContainsKey("Piercing")))
        {
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, ScaleModifiers);
            scale.Comp.Active = true;
        }

        if (TryGetImplant(ent, DHImplantKind.Filter, out var filter) &&
            (args.Damage.DamageDict.ContainsKey("Poison") || args.Damage.DamageDict.ContainsKey("Radiation")))
        {
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, FilterModifiers);
            filter.Comp.Active = true;
        }
    }

    #endregion

    #region «Пульсар»

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || !TryComp<DHImplantedComponent>(args.Target, out var implanted) ||
            !TryGetImplant(args.Target, DHImplantKind.Pulsar, out var pulsar) || _timing.CurTime < pulsar.Comp.NextTrigger)
        {
            return;
        }

        implanted.PulsarAt = _timing.CurTime + PulsarDelay;
    }

    private void TickPulsar(Entity<DHImplantedComponent> ent, TimeSpan now)
    {
        if (ent.Comp.PulsarAt is not { } at || now < at)
            return;

        ent.Comp.PulsarAt = null;
        if (!_mobState.IsDead(ent) || !TryGetImplant(ent, DHImplantKind.Pulsar, out var pulsar))
            return;

        // Разряд: дыхание и кровообращение — как после удачного цикла СЛР, потом запуск сердца.
        _damageable.TryChangeDamage(ent.Owner, Damage(Asphyxiation, -40f), true, false);
        _popup.PopupEntity(Loc.GetString("dh-implant-pulsar-zap", ("target", ent.Owner)), ent, PopupType.Medium);
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(2), true);

        pulsar.Comp.NextTrigger = now + pulsar.Comp.Cooldown;
        if (_lifeSupport.TryRestartHeart(ent, ent))
            ent.Comp.Rejection = MathF.Min(100f, ent.Comp.Rejection + pulsar.Comp.TriggerCost);
    }

    #endregion

    #region Отторжение

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHImplantedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + Interval;
            TickPulsar((uid, comp), now);

            if (_mobState.IsDead(uid))
                continue;

            TickHemostat(uid);
            TickRejection((uid, comp), now);
        }
    }

    /// <summary>«Гемостат» сам останавливает внешнее кровотечение.</summary>
    private void TickHemostat(EntityUid uid)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var blood) || blood.BleedAmount <= 0f ||
            !TryGetImplant(uid, DHImplantKind.Hemostat, out var hemostat))
        {
            return;
        }

        _bloodstream.TryModifyBleedAmount((uid, blood), -0.3f);
        hemostat.Comp.Active = true;
    }

    private void TickRejection(Entity<DHImplantedComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        if (comp.AgonySince is { } since)
        {
            TickAgony(ent, now, since);
            return;
        }

        var rate = 0f;
        foreach (var implant in Implants(ent))
        {
            rate += implant.Comp.PassiveRate + (implant.Comp.Active ? implant.Comp.ActiveRate : 0f);
            implant.Comp.Active = false;
        }

        if (now < comp.SlowedUntil)
            rate *= 0.5f;

        // Диализ: жизнеобеспечение чистит кровь.
        if (TryComp<DHMachinePatientComponent>(ent, out var patient) && patient.LifeSupport != null)
            rate *= 0.5f;

        comp.Rejection = MathF.Min(100f, comp.Rejection + rate * (float) Interval.TotalSeconds);
        Symptoms(ent);

        if (comp.Rejection >= 100f)
        {
            comp.AgonySince = now;
            _popup.PopupEntity(Loc.GetString("dh-implant-agony-start"), ent, ent, PopupType.LargeCaution);
        }
    }

    /// <summary>Тошнота с 40%, слабость и яд с 70%.</summary>
    private void Symptoms(Entity<DHImplantedComponent> ent)
    {
        var rejection = ent.Comp.Rejection;
        if (rejection >= 40f && _random.Prob(0.03f))
            _popup.PopupEntity(Loc.GetString("dh-implant-nausea"), ent, ent, PopupType.SmallCaution);

        if (rejection < 70f)
            return;

        _damageable.TryChangeDamage(ent.Owner, Damage(Poison, 0.1f), true, false);
        if (_random.Prob(0.04f))
        {
            _popup.PopupEntity(Loc.GetString("dh-implant-weakness"), ent, ent, PopupType.MediumCaution);
            _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(2));
        }
    }

    /// <summary>Агония: судороги и кровь горлом минуту, потом смерть без возврата.</summary>
    private void TickAgony(Entity<DHImplantedComponent> ent, TimeSpan now, TimeSpan since)
    {
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(2), true, 20f, 8f);
        if (_random.Prob(0.3f))
        {
            _popup.PopupEntity(Loc.GetString("dh-implant-agony", ("target", ent.Owner)), ent, PopupType.MediumCaution);
            _bloodstream.TryModifyBloodLevel(ent.Owner, FixedPoint2.New(-3));
        }

        if (_random.Prob(0.15f))
            _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(3));

        if (now < since + ent.Comp.AgonyTime)
            return;

        var unrevivable = EnsureComp<UnrevivableComponent>(ent);
        unrevivable.ReasonMessage = "dh-implant-rejection-death";
        Dirty(ent, unrevivable);
        _damageable.TryChangeDamage(ent.Owner, Damage(Cellular, 400f), true, false);
    }

    /// <summary>Строки анализатора: импланты и отторжение.</summary>
    public void AddSummary(EntityUid uid, List<string> lines)
    {
        if (!TryComp<DHImplantedComponent>(uid, out var comp))
            return;

        var names = Implants(uid).Select(i => Loc.GetString($"dh-implant-kind-{i.Comp.Kind}")).ToList();
        if (names.Count > 0)
            lines.Add(Loc.GetString("dh-implant-analyzer-list", ("implants", string.Join(", ", names))));

        if (comp.AgonySince != null)
            lines.Add(Loc.GetString("dh-implant-analyzer-terminal"));
        else if (comp.Rejection >= 1f)
            lines.Add(Loc.GetString("dh-implant-analyzer-rejection", ("percent", (int) comp.Rejection)));
    }

    #endregion

    private static DamageSpecifier Damage(ProtoId<DamageTypePrototype> type, float amount)
    {
        var spec = new DamageSpecifier();
        spec.DamageDict[type] = FixedPoint2.New(amount);
        return spec;
    }
}
