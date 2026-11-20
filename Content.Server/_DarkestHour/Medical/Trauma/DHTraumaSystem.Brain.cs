using Content.Shared._DarkestHour.Medical.LifeSupport;
using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.Medical;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Medical.Trauma;

/// <summary>
/// Сердце, мозг и черепно-мозговая травма (план осады п. 18, решения автора 2026-11-20):
/// <list type="bullet">
/// <item>сердце отказало или его нет — мёртвого не поднять ни дефибриллятором, ни СЛР, пока сердце не ушьют или не
/// пересадят (в окне клинической смерти или на жизнеобеспечении);</item>
/// <item>мозг отказал или его нет — смерть мозга навсегда;</item>
/// <item>ЧМТ от ударов и пуль в голову: спутанность, потеря речи, судороги; без трепанации мозг за ~5 минут доходит до
/// отказа (жизнеобеспечение вдвое замедляет).</item>
/// </list>
/// </summary>
public sealed partial class DHTraumaSystem
{
    private static readonly EntProtoId SlurredEffect = "StatusEffectSlurred";
    private static readonly EntProtoId MutedEffect = "StatusEffectMuted";

    private void InitializeBrain()
    {
        SubscribeLocalEvent<DHTraumaComponent, TargetBeforeDefibrillatorZapsEvent>(OnBeforeDefib);
        SubscribeLocalEvent<DHTraumaComponent, RejuvenateEvent>(OnRejuvenate);
    }

    /// <summary>Удар в голову: шанс ЧМТ (тупые удары и пули, сломанный череп — почти наверняка).</summary>
    private void TryBrainInjury(Entity<DHTraumaComponent> ent, DHPartTrauma head, float brute, float blunt,
        float piercing, bool skullBroken)
    {
        if (ent.Comp.BrainInjury || brute < 12f)
            return;

        var chance = blunt / 40f + piercing / 60f + (skullBroken ? 0.4f : 0f);
        if (!_random.Prob(Math.Clamp(chance, 0f, 0.8f)))
            return;

        ent.Comp.BrainInjury = true;
        _popup.PopupEntity(Loc.GetString("dh-trauma-brain-injury-self"), ent, ent, PopupType.LargeCaution);
    }

    /// <summary>Взрыв рядом: контузия может перейти в ЧМТ.</summary>
    private void TryBlastBrainInjury(Entity<DHTraumaComponent> ent, float brute)
    {
        if (ent.Comp.BrainInjury || brute < 15f || !_random.Prob(Math.Clamp(brute / 100f, 0f, 0.35f)))
            return;

        ent.Comp.BrainInjury = true;
        _popup.PopupEntity(Loc.GetString("dh-trauma-brain-injury-self"), ent, ent, PopupType.LargeCaution);
    }

