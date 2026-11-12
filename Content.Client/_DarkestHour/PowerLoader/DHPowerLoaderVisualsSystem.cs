using Content.Shared._DarkestHour.PowerLoader;
using Content.Shared.Buckle.Components;
using Robust.Client.GameObjects;
using DrawDepthEnum = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._DarkestHour.PowerLoader;

/// <summary>Пустой погрузчик стоит среди людей; с пилотом его рама рисуется поверх пилота (как у LU-50 в RMC).</summary>
public sealed partial class DHPowerLoaderVisualsSystem : VisualizerSystem<DHPowerLoaderComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, DHPowerLoaderComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var strapped = AppearanceSystem.TryGetData<bool>(uid, StrapVisuals.State, out var state, args.Component) && state;
        SpriteSystem.SetDrawDepth((uid, args.Sprite), strapped ? (int) DrawDepthEnum.OverMobs : (int) DrawDepthEnum.Mobs);
    }
}
