using System.Linq;
using Content.Shared._DarkestHour.Clothing.ModularArmor;
using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server._DarkestHour.Medical.Trauma;

/// <summary>
/// Нервы и спинной мозг (план осады п. 18, решения автора 2026-11-20):
/// <list type="bullet">
/// <item>нерв конечности — от глубоких рубящих и колющих ран, пуль и осколков, переломов: рука роняет предметы и
/// стреляет медленнее, нога хромает, боль из этой части не чувствуется. Лечится операцией «сшить нерв»;</item>
/// <item>спинной мозг — от тяжёлых ударов, пуль в торс и шею, взрывов и падений: паралич ног (полный — и рук).
/// Парализованный только ползёт; в экзоскелете ходит на 70% скорости. Декомпрессия позвоночника в течение
/// 10 минут лечит, позже — паралич до конца раунда.</item>
/// </list>
/// </summary>
public sealed partial class DHTraumaSystem
{
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private InventorySystem _inventory = default!;

    private const float NerveFireRate = 0.7f;
    private const float NerveLegSpeed = 0.8f;
    private const float SpinalSupportedSpeed = 0.7f;

    private static readonly string[] Limbs =
        { "ArmLeft", "ArmRight", "HandLeft", "HandRight", "LegLeft", "LegRight", "FootLeft", "FootRight" };

    #region Травмы

    /// <summary>Нерв конечности: глубокая рубленая или колотая рана, пуля, перелом.</summary>
    private void TryNerveDamage(Entity<DHTraumaComponent> ent, string category, DHPartTrauma part, float slash,
        float piercing, bool newFracture)
    {
        if (part.Nerve || Array.IndexOf(Limbs, category) < 0 || slash + piercing < 12f && !newFracture)
            return;

        var chance = slash / 80f + piercing / 100f + (newFracture ? 0.15f : 0f);
        if (!_random.Prob(Math.Clamp(chance, 0f, 0.4f)))
            return;

        part.Nerve = true;
        _popup.PopupEntity(Loc.GetString("dh-trauma-nerve-self", ("part", PartName(category))), ent, ent,
            PopupType.MediumCaution);
    }

    /// <summary>Спинной мозг: тяжёлый удар или пуля в торс или шею (голову), взрыв, падение.</summary>
    private void TrySpinalInjury(Entity<DHTraumaComponent> ent, string category, float brute, float blunt,
        float piercing, bool explosion)
    {
        if (ent.Comp.Spinal == DHSpinalInjury.Full || category != "Torso" && category != "Head")
            return;

        var chance = MathF.Max(0f, blunt - 15f) / 150f + MathF.Max(0f, piercing - 25f) / 120f;
        if (explosion)
            chance += brute / 300f;
        if (category == "Head")
            chance *= 0.5f; // шея

        if (!_random.Prob(Math.Clamp(chance, 0f, 0.25f)))
            return;

        // Шея или очень тяжёлый удар — полный паралич.
        var full = category == "Head" || brute >= 45f && _random.Prob(0.5f);
        SetSpinal(ent, full ? DHSpinalInjury.Full : DHSpinalInjury.Legs);
    }