    /// <summary>Живой с ЧМТ: мозг повреждается, симптомы.</summary>
    private void TickBrain(Entity<DHTraumaComponent> ent, bool incapacitated)
    {
        var comp = ent.Comp;
        if (!comp.BrainInjury)
            return;

        if (!GetOrgans(ent).ContainsKey("Brain"))
        {
            comp.BrainInjury = false;
            return;
        }

        var rate = comp.BrainInjuryRate * (float) comp.Interval.TotalSeconds;
        if (TryComp<DHMachinePatientComponent>(ent, out var patient) && patient.LifeSupport != null)
            rate *= 0.5f;

        DamageOrgan(ent, "Brain", rate);

        if (incapacitated)
            return;

        // Спутанность: смазанная речь и шатание.
        if (_random.Prob(0.05f))
        {
            _status.TryUpdateStatusEffectDuration(ent, SlurredEffect, TimeSpan.FromSeconds(20));
            _jitter.DoJitter(ent, TimeSpan.FromSeconds(2), true);
            _popup.PopupEntity(Loc.GetString("dh-trauma-brain-confused"), ent, ent, PopupType.SmallCaution);
        }

        // Потеря речи.
        if (_random.Prob(0.02f))
        {
            _status.TryUpdateStatusEffectDuration(ent, MutedEffect, TimeSpan.FromSeconds(15));
            _popup.PopupEntity(Loc.GetString("dh-trauma-brain-mute"), ent, ent, PopupType.MediumCaution);
        }

        // Судороги — чаще, чем сильнее поражён мозг.
        var brain = comp.Organs.TryGetValue("Brain", out var damage) ? damage / comp.OrganMaxDamage : 0f;
        if (_random.Prob(0.01f + brain * 0.03f))
        {
            _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(4));
            _jitter.DoJitter(ent, TimeSpan.FromSeconds(6), true, 20f, 8f);
            _popup.PopupEntity(Loc.GetString("dh-trauma-brain-seizure", ("target", ent.Owner)), ent,
                PopupType.MediumCaution);
        }
    }

    /// <summary>Трепанация: гематома удалена, мозг больше не повреждается (уже полученное — ушивание органов).</summary>
    public void Trepanation(EntityUid uid)
    {
        if (TryComp<DHTraumaComponent>(uid, out var comp))
            comp.BrainInjury = false;
    }

    public bool HasBrainInjury(EntityUid uid, string category) =>
        category == "Head" && TryComp<DHTraumaComponent>(uid, out var comp) && comp.BrainInjury;

    #region Реанимация

    private bool OrganFailed(Entity<DHTraumaComponent> ent, Dictionary<string, EntityUid> organs, string organ)
    {
        return !organs.ContainsKey(organ) ||
               ent.Comp.Organs.TryGetValue(organ, out var damage) && damage >= ent.Comp.OrganMaxDamage;
    }

    /// <summary>Мёртвый: отказ сердца — реанимация запрещена до ремонта; отказ мозга — смерть мозга навсегда.</summary>
    private void UpdateReviveState(Entity<DHTraumaComponent> ent)
    {
        if (!_mobState.IsDead(ent))
        {
            RemComp<DHReviveBlockedComponent>(ent);
            return;
        }

        var organs = GetOrgans(ent);

        if (!HasComp<DHBrainDeadComponent>(ent) && OrganFailed(ent, organs, "Brain"))
        {
            var unrevivable = EnsureComp<UnrevivableComponent>(ent);
            unrevivable.ReasonMessage = "dh-revive-brain-damaged";
            Dirty(ent, unrevivable);
            EnsureComp<DHBrainDeadComponent>(ent);
        }

        if (OrganFailed(ent, organs, "Heart"))
            EnsureComp<DHReviveBlockedComponent>(ent);
        else
            RemComp<DHReviveBlockedComponent>(ent);
    }

    private void OnBeforeDefib(Entity<DHTraumaComponent> ent, ref TargetBeforeDefibrillatorZapsEvent args)
    {
        if (!TryComp<DHReviveBlockedComponent>(ent, out var blocked) || HasComp<UnrevivableComponent>(ent))
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString(blocked.Reason), ent, args.EntityUsingDefib, PopupType.MediumCaution);
    }

    /// <summary>Строки анализатора и вскрытия: почему не реанимировать.</summary>
    private void AddReviveSummary(Entity<DHTraumaComponent> ent, List<string> lines)
    {
        if (!_mobState.IsDead(ent))
            return;

        if (HasComp<DHBrainDeadComponent>(ent))
            lines.Add(Loc.GetString("dh-trauma-analyzer-revive-brain"));
        else if (HasComp<DHReviveBlockedComponent>(ent))
            lines.Add(Loc.GetString("dh-trauma-analyzer-revive-heart"));
    }

    #endregion

    private void OnRejuvenate(Entity<DHTraumaComponent> ent, ref RejuvenateEvent args)
    {
        ent.Comp.Parts.Clear();
        ent.Comp.Organs.Clear();
        ent.Comp.BrainInjury = false;
        ClearSpinal(ent);
        DrainChest(ent);
        ent.Comp.AirwayBlocked = false;
        ent.Comp.EyesLost = false;
        if (ent.Comp.Disfigured)
        {
            ent.Comp.Disfigured = false;
            _identity.QueueIdentityUpdate(ent);
        }
        UpdateEyes(ent);
        ent.Comp.FireRateModifier = 1f;
        ent.Comp.PainSpike = 0f;
        ent.Comp.ExtraPain = 0f;
        RemComp<DHReviveBlockedComponent>(ent);
        _vital.Recalculate(ent.Owner);
        Dirty(ent);
    }
}
