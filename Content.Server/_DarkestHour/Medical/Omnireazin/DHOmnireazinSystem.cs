using Content.Shared._DarkestHour.Medical.Omnireazin;
using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Medical.Omnireazin;

/// <summary>
/// Последствия омниреазина: когда действие кончается — боль возвращается; после нескольких курсов с тяжёлыми
/// ранениями — фантомные боли (их не снимает ничто); после многих — деперсонализация («тело чужое»).
/// «Солдат, которому она помогла, всё ещё ранен. Он просто может продолжать бой. Пока.»
/// </summary>
public sealed partial class DHOmnireazinSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;

    /// <summary>С какого курса с тяжёлой раной появляются фантомные боли.</summary>
    private const int PhantomCourses = 3; // было 2: отсрочено (решение автора 2026-11-12)

    /// <summary>С какого курса — деперсонализация.</summary>
    private const int DepersonalizationCourses = 5; // было 4

    private const float PhantomPerCourse = 6f;
    private const float PhantomMax = 25f;

    private const int PhantomLines = 4;
    private const int DepersonalizationLines = 5;

    private TimeSpan _nextUpdate;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<DHOmnireazinComponent, DHTraumaComponent>();
        while (query.MoveNext(out var uid, out var omni, out var trauma))
        {
            if (omni.Active && now >= omni.ActiveUntil)
                WearOff((uid, omni, trauma), now);

            if (!_mobState.IsAlive(uid))
                continue;

            if (trauma.PhantomPain > 0f && now >= omni.NextPhantom)
            {
                omni.NextPhantom = now + TimeSpan.FromSeconds(_random.NextFloat(90f, 180f));
                Popup(uid, $"dh-omnireazin-phantom-{_random.Next(1, PhantomLines + 1)}", PopupType.MediumCaution);
            }

            if (omni.HeavyCourses >= DepersonalizationCourses && now >= omni.NextDepersonalization)
            {
                omni.NextDepersonalization = now + TimeSpan.FromSeconds(_random.NextFloat(180f, 360f));
                Popup(uid, $"dh-omnireazin-depersonalization-{_random.Next(1, DepersonalizationLines + 1)}", PopupType.Medium);
            }
        }
    }

    private void WearOff(Entity<DHOmnireazinComponent, DHTraumaComponent> ent, TimeSpan now)
    {
        var (uid, omni, trauma) = ent;
        omni.Active = false;

        if (omni.Rebound > 1f)
        {
            trauma.ExtraPain += omni.Rebound;
            if (_mobState.IsAlive(uid))
                Popup(uid, "dh-omnireazin-wears-off", PopupType.MediumCaution);
        }

        omni.Rebound = 0f;

        if (!omni.HeavyThisCourse)
            return;

        omni.HeavyThisCourse = false;
        omni.HeavyCourses++;

        if (omni.HeavyCourses < PhantomCourses)
            return;

        var phantom = MathF.Min(PhantomMax, PhantomPerCourse * (omni.HeavyCourses - PhantomCourses + 1));
        if (phantom <= trauma.PhantomPain)
            return;

        var first = trauma.PhantomPain <= 0f;
        trauma.PhantomPain = phantom;
        if (first)
        {
            omni.NextPhantom = now + TimeSpan.FromSeconds(120);
            Popup(uid, "dh-omnireazin-phantom-start", PopupType.LargeCaution);
        }

        if (omni.HeavyCourses == DepersonalizationCourses)
            omni.NextDepersonalization = now + TimeSpan.FromSeconds(30);
    }

    private void Popup(EntityUid uid, string loc, PopupType type)
    {
        _popup.PopupEntity(Loc.GetString(loc), uid, uid, type);
    }
}
