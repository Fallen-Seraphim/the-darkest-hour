using Content.Shared._DarkestHour.Medical.Surgery;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Medical.Surgery;

/// <summary>
/// Раневая инфекция после нестерильной операции. Упрощённая версия до движка болезней (этап 2, шаг 3), где она станет
/// обычной болезнью «Раневая инфекция» из списка. Тикает только у заражённых.
/// </summary>
public sealed partial class DHWoundInfectionSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan IncubationMin = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan IncubationMax = TimeSpan.FromMinutes(4);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHWoundInfectionComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>Заразить или усилить уже идущую инфекцию.</summary>
    public void Infect(EntityUid uid, float severity = 0.5f)
    {
        if (TryComp<DHWoundInfectionComponent>(uid, out var existing))
        {
            existing.Severity = MathF.Min(existing.MaxSeverity, existing.Severity + severity);
            return;
        }

        var comp = AddComp<DHWoundInfectionComponent>(uid);
        comp.Severity = severity;
        var incubation = _random.Next(IncubationMin, IncubationMax);
        comp.IncubationEnd = _timing.CurTime + incubation;
        comp.NextUpdate = comp.IncubationEnd;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHWoundInfectionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + comp.Interval;
            if (_mobState.IsDead(uid))
                continue;

            comp.Severity = MathF.Min(comp.MaxSeverity, comp.Severity + comp.Growth);
            if (!comp.Damage.Empty)
                _damageable.TryChangeDamage(uid, comp.Damage * comp.Severity, interruptsDoAfters: false);

            if (_random.Prob(0.25f))
            {
                var key = comp.Severity >= 2f ? "dh-wound-infection-severe" : "dh-wound-infection-mild";
                _popup.PopupEntity(Loc.GetString(key), uid, uid, PopupType.SmallCaution);
            }
        }
    }

    private void OnExamined(Entity<DHWoundInfectionComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || _timing.CurTime < ent.Comp.IncubationEnd)
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.Severity >= 2f
            ? "dh-wound-infection-examine-severe"
            : "dh-wound-infection-examine", ("target", ent.Owner)));
    }
}
