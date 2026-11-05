using Content.Shared._DarkestHour.Environment;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.Environment;

/// <summary>Свет карты поверхности по времени суток — каждый кадр, плавно.</summary>
public sealed partial class DHDayNightSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHDayNightCycleComponent, MapLightComponent>();
        while (query.MoveNext(out _, out var cycle, out var light))
        {
            light.AmbientLightColor = DHDayNight.GetColor(cycle, DHDayNight.Fraction(cycle, now));
        }
    }
}
