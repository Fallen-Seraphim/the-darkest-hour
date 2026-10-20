using Content.Server.StationEvents.Events;
using Content.Shared._DarkestHour.Medical.Disease;
using Content.Shared._DarkestHour.Medical.Virology;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Station.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Medical.Disease;

/// <summary>
/// Вспышка болезни: случайный «нулевой пациент» (1–2 человека экипажа) заражается болезнью из списка. Без объявления —
/// медики узнают по симптомам, вирусолог определяет болезнь.
/// </summary>
[RegisterComponent, Access(typeof(DHDiseaseOutbreakRule))]
public sealed partial class DHDiseaseOutbreakRuleComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<DHDiseasePrototype>> Diseases = new();

    [DataField]
    public int MinPatients = 1;

    [DataField]
    public int MaxPatients = 2;
}

public sealed partial class DHDiseaseOutbreakRule : StationEventSystem<DHDiseaseOutbreakRuleComponent>
{
    [Dependency] private DHDiseaseSystem _disease = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    protected override void Started(Entity<DHDiseaseOutbreakRuleComponent, GameRuleComponent> ent, ref GameRuleStartedEvent args)
    {
        base.Started(ent, ref args);

        if (ent.Comp1.Diseases.Count == 0 ||
            !Station.TryGetRandomStation<StationEventEligibleComponent>(out var station))
        {
            return;
        }

        var candidates = new List<EntityUid>();
        var query = EntityQueryEnumerator<DHDiseaseCarrierComponent, MindContainerComponent>();
        while (query.MoveNext(out var uid, out var carrier, out var mind))
        {
            if (!mind.HasMind || !_mobState.IsAlive(uid) || carrier.Diseases.Count > 0)
                continue;

            if (Station.GetOwningStation(uid) != station.Value.Owner)
                continue;

            candidates.Add(uid);
        }

        if (candidates.Count == 0)
            return;

        var disease = ent.Comp1.Diseases[RobustRandom.Next(ent.Comp1.Diseases.Count)];
        var count = Math.Min(candidates.Count, RobustRandom.Next(ent.Comp1.MinPatients, ent.Comp1.MaxPatients + 1));
        for (var i = 0; i < count; i++)
        {
            var index = RobustRandom.Next(candidates.Count);
            var patient = candidates[index];
            candidates.RemoveAt(index);
            _disease.Infect(patient, disease);
            Sawmill.Info($"Disease outbreak: {ToPrettyString(patient)} infected with {disease}.");
        }
    }
}

/// <summary>
/// Утечка из лаборатории: одна из банок с культурой на станции трескается и заражает всех рядом.
/// Нет банок — ничего не происходит.
/// </summary>
[RegisterComponent, Access(typeof(DHLabLeakRule))]
public sealed partial class DHLabLeakRuleComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = { ["Blunt"] = 50 },
    };
}

public sealed partial class DHLabLeakRule : StationEventSystem<DHLabLeakRuleComponent>
{
    [Dependency] private DamageableSystem _damageable = default!;

    protected override void Started(Entity<DHLabLeakRuleComponent, GameRuleComponent> ent, ref GameRuleStartedEvent args)
    {
        base.Started(ent, ref args);

        if (!Station.TryGetRandomStation<StationEventEligibleComponent>(out var station))
            return;

        var jars = new List<EntityUid>();
        var query = EntityQueryEnumerator<DHCultureJarComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (Station.GetOwningStation(uid) == station.Value.Owner)
                jars.Add(uid);
        }

        if (jars.Count == 0)
            return;

        var jar = jars[RobustRandom.Next(jars.Count)];
        _damageable.TryChangeDamage(jar, ent.Comp1.Damage, ignoreResistances: true);
        Sawmill.Info($"Lab leak: {ToPrettyString(jar)} broke.");
    }
}
