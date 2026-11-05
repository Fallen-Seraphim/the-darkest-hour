using Content.Shared._DarkestHour.Environment;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Environment;

/// <summary>
/// Сервер держит свет карты в такт со сменой дня и ночи (раз в 5 с) — для новых игроков и всего, что читает
/// свет карты на сервере. Плавно его рисует клиент.
/// </summary>
public sealed partial class DHDayNightSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private TimeSpan _next;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHDayNightCycleComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<DHDayNightCycleComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Start = _timing.CurTime;
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _next)
            return;

        _next = now + Interval;
        var query = EntityQueryEnumerator<DHDayNightCycleComponent, MapLightComponent>();
        while (query.MoveNext(out var uid, out var cycle, out var light))
        {
            light.AmbientLightColor = DHDayNight.GetColor(cycle, DHDayNight.Fraction(cycle, now));
            Dirty(uid, light);
        }
    }

    /// <summary>Включить смену дня и ночи на карте поверхности со светом планеты <paramref name="noon"/>.</summary>
    public void Enable(EntityUid map, Color noon)
    {
        var cycle = EnsureComp<DHDayNightCycleComponent>(map);
        cycle.BaseColor = noon;
        cycle.Start = _timing.CurTime;
        Dirty(map, cycle);
    }
}
