using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;

namespace Content.Server._DarkestHour.Medical.Trauma;

/// <summary>
/// Ожоги III степени, лицо и глаза (план осады п. 18, решения автора 2026-11-20):
/// <list type="bullet">
/// <item>ожог III степени — от сильных ожогов части тела: часть теряет жидкость и болит, мази урон снимают, но сам
/// ожог остаётся, пока его не иссекут и не пересадят кожный трансплантат;</item>
/// <item>обезображенное лицо — от тяжёлых ранений и ожогов головы: человека не узнать (как в маске);</item>
/// <item>глаза — повреждение органа «глаза» мутит зрение, отказ или отсутствие — слепота. Реконструкция лица и глаз
/// возвращает лицо и чинит глаза (без глаз — пересадка).</item>
/// </list>
/// </summary>
public sealed partial class DHTraumaSystem
{
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private IdentitySystem _identity = default!;

    /// <summary>Потеря крови (плазмы) в секунду с каждой части с ожогом III степени.</summary>
    private const float DeepBurnFluidLoss = 0.15f;

    private const float DeepBurnPain = 15f;

    /// <summary>Повреждение глаз (минимум BlindableComponent), если глаза повреждены наполовину.</summary>
    private const int EyesBlurry = 5;

    #region Травмы

    private void TryDeepBurn(Entity<DHTraumaComponent> ent, string category, DHPartTrauma part, float burn)
    {
        if (part.DeepBurn || burn < 12f)
            return;

        var chance = burn / 40f + (part.Burn >= MaxOf(ent.Comp, category) * 0.6f ? 0.2f : 0f);
        if (!_random.Prob(Math.Clamp(chance, 0f, 0.6f)))
            return;

        part.DeepBurn = true;
        _popup.PopupEntity(Loc.GetString("dh-trauma-deep-burn-self", ("part", PartName(category))), ent, ent,
            PopupType.MediumCaution);
    }

    /// <summary>Тяжёлое ранение или ожог разбитой головы: лицо обезображено; сильный ожог задевает глаза.</summary>
    private void TryFaceInjury(Entity<DHTraumaComponent> ent, string category, DHPartTrauma part, float brute,
        float burn)
    {
        if (category != "Head")
            return;

        if (burn >= 15f && _random.Prob(0.3f) && GetOrgans(ent).ContainsKey("Eyes"))
            DamageOrgan(ent, "Eyes", burn * 0.5f);

        if (ent.Comp.Disfigured || brute + burn < 20f ||
            part.Brute + part.Burn < MaxOf(ent.Comp, category) * 0.6f || !_random.Prob(0.25f))
        {
            return;
        }

        ent.Comp.Disfigured = true;
        Dirty(ent);
        _identity.QueueIdentityUpdate(ent);
        _popup.PopupEntity(Loc.GetString("dh-trauma-disfigured-self"), ent, ent, PopupType.MediumCaution);
    }

    #endregion

    /// <summary>Ожоги III степени теряют жидкость; глаза — по состоянию органа.</summary>
    private void TickBurns(Entity<DHTraumaComponent> ent)
    {
        var deep = 0;
        foreach (var part in ent.Comp.Parts.Values)
        {
            if (part.DeepBurn)
                deep++;
        }

        if (deep > 0)
        {
            var loss = deep * DeepBurnFluidLoss * (float) ent.Comp.Interval.TotalSeconds;
            _bloodstream.TryModifyBloodLevel(ent.Owner, FixedPoint2.New(-loss));
        }

        UpdateEyes(ent);
    }

    private static float DeepBurnPainOf(DHPartTrauma part) => part.DeepBurn ? DeepBurnPain : 0f;

    /// <summary>Глаза: отказ или нет органа — слепота, повреждены наполовину — муть. Врождённое не трогаем.</summary>
    private void UpdateEyes(Entity<DHTraumaComponent> ent)
    {
        if (!TryComp<BlindableComponent>(ent, out var blindable))
            return;

        var comp = ent.Comp;
        var present = GetOrgans(ent).ContainsKey("Eyes");
        var damage = comp.Organs.TryGetValue("Eyes", out var d) ? d : 0f;
        var desired = !present && comp.EyesLost || present && damage >= comp.OrganMaxDamage
            ? blindable.MaxDamage
            : damage >= comp.OrganMaxDamage * 0.5f ? EyesBlurry : 0;

        if (desired == 0)
        {
            if (comp.EyeMinApplied < 0)
                return;

            comp.EyeMinApplied = -1;
            _blindable.SetMinDamage((ent.Owner, blindable), comp.EyeBaseMin);
            _blindable.AdjustEyeDamage((ent.Owner, blindable), -blindable.MaxDamage);
            return;
        }

        if (desired == comp.EyeMinApplied)
            return;

        if (comp.EyeMinApplied < 0)
            comp.EyeBaseMin = blindable.MinDamage;

        comp.EyeMinApplied = desired;
        _blindable.SetMinDamage((ent.Owner, blindable), Math.Max(comp.EyeBaseMin, desired));
    }

    #region API (хирургия)

    public bool HasDeepBurn(EntityUid uid, string category) =>
        TryComp<DHTraumaComponent>(uid, out var comp) && comp.Parts.TryGetValue(category, out var p) && p.DeepBurn;

    public void TreatDeepBurn(EntityUid uid, string category)
    {
        if (TryComp<DHTraumaComponent>(uid, out var comp) && comp.Parts.TryGetValue(category, out var p))
            p.DeepBurn = false;
    }

    /// <summary>Есть что восстанавливать: лицо обезображено или глаза повреждены (но на месте).</summary>
    public bool HasFaceInjury(EntityUid uid, string category)
    {
        if (category != "Head" || !TryComp<DHTraumaComponent>(uid, out var comp))
            return false;

        return comp.Disfigured || comp.Organs.TryGetValue("Eyes", out var eyes) && eyes > 0f;
    }

    public void ReconstructFace(EntityUid uid)
    {
        if (!TryComp<DHTraumaComponent>(uid, out var comp))
            return;

        comp.Organs.Remove("Eyes");
        if (comp.Disfigured)
        {
            comp.Disfigured = false;
            Dirty(uid, comp);
            _identity.QueueIdentityUpdate(uid);
        }

        UpdateEyes((uid, comp));
    }

    #endregion
}
