using Content.Shared._DarkestHour.Shuttles.Rift;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._DarkestHour.Shuttles.Rift;

/// <summary>
/// Пока корабль игрока идёт сквозь разрыв: тень по краям экрана (дышит), при разрядах молний у корпуса — вспышка.
/// </summary>
public sealed class DHRiftOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private DHRiftOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        var texture = _sprite.Frame0(new SpriteSpecifier.Rsi(new ResPath("/Textures/_DarkestHour/Effects/rift.rsi"),
            "vignette"));
        _overlay = new DHRiftOverlay(texture);
        _overlays.AddOverlay(_overlay);

        SubscribeNetworkEvent<DHRiftThunderEvent>(OnThunder);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }

    private void OnThunder(DHRiftThunderEvent ev)
    {
        _overlay.Flash = MathF.Min(1f, MathF.Max(_overlay.Flash, ev.Strength));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var inRift = _player.LocalEntity is { } local &&
                     Transform(local).GridUid is { } grid &&
                     HasComp<DHRiftTransitComponent>(grid);

        // Тень наползает за ~1.5 с и так же отступает после выхода из разрыва.
        var target = inRift ? 1f : 0f;
        _overlay.Strength += Math.Clamp(target - _overlay.Strength, -frameTime / 1.5f, frameTime / 1.5f);
        _overlay.Flash = MathF.Max(0f, _overlay.Flash - frameTime * 2.2f);
        _overlay.Time += frameTime;
    }
}

public sealed class DHRiftOverlay : Overlay
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private static readonly Color Shadow = Color.FromHex("#07020d");
    private static readonly Color FlashColor = Color.FromHex("#d9b8ff");

    private readonly Texture _vignette;

    /// <summary>0..1 — насколько корабль «в разрыве» (плавно).</summary>
    public float Strength;

    /// <summary>0..1 — вспышка молнии, гаснет сама.</summary>
    public float Flash;

    public float Time;

    public DHRiftOverlay(Texture vignette)
    {
        IoCManager.InjectDependencies(this);
        _vignette = vignette;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (Strength <= 0f && Flash <= 0f)
            return false;

        return _entities.TryGetComponent(_player.LocalEntity, out EyeComponent? eye) && args.Viewport.Eye == eye.Eye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var pulse = 0.82f + 0.18f * MathF.Sin(Time * 1.7f);

        // Тень по краям: двойной слой даёт плотный край и мягкий переход к центру.
        var shadow = Shadow.WithAlpha(Strength * pulse * (1f - Flash * 0.6f));
        handle.DrawTextureRect(_vignette, args.WorldBounds, shadow);
        handle.DrawTextureRect(_vignette, args.WorldBounds, shadow.WithAlpha(shadow.A * 0.6f));

        // Общая лёгкая фиолетовая дымка.
        handle.DrawRect(args.WorldBounds, Color.FromHex("#2a0a45").WithAlpha(0.12f * Strength));

        if (Flash > 0f)
        {
            handle.DrawRect(args.WorldBounds, FlashColor.WithAlpha(Flash * 0.28f));
            handle.DrawTextureRect(_vignette, args.WorldBounds, FlashColor.WithAlpha(Flash * 0.35f));
        }
    }
}
