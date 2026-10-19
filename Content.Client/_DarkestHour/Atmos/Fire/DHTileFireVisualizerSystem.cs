using Content.Shared._DarkestHour.Atmos.Fire;
using Robust.Client.GameObjects;

namespace Content.Client._DarkestHour.Atmos.Fire;

/// <summary>
/// Красит серое пламя (состояния dynamic_*) в цвет горящего реагента.
/// Уровень пламени переключает GenericVisualizer в прототипе.
/// </summary>
public sealed class DHTileFireVisualizerSystem : VisualizerSystem<DHTileFireComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, DHTileFireComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null ||
            !AppearanceSystem.TryGetData<Color>(uid, DHTileFireVisuals.Color, out var color, args.Component))
        {
            return;
        }

        SpriteSystem.LayerSetColor((uid, args.Sprite), DHTileFireLayers.Base, color);
    }
}
