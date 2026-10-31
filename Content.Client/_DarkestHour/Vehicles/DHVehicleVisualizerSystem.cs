using System.Numerics;
using Content.Shared._DarkestHour.Vehicles;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._DarkestHour.Vehicles;

/// <summary>
/// Башня техники плавно поворачивается за прицелом: слои башни берут то же направление спрайта, что и корпус,
/// и вращаются на угол башни вокруг её оси (у каждого направления корпуса ось своя — <see cref="DHVehicleComponent.TurretPivots"/>).
/// </summary>
public sealed partial class DHVehicleVisualizerSystem : EntitySystem
{
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var eyeRotation = _eyeManager.CurrentEye.Rotation;
        var query = EntityQueryEnumerator<DHVehicleComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var vehicle, out var sprite))
        {
            if (vehicle.TurretLayers.Count == 0)
                continue;

            // Направление спрайта корпуса — как его выбирает движок (с учётом поворота камеры).
            var dir = (_transform.GetWorldRotation(uid) + eyeRotation).GetCardinalDir();
            var pivot = PivotFor(vehicle, dir);

            var rotation = vehicle.TurretAngle;
            // Слой вращается вокруг центра спрайта; сдвигаем так, чтобы осью была ось башни.
            var offset = pivot - rotation.RotateVec(pivot);

            foreach (var key in vehicle.TurretLayers)
            {
                if (!_sprite.LayerMapTryGet((uid, sprite), key, out var index, false))
                    continue;

                _sprite.LayerSetRotation((uid, sprite), index, rotation);
                _sprite.LayerSetOffset((uid, sprite), index, offset);
            }
        }
    }

    private static Vector2 PivotFor(DHVehicleComponent vehicle, Direction dir)
    {
        var index = dir switch
        {
            Direction.South => 0,
            Direction.North => 1,
            Direction.East => 2,
            Direction.West => 3,
            _ => 0,
        };

        return index < vehicle.TurretPivots.Count ? vehicle.TurretPivots[index] : Vector2.Zero;
    }
}
