using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Assault;

/// <summary>
/// Эффекты ЛТЦ (план осады п. 8, решения автора 2026-11-20): в небе у машины ловушки разлетаются со шлейфом дыма,
/// на поверхности под ней — падают яркими точками и ещё 8–10 с горят на земле. Само действие ЛТЦ (срыв наведения
/// ПВО) — в системах челнока и штурмовика.
/// </summary>
public sealed partial class DHFlareEffectsServerSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const string AirProto = "DHFlareAir";
    private const string FallingProto = "DHFlareFalling";

    /// <summary>Залп у машины в небе: ловушки разлетаются от корпуса во все стороны.</summary>
    public void AirBurst(MapCoordinates center, int count = 6)
    {
        for (var i = 0; i < count; i++)
        {
            var direction = _random.NextAngle().ToVec();
            var flare = Spawn(AirProto, new MapCoordinates(center.Position + direction * 0.8f, center.MapId));
            _physics.SetLinearVelocity(flare, direction * _random.NextFloat(3f, 6f));
        }
    }

    /// <summary>Ловушки падают на поверхность вокруг точки под машиной.</summary>
    public void FallBelow(MapCoordinates center, int count = 6, float spread = 7f)
    {
        for (var i = 0; i < count; i++)
        {
            var offset = new Vector2(_random.NextFloat(-spread, spread), _random.NextFloat(-spread, spread));
            var flare = Spawn(FallingProto, new MapCoordinates(center.Position + offset, center.MapId));
            _physics.SetLinearVelocity(flare, _random.NextAngle().ToVec() * _random.NextFloat(0.3f, 1.2f));
        }
    }
}
