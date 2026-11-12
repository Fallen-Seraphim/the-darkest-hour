using System.Linq;
using System.Numerics;
using Content.Shared._DarkestHour.Vehicles;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;

namespace Content.Client._DarkestHour.Vehicles;

/// <summary>
/// Спрайт техники, как в RMC-14 / CMU (VehicleExactCardinalDirectionSystem). Корпус поворачивается плавно, а кадров
/// у спрайта четыре: берём кадр ближайшего направления и доворачиваем его на остаток угла. Боковые (восток, запад)
/// кадры — только в секторе ±22,5° от своей стороны, всё остальное — север или юг: машина длинная, и повёрнутый
/// боковой кадр смотрится хуже продольного.
/// Башня за прицелом наводчика: берётся кадр башни того направления, куда она смотрит относительно кадра корпуса, и
/// доворачивается на остаток (не больше 45°) вокруг своей оси. Кадр башни другого направления нарисован для корпуса,
/// смотрящего туда же, поэтому башню ещё сдвигаем с её места в том кадре (<see cref="DHVehicleComponent.TurretPivots"/>)
/// на место в кадре корпуса.
/// </summary>
public sealed partial class DHVehicleVisualizerSystem : EntitySystem
{
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const double EighthPi = Math.PI / 8d;
    private const double BoundaryEpsilon = 1e-12;

    private readonly HashSet<int> _turretIndices = new();

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var eyeRotation = _eyeManager.CurrentEye.Rotation;
        var query = EntityQueryEnumerator<DHVehicleComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var vehicle, out var sprite))
        {
            // Кадр корпуса — по сектору (с учётом поворота камеры); остаток угла даёт сам поворот сущности.
            var hullDir = GetRenderAlignedCardinalDir(_transform.GetWorldRotation(uid) + eyeRotation);
            var hullAngle = hullDir.ToAngle();

            sprite.EnableDirectionOverride = true;
            sprite.DirectionOverride = hullDir;
            // Движок считает поворот кадра от своего направления, а не от заданного: выключаем это и поворачиваем
            // каждый четырёхсторонний слой сами.
            sprite.NoRotation = true;
            _sprite.SetGranularLayersRendering((uid, sprite), true);

            _turretIndices.Clear();
            foreach (var key in vehicle.TurretLayers)
            {
                if (_sprite.LayerMapTryGet((uid, sprite), key, out var index, false))
                    _turretIndices.Add(index);
            }

            for (var i = 0; i < sprite.AllLayers.Count(); i++)
            {
                _sprite.LayerSetRenderingStrategy((uid, sprite), i, LayerRenderingStrategy.Default);
                if (_turretIndices.Contains(i))
                    continue;

                var layer = sprite[i];
                if (layer.ActualRsi is not { } rsi || !rsi.TryGetState(layer.RsiState, out var state) ||
                    state.RsiDirections != RsiDirectionType.Dir4)
                {
                    continue;
                }

                _sprite.LayerSetRotation((uid, sprite), i, -hullAngle);
            }

            if (_turretIndices.Count == 0)
                continue;

            // Куда смотрит башня в кадре корпуса, ближайшее из четырёх направлений и остаток.
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
            // Смещение слоя поворачивается вместе с сущностью, поэтому из кадра корпуса переводим его в её оси.
            var offset = (-hullAngle).RotateVec(hullPivot - residual.RotateVec(turretPivot));

            foreach (var index in _turretIndices)
            {
                _sprite.LayerSetDirOffset((uid, sprite), index, dirOffset);
                _sprite.LayerSetRotation((uid, sprite), index, residual - hullAngle);
                _sprite.LayerSetOffset((uid, sprite), index, offset);
            }
        }
    }

    /// <summary>
    /// Восток и запад — по 45° (±22,5° от своей стороны), остальное — север и юг (RMC
    /// VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir).
    /// </summary>
    public static Direction GetRenderAlignedCardinalDir(Angle facing)
    {
        var theta = facing.Reduced().FlipPositive().Theta;

        if (theta >= EighthPi * 3 - BoundaryEpsilon && theta <= EighthPi * 5 + BoundaryEpsilon)
            return Direction.East;

        if (theta >= EighthPi * 11 - BoundaryEpsilon && theta <= EighthPi * 13 + BoundaryEpsilon)
            return Direction.West;

        if (theta > EighthPi * 5 && theta < EighthPi * 11)
            return Direction.North;

        return Direction.South;
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
