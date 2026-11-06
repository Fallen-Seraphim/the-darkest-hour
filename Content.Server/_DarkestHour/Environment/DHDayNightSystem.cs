using Content.Shared._DarkestHour.Environment;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Console;

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

/// <summary>
/// dhdaynight — время суток на карте, где стоит админ: доля суток, часы и цвет. dhdaynight 0.5 — поставить полдень
/// (0 — полночь, 0.25 — восход, 0.75 — закат) на всех картах со сменой суток.
/// </summary>
[Content.Server.Administration.AdminCommand(Content.Shared.Administration.AdminFlags.Admin)]
public sealed partial class DHDayNightCommand : LocalizedEntityCommands
{
    [Dependency] private IGameTiming _timing = default!;

    public override string Command => "dhdaynight";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } player ||
            EntityManager.GetComponent<TransformComponent>(player).MapUid is not { } map ||
            !EntityManager.TryGetComponent<DHDayNightCycleComponent>(map, out var cycle))
        {
            shell.WriteError(Loc.GetString("cmd-dhdaynight-no-cycle"));
            return;
        }

        var now = _timing.CurTime;
        if (args.Length > 0)
        {
            if (!float.TryParse(args[0], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var target) || target < 0f || target >= 1f)
            {
                shell.WriteError(Loc.GetString("cmd-dhdaynight-bad"));
                return;
            }

            // Все карты со сменой суток (поверхность и этажи) — одинаково, чтобы уровни не разошлись.
            var query = EntityManager.EntityQueryEnumerator<DHDayNightCycleComponent>();
            while (query.MoveNext(out var uid, out var other))
            {
                other.Start = now;
                other.StartFraction = target;
                EntityManager.Dirty(uid, other);
            }
        }

        var fraction = DHDayNight.Fraction(cycle, now);
        var hours = fraction * 24f;
        shell.WriteLine(Loc.GetString("cmd-dhdaynight-status",
            ("map", EntityManager.ToPrettyString(map)),
            ("fraction", fraction.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
            ("time", $"{(int) hours:00}:{(int) (hours % 1f * 60f):00}"),
            ("color", DHDayNight.GetColor(cycle, fraction).ToHex()),
            ("minutes", (int) cycle.Duration.TotalMinutes)));
    }
}
