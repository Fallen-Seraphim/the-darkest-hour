using Content.Shared._DarkestHour.Vehicles;
using Robust.Client.GameObjects;

namespace Content.Client._DarkestHour.Vehicles;

/// <summary>Слои модулей на машине: стоит — свой спрайт (целый или разбитый), снят — слоя не видно.</summary>
public sealed partial class DHVehicleHardpointVisualizerSystem : VisualizerSystem<DHVehicleHardpointsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, DHVehicleHardpointsComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        foreach (var (slot, layer) in component.Slots)
        {
            if (string.IsNullOrEmpty(layer) ||
                !AppearanceSystem.TryGetData<string>(uid, DHHardpoints.Visual(slot), out var state, args.Component) ||
                !SpriteSystem.LayerMapTryGet((uid, sprite), layer, out var index, false))
            {
                continue;
            }

            if (string.IsNullOrEmpty(state))
            {
                SpriteSystem.LayerSetVisible((uid, sprite), index, false);
                continue;
            }

            SpriteSystem.LayerSetVisible((uid, sprite), index, true);
            SpriteSystem.LayerSetRsiState((uid, sprite), index, state);
        }
    }
}
