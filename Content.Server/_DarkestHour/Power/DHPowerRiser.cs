using Content.Server.Power.Components;
using Content.Shared.Examine;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Power;

/// <summary>
/// Кабельный стояк между уровнями (решение автора 2026-11-13): бункер под базой — отдельная карта, провода туда не
/// дотянуть. Верхний конец (<see cref="Upper"/>) — потребитель на СН поверхности, нижний — источник на СН бункера с тем
/// же <see cref="Link"/>. Сколько верхний получил, столько нижний и отдаёт; верхний берёт столько, сколько бункер просит
/// (плюс небольшой запас). Нет тока наверху — нет и в бункере.
/// </summary>
[RegisterComponent]
public sealed partial class DHPowerRiserComponent : Component
{
    [DataField(required: true)]
    public string Link = string.Empty;

    [DataField]
    public bool Upper;

    /// <summary>Наибольшая мощность через стояк, Вт.</summary>
    [DataField]
    public float MaxRate = 150000f;
}

public sealed partial class DHPowerRiserSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _next;

    /// <summary>Запас сверх того, что бункер сейчас берёт: чтобы рост нагрузки подхватывался.</summary>
    private const float Headroom = 5000f;

    private readonly Dictionary<string, EntityUid> _uppers = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHPowerRiserComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<DHPowerRiserComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var watts = ent.Comp.Upper
            ? TryComp<PowerConsumerComponent>(ent, out var consumer) ? consumer.ReceivedPower : 0f
            : TryComp<PowerSupplierComponent>(ent, out var supplier) ? supplier.CurrentSupply : 0f;
        args.PushMarkup(Loc.GetString("dh-power-riser-examine", ("kw", MathF.Round(watts / 1000f, 1))));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _next)
            return;

        _next = now + Interval;
        _uppers.Clear();
        var query = EntityQueryEnumerator<DHPowerRiserComponent>();
        while (query.MoveNext(out var uid, out var riser))
        {
            if (riser.Upper)
                _uppers[riser.Link] = uid;
        }

        var lowers = EntityQueryEnumerator<DHPowerRiserComponent, PowerSupplierComponent>();
        while (lowers.MoveNext(out var uid, out var riser, out var supplier))
        {
            if (riser.Upper)
                continue;

            if (!_uppers.TryGetValue(riser.Link, out var upper) ||
                !TryComp<PowerConsumerComponent>(upper, out var consumer))
            {
                supplier.MaxSupply = 0f;
                continue;
            }

            // Наверх: просить столько, сколько бункер берёт, с запасом. Вниз: отдать, что получили.
            consumer.DrawRate = MathF.Min(riser.MaxRate, supplier.CurrentSupply + Headroom);
            supplier.MaxSupply = MathF.Min(riser.MaxRate, consumer.ReceivedPower);
        }
    }
}
