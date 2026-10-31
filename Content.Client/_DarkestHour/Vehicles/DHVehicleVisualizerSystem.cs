using Content.Shared._DarkestHour.Vehicles;
using Robust.Client.GameObjects;

namespace Content.Client._DarkestHour.Vehicles;

/// <summary>Башня техники смотрит за прицелом: слои башни поворачиваются на четверть оборота относительно корпуса.</summary>
public sealed partial class DHVehicleVisualizerSystem : VisualizerSystem<DHVehicleComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, DHVehicleComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<int>(uid, DHVehicleVisuals.TurretQuarter, out var quarter, args.Component))
            quarter = component.TurretQuarter;

        // Четверть 1 — башня повёрнута на 90° против часовой (юг → восток).
        var offset = (quarter & 3) switch
        {
            1 => SpriteComponent.DirectionOffset.CounterClockwise,
            2 => SpriteComponent.DirectionOffset.Flip,
            3 => SpriteComponent.DirectionOffset.Clockwise,
            _ => SpriteComponent.DirectionOffset.None,
        };

        foreach (var key in component.TurretLayers)
        {
            if (SpriteSystem.LayerMapTryGet((uid, args.Sprite), key, out var index, false))
                SpriteSystem.LayerSetDirOffset((uid, args.Sprite), index, offset);
        }
    }
}
