using Content.Shared._DarkestHour.Medical.IV;
using Content.Shared.Rounding;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client._DarkestHour.Medical.IV;

/// <summary>DarkestHour: вид капельницы и пакета крови, трубки к пациенту (порт RMC-14).</summary>
public sealed partial class DHIVDripSystem : SharedDHIVDripSystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        if (!_overlay.HasOverlay<DHIVDripOverlay>())
            _overlay.AddOverlay(new DHIVDripOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<DHIVDripOverlay>();
    }

    protected override void UpdateIVAppearance(Entity<DHIVDripComponent> iv)
    {
        base.UpdateIVAppearance(iv);
        if (!TryComp(iv, out SpriteComponent? sprite))
            return;

        _sprite.LayerSetRsiState((iv.Owner, sprite), DHIVDripVisualLayers.Base,
            iv.Comp.AttachedTo == null ? iv.Comp.UnattachedState : iv.Comp.AttachedState);

        string? reagentState = null;
        for (var i = iv.Comp.ReagentStates.Count - 1; i >= 0; i--)
        {
            var (amount, state) = iv.Comp.ReagentStates[i];
            if (amount <= iv.Comp.FillPercentage)
            {
                reagentState = state;
                break;
            }
        }

        if (reagentState == null)
        {
            _sprite.LayerSetVisible((iv.Owner, sprite), DHIVDripVisualLayers.Reagent, false);
            return;
        }

        _sprite.LayerSetVisible((iv.Owner, sprite), DHIVDripVisualLayers.Reagent, true);
        _sprite.LayerSetRsiState((iv.Owner, sprite), DHIVDripVisualLayers.Reagent, reagentState);
        _sprite.LayerSetColor((iv.Owner, sprite), DHIVDripVisualLayers.Reagent, iv.Comp.FillColor);
    }

    protected override void UpdatePackAppearance(Entity<DHBloodPackComponent> pack)
    {
        base.UpdatePackAppearance(pack);
        if (!TryComp(pack, out SpriteComponent? sprite))
            return;

        _sprite.LayerSetVisible((pack.Owner, sprite), DHBloodPackVisuals.Label, false);

        if (!_sprite.LayerMapTryGet((pack.Owner, sprite), DHBloodPackVisuals.Fill, out var fillLayer, false))
            return;

        var fill = pack.Comp.FillPercentage.Float();
        var level = ContentHelpers.RoundToLevels(fill, 1, pack.Comp.MaxFillLevels + 1);
        var state = level > 0 ? $"{pack.Comp.FillBaseName}{level}" : pack.Comp.FillBaseName;
        _sprite.LayerSetRsiState((pack.Owner, sprite), fillLayer, state);
        _sprite.LayerSetColor((pack.Owner, sprite), fillLayer, pack.Comp.FillColor);
        _sprite.LayerSetVisible((pack.Owner, sprite), fillLayer, level > 0);
    }
}

/// <summary>Трубки от капельниц и пакетов к пациентам.</summary>
public sealed partial class DHIVDripOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public DHIVDripOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var transform = _entity.System<TransformSystem>();
        var handle = args.WorldHandle;

        var ivs = _entity.EntityQueryEnumerator<DHIVDripComponent>();
        while (ivs.MoveNext(out var uid, out var comp))
        {
            if (comp.AttachedTo is { Valid: true } attached)
                DrawTube(transform, handle, uid, attached, args.MapId);
        }

        var packs = _entity.EntityQueryEnumerator<DHBloodPackComponent>();
        while (packs.MoveNext(out var uid, out var comp))
        {
            if (comp.AttachedTo is { Valid: true } attached)
                DrawTube(transform, handle, uid, attached, args.MapId);
        }
    }

    private static void DrawTube(TransformSystem transform, DrawingHandleWorld handle, EntityUid from, EntityUid to,
        MapId map)
    {
        var a = transform.GetMapCoordinates(from);
        var b = transform.GetMapCoordinates(to);
        if (a.MapId != map || b.MapId != map)
            return;

        handle.DrawLine(a.Position, b.Position, Color.White);
    }
}
