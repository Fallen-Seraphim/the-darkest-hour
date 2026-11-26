using Content.Shared._DarkestHour.Vehicles;
using Robust.Client.GameObjects;

namespace Content.Client._DarkestHour.Vehicles;

/// <summary>
/// Слои повреждений техники, как VehicleFrameDamageVisualizerSystem в RMC-14: ниже порога прочности слой виден, и чем
/// меньше прочности, тем он плотнее.
/// </summary>
public sealed partial class DHVehicleDamageVisualizerSystem : VisualizerSystem<DHVehicleDamageVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, DHVehicleDamageVisualsComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        if (!AppearanceSystem.TryGetData<float>(uid, DHVehicleDamageVisuals.Hull, out var hull, args.Component))
            hull = 1f;

        foreach (var layer in component.Hull)
        {
            Apply((uid, sprite), component, layer, hull);
        }

        foreach (var (slot, layer) in component.Hardpoints)
        {
            // Модуль снят (или слоя модуля нет) — нет и его повреждений.
            var fitted = AppearanceSystem.TryGetData<string>(uid, DHHardpoints.Visual(slot), out var state, args.Component) &&
                         !string.IsNullOrEmpty(state);
            if (!fitted ||
                !AppearanceSystem.TryGetData<float>(uid, DHVehicleDamage.Visual(slot), out var fraction, args.Component))
            {
                fraction = 1f;
            }

            Apply((uid, sprite), component, layer, fraction);
        }
    }

    private void Apply(Entity<SpriteComponent> ent, DHVehicleDamageVisualsComponent component, string layer, float fraction)
    {
        if (!SpriteSystem.LayerMapTryGet(ent.AsNullable(), layer, out var index, false))
            return;

        if (fraction >= component.ShowThreshold)
        {
            SpriteSystem.LayerSetVisible(ent.AsNullable(), index, false);
            return;
        }

        var t = Math.Clamp(fraction / component.ShowThreshold, 0f, 1f);
        var alpha = component.MinAlpha + (1f - component.MinAlpha) * (1f - t);
        SpriteSystem.LayerSetVisible(ent.AsNullable(), index, true);
        SpriteSystem.LayerSetColor(ent.AsNullable(), index, Color.White.WithAlpha(alpha));
    }
}
