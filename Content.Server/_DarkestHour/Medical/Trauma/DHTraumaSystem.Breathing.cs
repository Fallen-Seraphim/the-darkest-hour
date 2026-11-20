using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Medical.Trauma;

/// <summary>
/// Грудь и дыхательные пути (план осады п. 18, решения автора 2026-11-20):
/// <list type="bullet">
/// <item>пневмоторакс — от пуль, осколков и ударов в грудь (сломанные рёбра): удушье, которое нарастает, пока рану не
/// закроют окклюзионной повязкой; игла-декомпрессия санитара стравливает напряжение на время, дренаж грудной клетки
/// лечит;</item>
/// <item>перекрытые дыхательные пути — от тяжёлых ударов по разбитой голове и ожогов головы: пациент задыхается,
/// пока ему не сделают трахеостомию (на столе или на полу).</item>
/// </list>
/// </summary>
public sealed partial class DHTraumaSystem
{
    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";

    /// <summary>За сколько секунд пневмоторакс доходит до полного напряжения.</summary>
    private const float PneumothoraxGrowth = 120f;

    private void InitializeBreathing()
    {
        SubscribeLocalEvent<DHDecompressionNeedleComponent, AfterInteractEvent>(OnNeedleAfterInteract);
        SubscribeLocalEvent<DHTraumaComponent, DHNeedleDecompressionDoAfterEvent>(OnNeedleDoAfter);
    }

    #region Травмы

    private void TryChestInjury(Entity<DHTraumaComponent> ent, string category, float piercing, float blunt,
        bool newFracture)
    {
        if (category != "Torso" || ent.Comp.Pneumothorax > 0f)
            return;

        var chance = piercing / 80f + MathF.Max(0f, blunt - 20f) / 150f + (newFracture ? 0.3f : 0f);
        if (!_random.Prob(Math.Clamp(chance, 0f, 0.5f)))
            return;

        ent.Comp.Pneumothorax = 0.1f;
        ent.Comp.ChestSealed = false;
        _popup.PopupEntity(Loc.GetString("dh-trauma-pneumothorax-self"), ent, ent, PopupType.MediumCaution);
    }

    private void TryAirwayInjury(Entity<DHTraumaComponent> ent, string category, DHPartTrauma part, float brute,
        float burn, float blunt, float slash)
    {
        if (category != "Head" || ent.Comp.AirwayBlocked)
            return;

        var chance = 0f;
        if (brute >= 15f && blunt + slash >= 15f && part.Brute >= MaxOf(ent.Comp, category) * 0.5f)
            chance += 0.15f;
        if (burn >= 20f)
            chance += 0.1f;

        if (!_random.Prob(chance))
            return;

        ent.Comp.AirwayBlocked = true;
        _popup.PopupEntity(Loc.GetString("dh-trauma-airway-self"), ent, ent, PopupType.LargeCaution);
    }

    #endregion

    /// <summary>Удушье от пневмоторакса и перекрытых дыхательных путей; рост напряжения.</summary>
    private void TickBreathing(Entity<DHTraumaComponent> ent, TimeSpan now, bool incapacitated)
    {
        var comp = ent.Comp;
        var seconds = (float) comp.Interval.TotalSeconds;
        var asphyxiation = 0f;

        if (comp.Pneumothorax > 0f)
        {
            if (!comp.ChestSealed && now >= comp.DecompressedUntil)
                comp.Pneumothorax = MathF.Min(1f, comp.Pneumothorax + seconds / PneumothoraxGrowth);

            // Дыхание само снимает ~1 удушья в секунду: лёгкий пневмоторакс — одышка, напряжённый — удушье.
            asphyxiation += 1.1f + 2f * comp.Pneumothorax;
            if (!incapacitated && _random.Prob(0.04f + 0.08f * comp.Pneumothorax))
                _popup.PopupEntity(Loc.GetString("dh-trauma-pneumothorax-breath"), ent, ent, PopupType.SmallCaution);
        }

        if (comp.AirwayBlocked)
        {
            asphyxiation += 2.5f;
            if (!incapacitated && _random.Prob(0.1f))
                _popup.PopupEntity(Loc.GetString("dh-trauma-airway-choke"), ent, ent, PopupType.MediumCaution);
        }

        if (asphyxiation <= 0f)
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict[Asphyxiation] = FixedPoint2.New(asphyxiation * seconds);
        _damageable.TryChangeDamage(ent.Owner, damage, ignoreResistances: true, interruptsDoAfters: false);
    }

