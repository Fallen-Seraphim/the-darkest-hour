using Content.Shared._DarkestHour.Hearing;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.Hearing;

/// <summary>Контузия своего персонажа: вспышка и муть в глазах (<see cref="DHConcussionComponent"/>).</summary>
public sealed partial class DHConcussionSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    private DHConcussionOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new DHConcussionOverlay();
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }
}

/// <summary>
/// Белая вспышка в первые доли секунды и «муть» (шейдер катаракты, как у повреждённых глаз), затухающая к концу.
/// Уважает настройки доступности: без мерцания — только мягкое затемнение по краям.
/// </summary>
public sealed partial class DHConcussionOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> CataractsShader = "Cataracts";
    private static readonly ProtoId<ShaderPrototype> CircleShader = "CircleMask";

    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ShaderInstance _cataracts;
    private readonly ShaderInstance _circle;

    /// <summary>Сколько держится белая вспышка (не дольше трети всего эффекта), с.</summary>
    private const float FlashSeconds = 1.2f;

    private float _blur;
    private float _flash;

    public DHConcussionOverlay()
    {
        IoCManager.InjectDependencies(this);
        _cataracts = _prototypes.Index(CataractsShader).InstanceUnique();
        _circle = _prototypes.Index(CircleShader).InstanceUnique();
        _circle.SetParameter("CircleMinDist", 0.0f);
        _circle.SetParameter("CirclePow", 0.2f);
        _circle.SetParameter("CircleMax", 8.0f);
        _circle.SetParameter("CircleMult", 0.75f);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local ||
            !_entities.TryGetComponent(local, out EyeComponent? eye) || args.Viewport.Eye != eye.Eye ||
            !_entities.TryGetComponent(local, out DHConcussionComponent? concussion))
        {
            return false;
        }

        var now = _timing.CurTime;
        var total = (float) (concussion.EndTime - concussion.StartTime).TotalSeconds;
        var elapsed = (float) (now - concussion.StartTime).TotalSeconds;
        if (total <= 0f || elapsed >= total || elapsed < 0f)
            return false;

        var progress = elapsed / total;
        _blur = concussion.Strength * MathF.Pow(1f - progress, 1.2f);

        var flashFor = MathF.Min(FlashSeconds, total / 3f);
        _flash = elapsed < flashFor ? concussion.Strength * 0.85f * (1f - elapsed / flashFor) : 0f;
        return _blur > 0.01f || _flash > 0.01f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        var bounds = args.WorldBounds;
        var zoom = _entities.TryGetComponent(_player.LocalEntity, out EyeComponent? eye) ? eye.Zoom.X : 1f;

        if (_blur > 0.01f)
        {
            if (_config.GetCVar(CCVars.DisableBlurryVision))
            {
                _circle.SetParameter("SCREEN_TEXTURE", ScreenTexture);
                _circle.SetParameter("Zoom", zoom);
                _circle.SetParameter("CircleRadius", 30f / MathF.Max(_blur, 0.05f));
                handle.UseShader(_circle);
                handle.DrawRect(bounds, Color.White);
            }
            else
            {
                _cataracts.SetParameter("SCREEN_TEXTURE", ScreenTexture);
                _cataracts.SetParameter("LIGHT_TEXTURE", args.Viewport.LightRenderTarget.Texture);
                _cataracts.SetParameter("Zoom", zoom);
                _cataracts.SetParameter("DistortionScalar", _blur * _blur);
                _cataracts.SetParameter("CloudinessScalar", _blur);
                handle.UseShader(_cataracts);
                handle.DrawRect(bounds, Color.White);
            }

            handle.UseShader(null);
        }

        // Вспышка: белая пелена, быстро сходящая. Без мерцания — приглушённая.
        if (_flash > 0.01f)
        {
            var alpha = _config.GetCVar(CCVars.DisableFlashEffect) ? _flash * 0.4f : _flash;
            handle.DrawRect(bounds, Color.White.WithAlpha(alpha));
        }
    }
}
