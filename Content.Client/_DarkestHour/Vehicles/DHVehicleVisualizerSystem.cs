using System.Numerics;
using Content.Shared._DarkestHour.Vehicles;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._DarkestHour.Vehicles;

/// <summary>
/// Башня техники за прицелом наводчика, как в RMC-14: берётся кадр башни того направления (из четырёх), куда она
/// смотрит, и доворачивается на остаток угла (не больше 45°) вокруг своей оси. Так спрайт почти не искажается
/// поворотом. Кадр башни другого направления нарисован для корпуса, смотрящего туда же, поэтому башню ещё сдвигаем
/// с её места в том кадре (<see cref="DHVehicleComponent.TurretPivots"/>) на место в кадре корпуса.
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

            // Кадр корпуса — как его выбирает движок (с учётом поворота камеры).
            var hullDir = (_transform.GetWorldRotation(uid) + eyeRotation).GetCardinalDir();
            var hullAngle = hullDir.ToAngle();

            // Куда смотрит башня на экране, ближайшее из четырёх направлений и остаток.
            var turretAngle = hullAngle + vehicle.TurretAngle;
            var turretDir = turretAngle.GetCardinalDir();
            var residual = (turretAngle - turretDir.ToAngle()).Reduced();
            if (residual.Theta > Math.PI)
                residual = new Angle(residual.Theta - 2 * Math.PI);

            var quarters = (Quarter(turretDir) - Quarter(hullDir) + 4) % 4;
            var dirOffset = quarters switch
            {
                1 => SpriteComponent.DirectionOffset.CounterClockwise,
                2 => SpriteComponent.DirectionOffset.Flip,
                3 => SpriteComponent.DirectionOffset.Clockwise,
                _ => SpriteComponent.DirectionOffset.None,
            };

            var hullPivot = PivotFor(vehicle, hullDir);
            var turretPivot = PivotFor(vehicle, turretDir);
            // Поворот слоя идёт вокруг центра спрайта: ось башни из кадра башни уводим на место оси в кадре корпуса.
            var offset = hullPivot - residual.RotateVec(turretPivot);

            foreach (var key in vehicle.TurretLayers)
            {
                if (!_sprite.LayerMapTryGet((uid, sprite), key, out var index, false))
                    continue;

                _sprite.LayerSetDirOffset((uid, sprite), index, dirOffset);
                _sprite.LayerSetRotation((uid, sprite), index, residual);
                _sprite.LayerSetOffset((uid, sprite), index, offset);
            }
        }
    }

    /// <summary>Четверти оборота против часовой от юга: юг 0, восток 1, север 2, запад 3.</summary>
    private static int Quarter(Direction dir)
    {
        return dir switch
        {
            Direction.East => 1,
            Direction.North => 2,
            Direction.West => 3,
            _ => 0,
        };
    }

    private static Vector2 PivotFor(DHVehicleComponent vehicle, Direction dir)
    {
        var index = dir switch
        {
            Direction.North => 1,
            Direction.East => 2,
            Direction.West => 3,
            _ => 0,
        };

        return index < vehicle.TurretPivots.Count ? vehicle.TurretPivots[index] : Vector2.Zero;
    }
}
