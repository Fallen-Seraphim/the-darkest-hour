using Content.Shared._DarkestHour.Explosion;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._DarkestHour.Explosion;

/// <summary>Слой следов от пуль поверх стены (порт BulletholeVisualizerSystem из RMC-14, MIT).</summary>
public sealed partial class DHBulletholeVisualizerSystem : VisualizerSystem<DHBulletholeComponent>
{
    private static readonly ResPath RsiPath = new("/Textures/_RMC14/Effects/bulletholes.rsi");

    protected override void OnAppearanceChange(EntityUid uid, DHBulletholeComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite ||
            !AppearanceSystem.TryGetData<string>(uid, DHBulletholeVisuals.State, out var state, args.Component))
        {
            return;
        }

        var ent = (uid, sprite);
        var layer = SpriteSystem.LayerMapReserve(ent, DHBulletholeLayers.Bullethole);
        var valid = !string.IsNullOrWhiteSpace(state);
        SpriteSystem.LayerSetVisible(ent, layer, valid);
        if (!valid)
            return;

        SpriteSystem.LayerSetSprite(ent, layer, new SpriteSpecifier.Rsi(RsiPath, state));
    }
}
