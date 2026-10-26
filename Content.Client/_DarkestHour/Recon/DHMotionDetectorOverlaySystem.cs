using System.Numerics;
using Content.Shared._DarkestHour.Recon;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._DarkestHour.Recon;

/// <summary>Отметки детектора движения: мигают на месте движущихся и гаснут за полторы секунды.</summary>
public sealed class DHMotionDetectorOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;

    private DHMotionDetectorOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        var texture = _sprite.Frame0(new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Objects/Tools/motion_detector.rsi"),
            "detector_blip"));
        _overlay = new DHMotionDetectorOverlay(texture, _timing);
        _overlays.AddOverlay(_overlay);

        SubscribeNetworkEvent<DHMotionBlipsEvent>(OnBlips);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }

    private void OnBlips(DHMotionBlipsEvent ev)
    {
        _overlay.Blips.Clear();
        _overlay.Shown = _timing.RealTime;
        _overlay.Blips.AddRange(ev.Positions);
    }
}

public sealed class DHMotionDetectorOverlay(Texture blip, IGameTiming timing) : Overlay
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(1.5);

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public readonly List<Vector2> Blips = new();
    public TimeSpan Shown;

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Blips.Count > 0 && timing.RealTime - Shown < Lifetime;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var alpha = 1f - (float) ((timing.RealTime - Shown) / Lifetime);
        var color = Color.White.WithAlpha(Math.Clamp(alpha, 0f, 1f));
        foreach (var position in Blips)
        {
            args.WorldHandle.DrawTextureRect(blip, Box2.CenteredAround(position, new Vector2(1f, 1f)), color);
        }
    }
}
