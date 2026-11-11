using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Examine;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Medical.Trauma;

/// <summary>
/// Отделённая часть тела сохнет вне холода: раз в секунду копится время; в холодильнике, морозильном шкафу или морге
/// (всё, что держит тела от гниения, <see cref="AntiRottingContainerComponent"/>) — не копится. Иссохшую не пришить.
/// </summary>
public sealed partial class DHLimbDecaySystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private TimeSpan _next;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHLimbDecayComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _next)
            return;

        _next = now + Interval;
        var dt = (float) Interval.TotalSeconds;

        var query = EntityQueryEnumerator<DHLimbDecayComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Withered)
                continue;

            var cold = InColdStorage(uid);
            var dirty = cold != comp.Cold;
            comp.Cold = cold;

            if (!cold)
            {
                comp.Elapsed += dt;
                if (comp.Elapsed >= comp.FreshFor.TotalSeconds)
                {
                    comp.Withered = true;
                    dirty = true;
                }
                else if ((int) comp.Elapsed % 30 == 0)
                {
                    dirty = true;
                }
            }

            if (dirty)
                Dirty(uid, comp);
        }
    }

    /// <summary>Лежит ли в холоде (в любом из вложенных контейнеров).</summary>
    public bool InColdStorage(EntityUid uid)
    {
        var current = uid;
        for (var depth = 0; depth < 8; depth++)
        {
            if (!_container.TryGetContainingContainer((current, null, null), out var container))
                return false;

            if (HasComp<AntiRottingContainerComponent>(container.Owner))
                return true;

            current = container.Owner;
        }

        return false;
    }

    /// <summary>Можно ли ещё пришить эту часть тела.</summary>
    public bool IsViable(EntityUid uid)
    {
        return !TryComp<DHLimbDecayComponent>(uid, out var comp) || !comp.Withered;
    }

    private void OnExamined(Entity<DHLimbDecayComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Withered)
        {
            args.PushMarkup(Loc.GetString("dh-limb-examine-withered"));
            return;
        }

        var left = (int) MathF.Ceiling((float) (ent.Comp.FreshFor.TotalSeconds - ent.Comp.Elapsed) / 60f);
        args.PushMarkup(ent.Comp.Cold
            ? Loc.GetString("dh-limb-examine-cold", ("minutes", left))
            : Loc.GetString("dh-limb-examine-fresh", ("minutes", left)));
    }
}
