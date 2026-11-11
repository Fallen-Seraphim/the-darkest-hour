using Content.Shared._DarkestHour.Weight;
using Content.Shared.Alert;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Weight;

/// <summary>
/// Раз в секунду пересчитывает нагрузку существ (вес надетого и взятого, с поправками на разгрузку и экзоскелет) и
/// показывает алерт перегруза: 0 — тяжело (рывок дороже), 1 — рывка нет, 2 — полный перегруз.
/// </summary>
public sealed partial class DHEncumbranceSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DHWeightSystem _weight = default!;

    private static readonly ProtoId<AlertPrototype> LoadAlert = "DHLoad";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private TimeSpan _next;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _next)
            return;

        _next = now + Interval;

        var query = EntityQueryEnumerator<DHEncumbranceComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var (load, carried) = _weight.ComputeLoad(uid);
            _weight.SetLoad((uid, comp), load, carried);
            UpdateAlert((uid, comp));
        }
    }

    private void UpdateAlert(Entity<DHEncumbranceComponent> ent)
    {
        if (ent.Comp.Load <= ent.Comp.Free)
        {
            _alerts.ClearAlert(ent.Owner, LoadAlert);
            return;
        }

        short severity = ent.Comp.Load >= ent.Comp.Max ? (short) 2 : ent.Comp.NoSprint ? (short) 1 : (short) 0;
        _alerts.ShowAlert(ent.Owner, LoadAlert, severity);
    }
}
