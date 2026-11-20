// Перенесено из RussianCM (CMU14, github.com/flex5hybrid/RussianCM, AGPL v3): DropshipTacticalHoverVisualsSystem.
using Content.Shared._DarkestHour.Assault;
using Robust.Client.Graphics;

namespace Content.Client._DarkestHour.Assault;

/// <summary>Оверлей тени и струй челнока над полем боя.</summary>
public sealed partial class DHDropshipHoverVisualsSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    private DHDropshipHoverOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new DHDropshipHoverOverlay(EntityManager);
        _overlays.AddOverlay(_overlay);
        SubscribeLocalEvent<DHDropshipHoverShadowComponent, ComponentShutdown>(OnShadowShutdown);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay(_overlay);
        base.Shutdown();
    }

    private void OnShadowShutdown(Entity<DHDropshipHoverShadowComponent> ent, ref ComponentShutdown args)
    {
        _overlay.Invalidate(ent.Owner);
    }
}
