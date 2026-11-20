using System.Numerics;
using Content.Shared._DarkestHour.Effects;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._DarkestHour.Effects;

/// <summary>
/// Эффекты ЛТЦ на клиенте (план осады п. 8): дымный шлейф за летящими ловушками и «падение» с неба — ловушка
/// уменьшается до обычного размера, пока долетает до земли.
/// </summary>
public sealed partial class DHFlareEffectsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly List<(EntProtoId Proto, MapCoordinates At)> _puffs = new();

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _puffs.Clear();
        var trails = EntityQueryEnumerator<DHSmokeTrailComponent, TransformComponent>();
        while (trails.MoveNext(out _, out var trail, out var xform))
        {
            trail.Accumulator += frameTime;
            if (trail.Accumulator < trail.Interval || xform.MapUid == null)
                continue;

            trail.Accumulator = 0f;
            _puffs.Add((trail.Puff, _transform.GetMapCoordinates(xform)));
        }

        // Ставим после обхода: новые сущности во время перебора ломают перечисление.
        foreach (var (proto, at) in _puffs)
        {
            Spawn(proto, at);
        }

        var falling = EntityQueryEnumerator<DHFallingFlareComponent, SpriteComponent>();
        while (falling.MoveNext(out var uid, out var flare, out var sprite))
        {
            flare.Elapsed = MathF.Min(flare.FallTime, flare.Elapsed + frameTime);
            var t = flare.FallTime <= 0f ? 1f : flare.Elapsed / flare.FallTime;
            var scale = flare.StartScale + (1f - flare.StartScale) * t;
            _sprite.SetScale((uid, sprite), new Vector2(scale, scale));
        }
    }
}