    private void SetSpinal(Entity<DHTraumaComponent> ent, DHSpinalInjury injury)
    {
        if (injury <= ent.Comp.Spinal)
            return;

        if (ent.Comp.Spinal == DHSpinalInjury.None)
            ent.Comp.SpinalSince = _timing.CurTime;

        ent.Comp.Spinal = injury;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString(injury == DHSpinalInjury.Full ? "dh-trauma-spinal-full-self" : "dh-trauma-spinal-legs-self"),
            ent, ent, PopupType.LargeCaution);
    }

    #endregion

    #region Последствия

    private void TickNerves(Entity<DHTraumaComponent> ent, bool incapacitated)
    {
        var comp = ent.Comp;

        // Рука с повреждённым нервом: роняет предметы, оружие в руках стреляет медленнее.
        var leftArm = NerveIn(comp, "ArmLeft", "HandLeft");
        var rightArm = NerveIn(comp, "ArmRight", "HandRight");
        var fireRate = leftArm || rightArm ? NerveFireRate : 1f;
        if (MathF.Abs(fireRate - comp.FireRateModifier) > 0.01f)
        {
            comp.FireRateModifier = fireRate;
            Dirty(ent);
            foreach (var held in _hands.EnumerateHeld(ent.Owner).ToList())
            {
                if (HasComp<GunComponent>(held))
                    _gun.RefreshModifiers(held);
            }
        }

        if (incapacitated)
            return;

        if (leftArm && _random.Prob(0.03f) && _hands.TryDrop(ent.Owner, "left"))
            _popup.PopupEntity(Loc.GetString("dh-trauma-nerve-drop"), ent, ent, PopupType.SmallCaution);
        if (rightArm && _random.Prob(0.03f) && _hands.TryDrop(ent.Owner, "right"))
            _popup.PopupEntity(Loc.GetString("dh-trauma-nerve-drop"), ent, ent, PopupType.SmallCaution);
    }

    /// <summary>Паралич: без экзоскелета — лёжа (ползком), полный — руки ничего не держат. Окно декомпрессии.</summary>
    private void TickSpinal(Entity<DHTraumaComponent> ent, TimeSpan now, ref float speed)
    {
        var comp = ent.Comp;
        if (comp.Spinal == DHSpinalInjury.None)
            return;

        if (!comp.SpinalPermanent && now >= comp.SpinalSince + comp.SpinalWindow)
        {
            comp.SpinalPermanent = true;
            _popup.PopupEntity(Loc.GetString("dh-trauma-spinal-permanent"), ent, ent, PopupType.LargeCaution);
        }

        var supported = _inventory.TryGetSlotEntity(ent.Owner, "outerClothing", out var outer) &&
                        HasComp<DHExoskeletonComponent>(outer);
        if (supported != comp.SpinalSupported)
        {
            comp.SpinalSupported = supported;
            Dirty(ent);
        }

        if (supported)
            speed *= SpinalSupportedSpeed;
        else if (!HasComp<KnockedDownComponent>(ent))
            _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(1), refresh: false, autoStand: false, drop: false, force: true);

        // Полный паралич: руки не держат.
        if (comp.Spinal == DHSpinalInjury.Full)
        {
            foreach (var held in _hands.EnumerateHeld(ent.Owner).ToList())
            {
                _hands.TryDrop(ent.Owner, held, checkActionBlocker: false);
            }
        }
    }

    private string SpinalFinding(DHTraumaComponent comp)
    {
        var key = comp.Spinal == DHSpinalInjury.Full ? "dh-trauma-desc-spinal-full" : "dh-trauma-desc-spinal-legs";
        return Loc.GetString(key) + Loc.GetString(comp.SpinalPermanent ? "dh-trauma-desc-spinal-permanent" : "dh-trauma-desc-spinal-treatable");
    }

    private static bool NerveIn(DHTraumaComponent comp, string arm, string hand)
    {
        return comp.Parts.TryGetValue(arm, out var a) && a.Nerve || comp.Parts.TryGetValue(hand, out var h) && h.Nerve;
    }

    #endregion

    #region API (хирургия)

    public bool HasNerveDamage(EntityUid uid, string category) =>
        TryComp<DHTraumaComponent>(uid, out var comp) && comp.Parts.TryGetValue(category, out var p) && p.Nerve;

    public void RepairNerve(EntityUid uid, string category)
    {
        if (TryComp<DHTraumaComponent>(uid, out var comp) && comp.Parts.TryGetValue(category, out var p))
            p.Nerve = false;
    }

    /// <summary>Декомпрессия поможет: травма есть и ещё не запущена.</summary>
    public bool HasTreatableSpinalInjury(EntityUid uid, string category) =>
        category == "Torso" && TryComp<DHTraumaComponent>(uid, out var comp) && comp.Spinal != DHSpinalInjury.None &&
        !comp.SpinalPermanent;

    public void SpinalDecompression(EntityUid uid)
    {
        if (!TryComp<DHTraumaComponent>(uid, out var comp) || comp.SpinalPermanent)
            return;

        ClearSpinal((uid, comp));
    }

    private void ClearSpinal(Entity<DHTraumaComponent> ent)
    {
        var wasDown = ent.Comp.Spinal != DHSpinalInjury.None && !ent.Comp.SpinalSupported;
        ent.Comp.Spinal = DHSpinalInjury.None;
        ent.Comp.SpinalPermanent = false;
        ent.Comp.SpinalSupported = false;
        Dirty(ent);

        // Снова может встать (как после обычного падения).
        if (wasDown && TryComp<KnockedDownComponent>(ent, out var knocked))
            _stun.SetAutoStand((ent.Owner, knocked), true);
    }

    #endregion
}
