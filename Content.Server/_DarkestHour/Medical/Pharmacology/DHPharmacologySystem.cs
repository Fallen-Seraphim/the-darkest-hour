using System.Linq;
using Content.Shared._DarkestHour.Medical.Pharmacology;
using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Medical.Pharmacology;

/// <summary>
/// Каркас фармакологии (этап 2 переработки медицины): раз в секунду — спад нагрузки классов препаратов,
/// привыкание, зависимость и ломка, угнетение дыхания при сочетании опиоидов, седативных и алкоголя.
/// Передозировка отдельного препарата — в его прототипе (ReagentCondition по количеству в крови).
/// Путь введения задаёт сам метаболизм: таблетка идёт через желудок (медленно, долго), укол — сразу в кровь.
/// </summary>
public sealed partial class DHPharmacologySystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>Суммарная нагрузка угнетающих дыхание классов (не меньше двух разных), с которой начинается гипоксия.</summary>
    private const float RespiratoryThreshold = 20f;

    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHPharmacologyComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + TimeSpan.FromSeconds(1);
            Tick((uid, comp), now);
        }
    }

    private void Tick(Entity<DHPharmacologyComponent> ent, TimeSpan now)
    {
        var alive = _mobState.IsAlive(ent);
        var depressantLoad = 0f;
        var depressantClasses = 0;

        foreach (var (id, state) in ent.Comp.Classes.ToList())
        {
            if (!_proto.TryIndex(id, out var proto))
            {
                ent.Comp.Classes.Remove(id);
                continue;
            }

            if (state.Load > 0f)
            {
                if (proto.RespiratoryDepressant)
                {
                    depressantLoad += state.Load;
                    depressantClasses++;
                }

                state.LastActive = now;
                state.Exposure += 1f;
                state.Tolerance = MathF.Min(proto.ToleranceMax, state.Tolerance + proto.ToleranceGain);
                if (proto.DependenceThreshold > 0f && !state.Dependent && state.Exposure >= proto.DependenceThreshold)
                    state.Dependent = true;

                if (state.Withdrawal)
                {
                    state.Withdrawal = false;
                    if (alive)
                        Popup(ent, "dh-drug-withdrawal-relieved", PopupType.Small);
                }

                state.Load = MathF.Max(0f, state.Load - proto.Decay);
                continue;
            }

            state.Tolerance = MathF.Max(0f, state.Tolerance - proto.ToleranceDecay);
            state.Exposure = MathF.Max(0f, state.Exposure - 0.2f);

            if (state.Dependent && proto.DependenceThreshold > 0f && state.Exposure < proto.DependenceThreshold * 0.5f)
            {
                state.Dependent = false;
                if (state.Withdrawal && alive)
                    Popup(ent, "dh-drug-withdrawal-over", PopupType.Small);
                state.Withdrawal = false;
            }

            if (state.Dependent && now - state.LastActive >= proto.WithdrawalDelay)
                Withdraw(ent, id.Id, proto, state, now, alive);

            if (state is { Tolerance: <= 0f, Exposure: <= 0f, Dependent: false })
                ent.Comp.Classes.Remove(id);
        }

        if (alive && depressantClasses >= 2 && depressantLoad >= RespiratoryThreshold)
        {
            var damage = MathF.Min(4f, depressantLoad / RespiratoryThreshold);
            _damageable.TryChangeDamage(ent.Owner, new DamageSpecifier { DamageDict = { [Asphyxiation] = FixedPoint2.New(damage) } },
                ignoreResistances: true, interruptsDoAfters: false);

            if (now >= ent.Comp.NextRespiratoryWarning)
            {
                ent.Comp.NextRespiratoryWarning = now + TimeSpan.FromSeconds(20);
                Popup(ent, "dh-drug-respiratory-depression", PopupType.LargeCaution);
            }
        }

        if (ent.Comp.Classes.Count == 0)
            RemCompDeferred<DHPharmacologyComponent>(ent);
    }

    private void Withdraw(Entity<DHPharmacologyComponent> ent, string id, DHDrugClassPrototype proto, DHDrugState state,
        TimeSpan now, bool alive)
    {
        if (!state.Withdrawal)
        {
            state.Withdrawal = true;
            state.NextWithdrawalMessage = now;
        }

        if (!alive)
            return;

        if (proto.WithdrawalPain > 0f && TryComp<DHTraumaComponent>(ent, out var trauma))
            trauma.ExtraPain = MathF.Max(trauma.ExtraPain, proto.WithdrawalPain);

        if (proto.WithdrawalJitter)
            _jitter.DoJitter(ent, TimeSpan.FromSeconds(2), true, 4f, 6f);

        if (proto.WithdrawalLines > 0 && now >= state.NextWithdrawalMessage)
        {
            state.NextWithdrawalMessage = now + TimeSpan.FromSeconds(_random.NextFloat(45f, 90f));
            Popup(ent, $"dh-drug-withdrawal-{id.ToLowerInvariant()}-{_random.Next(1, proto.WithdrawalLines + 1)}", PopupType.MediumCaution);
        }
    }

    private void Popup(EntityUid uid, string loc, PopupType type)
    {
        _popup.PopupEntity(Loc.GetString(loc), uid, uid, type);
    }
}