    private void AddBreathingFindings(DHTraumaComponent comp, string category, List<string> findings)
    {
        if (category == "Torso" && comp.Pneumothorax > 0f)
        {
            findings.Add(Loc.GetString("dh-trauma-desc-pneumothorax", ("percent", (int) (comp.Pneumothorax * 100f))));
            if (comp.ChestSealed)
                findings.Add(Loc.GetString("dh-trauma-desc-chest-sealed"));
        }

        if (category == "Head" && comp.AirwayBlocked)
            findings.Add(Loc.GetString("dh-trauma-desc-airway"));
    }

    #region Окклюзионная повязка и игла

    /// <summary>Окклюзионная повязка на грудь: пневмоторакс перестаёт нарастать.</summary>
    private void SealChest(Entity<DHTraumaComponent> ent)
    {
        if (ent.Comp.Pneumothorax > 0f)
            ent.Comp.ChestSealed = true;
    }

    private void OnNeedleAfterInteract(Entity<DHDecompressionNeedleComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target ||
            !TryComp<DHTraumaComponent>(target, out var trauma))
        {
            return;
        }

        args.Handled = true;
        if (trauma.Pneumothorax <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("dh-needle-nothing"), target, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay,
            new DHNeedleDecompressionDoAfterEvent(), target, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnNeedleDoAfter(Entity<DHTraumaComponent> ent, ref DHNeedleDecompressionDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } item ||
            !TryComp<DHDecompressionNeedleComponent>(item, out var needle) || ent.Comp.Pneumothorax <= 0f)
        {
            return;
        }

        args.Handled = true;
        ent.Comp.PainSpike += 20f;

        // Полевая процедура: можно промахнуться — укол без толку, игла цела.
        if (_random.Prob(needle.FailChance))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict["Piercing"] = FixedPoint2.New(5);
            _damageable.TryChangeDamage(ent.Owner, damage, origin: args.User);
            _popup.PopupEntity(Loc.GetString("dh-needle-miss", ("user", args.User), ("target", ent.Owner)), ent,
                PopupType.MediumCaution);
            return;
        }

        ent.Comp.Pneumothorax = 0.1f;
        ent.Comp.DecompressedUntil = _timing.CurTime + needle.Relief;
        _popup.PopupEntity(Loc.GetString("dh-needle-done", ("user", args.User), ("target", ent.Owner)), ent,
            PopupType.Medium);
        QueueDel(item);
        UpdateSummary(ent);
    }

    #endregion

    #region API (хирургия)

    public bool HasPneumothorax(EntityUid uid, string category) =>
        category == "Torso" && TryComp<DHTraumaComponent>(uid, out var comp) && comp.Pneumothorax > 0f;

    public void DrainChest(EntityUid uid)
    {
        if (!TryComp<DHTraumaComponent>(uid, out var comp))
            return;

        comp.Pneumothorax = 0f;
        comp.ChestSealed = false;
        comp.DecompressedUntil = TimeSpan.Zero;
    }

    public bool HasBlockedAirway(EntityUid uid, string category) =>
        category == "Head" && TryComp<DHTraumaComponent>(uid, out var comp) && comp.AirwayBlocked;

    public void Tracheostomy(EntityUid uid)
    {
        if (TryComp<DHTraumaComponent>(uid, out var comp))
            comp.AirwayBlocked = false;
    }

    #endregion
}
