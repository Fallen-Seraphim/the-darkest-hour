using System.Linq;
using Content.Shared._DarkestHour.Medical.Disease;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Medical.Disease;

/// <summary>
/// Течение болезней: прогресс по стадиям, иммунный ответ, симптомы, заражение окружающих (воздух, контакт, кровь)
/// с учётом биозащиты одежды. Тикают только носители, которые чем-то болеют.
/// </summary>
public sealed partial class DHDiseaseSystem : EntitySystem
{
    [Dependency] private SharedEntityEffectsSystem _effects = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SpreadInterval = TimeSpan.FromSeconds(4);

    private const float AirborneRange = 1.6f;
    private const float ContactRange = 1.1f;
    private const float MaxProtection = 0.95f;

    /// <summary>Слоты, одежда в которых даёт биозащиту.</summary>
    private static readonly string[] ProtectionSlots = { "mask", "head", "outerClothing", "gloves", "jumpsuit" };

    private readonly HashSet<Entity<DHDiseaseCarrierComponent>> _nearby = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHDiseaseCarrierComponent, DHInfectAttemptEvent>(OnInfectAttempt);
        SubscribeLocalEvent<DHDiseaseCarrierComponent, DamageChangedEvent>(OnDamageChanged);
    }

    #region API

    /// <summary>Заразить болезнью. false — уже болеет, есть иммунитет или нет такой болезни.</summary>
    public bool Infect(EntityUid uid, ProtoId<DHDiseasePrototype> disease)
    {
        if (!TryComp<DHDiseaseCarrierComponent>(uid, out var carrier) ||
            !_proto.HasIndex(disease) ||
            carrier.Immune.Contains(disease) ||
            carrier.Diseases.ContainsKey(disease))
        {
            return false;
        }

        carrier.Diseases[disease] = new DHActiveDisease
        {
            NextSymptoms = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(2f, 6f)),
        };

        if (carrier.NextUpdate < _timing.CurTime)
            carrier.NextUpdate = _timing.CurTime + UpdateInterval;

        return true;
    }

    public void Cure(EntityUid uid, ProtoId<DHDiseasePrototype> disease, bool grantImmunity)
    {
        if (!TryComp<DHDiseaseCarrierComponent>(uid, out var carrier) || !carrier.Diseases.Remove(disease))
            return;

        if (grantImmunity)
            carrier.Immune.Add(disease);

        UpdateSymptomatic((uid, carrier));
    }

    public bool HasDisease(EntityUid uid, ProtoId<DHDiseasePrototype> disease) =>
        TryComp<DHDiseaseCarrierComponent>(uid, out var carrier) && carrier.Diseases.ContainsKey(disease);

    /// <summary>Биозащита надетой одежды по пути передачи (0…0.95).</summary>
    public float GetProtection(EntityUid uid, DHSpreadVector vector)
    {
        var passThrough = 1f;
        foreach (var slot in ProtectionSlots)
        {
            if (!_inventory.TryGetSlotEntity(uid, slot, out var item) ||
                !TryComp<DHBioProtectionComponent>(item, out var protection))
            {
                continue;
            }

            // Спущенная маска не защищает.
            if (TryComp<MaskComponent>(item, out var mask) && mask.IsToggled)
                continue;

            var value = vector == DHSpreadVector.Airborne ? protection.Airborne : protection.Contact;
            passThrough *= 1f - Math.Clamp(value, 0f, 1f);
        }

        return MathF.Min(MaxProtection, 1f - passThrough);
    }

    #endregion

    private void OnInfectAttempt(Entity<DHDiseaseCarrierComponent> ent, ref DHInfectAttemptEvent args)
    {
        Infect(ent, args.Disease);
    }

    /// <summary>Передача через кровь: рубящий или колющий удар заражённого в ближнем бою.</summary>
    private void OnDamageChanged(Entity<DHDiseaseCarrierComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased ||
            args.Origin is not { } attacker ||
            attacker == ent.Owner ||
            args.DamageDelta is not { } delta ||
            !TryComp<DHDiseaseCarrierComponent>(attacker, out var source) ||
            source.Diseases.Count == 0)
        {
            return;
        }

        var wound = 0f;
        foreach (var type in new[] { "Slash", "Piercing" })
        {
            if (delta.DamageDict.TryGetValue(type, out var value) && value > 0)
                wound += value.Float();
        }

        if (wound < 5f || !_transform.InRange(Transform(attacker).Coordinates, Transform(ent).Coordinates, ContactRange + 0.5f))
            return;

        foreach (var (id, disease) in source.Diseases.ToList())
        {
            if (!_proto.TryIndex(id, out var proto) || !proto.Spread.Contains(DHSpreadVector.Blood) || !IsContagious(proto, disease))
                continue;

            if (_random.Prob(proto.Infectivity * (1f - GetProtection(ent, DHSpreadVector.Contact))))
                Infect(ent, id);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHDiseaseCarrierComponent>();
        while (query.MoveNext(out var uid, out var carrier))
        {
            if (carrier.Diseases.Count == 0 || now < carrier.NextUpdate)
                continue;

            var dt = (float) (now - carrier.NextUpdate + UpdateInterval).TotalSeconds;
            carrier.NextUpdate = now + UpdateInterval;

            if (_mobState.IsDead(uid))
                continue;

            Tick((uid, carrier), MathF.Min(dt, 5f), now);
        }
    }

    private void Tick(Entity<DHDiseaseCarrierComponent> ent, float dt, TimeSpan now)
    {
        var spread = now >= ent.Comp.NextSpread;
        if (spread)
            ent.Comp.NextSpread = now + SpreadInterval;

        foreach (var (id, disease) in ent.Comp.Diseases.ToList())
        {
            if (!_proto.TryIndex(id, out var proto) || proto.Stages.Count == 0)
            {
                ent.Comp.Diseases.Remove(id);
                continue;
            }

            // Течение: болезнь растёт, иммунитет её сбивает.
            disease.Immunity = MathF.Min(1f, disease.Immunity + proto.ImmuneGain * dt);
            disease.Progress += (proto.ProgressRate - disease.Immunity * proto.ImmuneStrength) * dt;
            disease.Progress = MathF.Min(disease.Progress, proto.Stages.Count - 0.001f);

            if (disease.Progress <= 0f)
            {
                ent.Comp.Diseases.Remove(id);
                if (proto.GrantsImmunity)
                    ent.Comp.Immune.Add(id);
                continue;
            }

            var stage = proto.Stages[StageIndex(proto, disease)];

            if (now >= disease.NextSymptoms)
            {
                disease.NextSymptoms = now + stage.Interval;
                if (stage.Effects.Length > 0)
                    _effects.TryApplyEffects(ent, stage.Effects);
            }

            if (spread && stage.Contagious)
                SpreadFrom(ent, id, proto);
        }

        UpdateSymptomatic(ent);
    }

    private static int StageIndex(DHDiseasePrototype proto, DHActiveDisease disease)
    {
        return Math.Clamp((int) disease.Progress, 0, proto.Stages.Count - 1);
    }

    private static bool IsContagious(DHDiseasePrototype proto, DHActiveDisease disease)
    {
        return proto.Stages[StageIndex(proto, disease)].Contagious;
    }

    private void SpreadFrom(Entity<DHDiseaseCarrierComponent> source, ProtoId<DHDiseasePrototype> id, DHDiseasePrototype proto)
    {
        var airborne = proto.Spread.Contains(DHSpreadVector.Airborne);
        var contact = proto.Spread.Contains(DHSpreadVector.Contact);
        if (!airborne && !contact)
            return;

        var coords = Transform(source).Coordinates;
        _nearby.Clear();
        _lookup.GetEntitiesInRange(coords, airborne ? AirborneRange : ContactRange, _nearby);

        // Маска на больном тоже сдерживает выдох.
        var sourceMask = airborne ? GetProtection(source, DHSpreadVector.Airborne) * 0.5f : 0f;

        foreach (var target in _nearby)
        {
            if (target.Owner == source.Owner || target.Comp.Diseases.ContainsKey(id) || target.Comp.Immune.Contains(id))
                continue;

            var chance = 0f;
            if (airborne)
                chance = proto.Infectivity * (1f - sourceMask) * (1f - GetProtection(target, DHSpreadVector.Airborne));

            if (contact && _transform.InRange(coords, Transform(target).Coordinates, ContactRange))
            {
                var contactChance = proto.Infectivity * (1f - GetProtection(target, DHSpreadVector.Contact));
                chance = 1f - (1f - chance) * (1f - contactChance);
            }

            if (chance > 0f && _random.Prob(Math.Clamp(chance, 0f, 1f)))
                Infect(target, id);
        }
    }

    private void UpdateSymptomatic(Entity<DHDiseaseCarrierComponent> ent)
    {
        var symptomatic = false;
        foreach (var (id, disease) in ent.Comp.Diseases)
        {
            if (_proto.TryIndex(id, out var proto) && proto.Stages[StageIndex(proto, disease)].Symptomatic)
            {
                symptomatic = true;
                break;
            }
        }

        if (symptomatic == ent.Comp.Symptomatic)
            return;

        ent.Comp.Symptomatic = symptomatic;
        Dirty(ent);
    }
}
