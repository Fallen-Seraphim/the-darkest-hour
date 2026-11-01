using Content.Server.Decals;
using Content.Shared.Explosion;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Explosion.EntitySystems;

/// <summary>
/// DarkestHour (решение автора 2026-11-01): эффекты взрыва как в RMC-14. Ванильные огненные клетки скрыты
/// (<c>fireColor: Transparent</c> у типов взрывов), вместо них — спрайт разрыва (масштаб по силе взрыва), ударная
/// волна, разлёт искр-осколков и подпалина на земле. Звуки — CM-SS13 (в прототипах типов взрывов).
/// </summary>
public sealed partial class ExplosionSystem
{
    [Dependency] private DecalSystem _dhDecals = default!;

    private static readonly EntProtoId[] DHBlastEffect =
    {
        "DHExplosionEffectBlast1", "DHExplosionEffectBlast2", "DHExplosionEffectBlast3", "DHExplosionEffectBlast4",
    };
    private static readonly EntProtoId DHShockWaveSmall = "DHExplosionEffectShockWave";
    private static readonly EntProtoId DHShockWaveLarge = "DHExplosionEffectShockWaveLarge";
    private static readonly EntProtoId[] DHShrapnel = { "DHExplosionEffectShrapnel1", "DHExplosionEffectShrapnel2" };
    private static readonly string[] DHScorch = { "RMCScorch1", "RMCScorch2", "RMCScorch3", "RMCScorch4" };
    private static readonly string[] DHScorchSmall = { "RMCScorchSmall1", "RMCScorchSmall2", "RMCScorchSmall3" };

    private void DHSpawnExplosionEffects(MapCoordinates epicenter, ExplosionPrototype type, float totalIntensity)
    {
        if (epicenter.MapId == MapId.Nullspace)
            return;

        // Ступень по силе: граната ~ до 100, снаряд/мина ~ до 600, крупный боеприпас ~ до 2000, больше — бомба.
        var size = totalIntensity switch
        {
            < 100f => 1,
            < 600f => 2,
            < 2000f => 3,
            _ => 4,
        };

        Spawn(DHBlastEffect[size - 1], epicenter);

        Spawn(size >= 3 ? DHShockWaveLarge : DHShockWaveSmall, epicenter);

        var shrapnel = _robustRandom.Next(4, 8) * size;
        for (var i = 0; i < shrapnel; i++)
        {
            var spark = Spawn(_robustRandom.Pick(DHShrapnel), epicenter);
            var direction = _robustRandom.NextAngle().ToVec() * (1.5f + size);
            _throwingSystem.TryThrow(spark, direction, 6f + 2f * size, recoil: false, compensateFriction: false,
                playSound: false, doSpin: false);
        }

        // Подпалина на земле.
        if (_map.TryFindGridAt(epicenter, out var gridUid, out _))
        {
            var coords = _transformSystem.ToCoordinates(gridUid, epicenter);
            var decal = _robustRandom.Pick(size >= 2 ? DHScorch : DHScorchSmall);
            _dhDecals.TryAddDecal(decal, coords.Offset(new System.Numerics.Vector2(-0.5f, -0.5f)), out _,
                rotation: _robustRandom.NextAngle(), cleanable: true);
        }
    }
}
