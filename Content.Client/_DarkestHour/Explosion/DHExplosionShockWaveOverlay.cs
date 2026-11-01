using System.Numerics;
using Content.Shared._DarkestHour.Explosion;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._DarkestHour.Explosion;

/// <summary>Ударные волны взрывов (порт RMCExplosionShockWaveOverlay из RMC-14, MIT).</summary>
public sealed class DHExplosionShockWaveOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private SharedTransformSystem? _xformSystem;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;

    /// <summary>Сколько волн одновременно может быть на экране.</summary>
    public const int MaxCount = 10;

    private readonly Vector2[] _positions = new Vector2[MaxCount];
    private readonly float[] _falloffPower = new float[MaxCount];
    private readonly float[] _sharpness = new float[MaxCount];
    private readonly float[] _width = new float[MaxCount];
    private int _count;

    public DHExplosionShockWaveOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index<ShaderPrototype>("RMCShockWave").Instance().Duplicate();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye == null || _xformSystem is null && !_entMan.TrySystem(out _xformSystem))
            return false;

        var query = _entMan.EntityQueryEnumerator<DHExplosionShockWaveComponent, TransformComponent>();
        _count = 0;
        while (query.MoveNext(out var uid, out var wave, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var mapPos = _xformSystem!.GetWorldPosition(uid);
            var local = args.Viewport.WorldToLocal(mapPos);

            // Нормированные координаты 0–1; во фрагментном шейдере ось Y идёт снизу.
            local.Y = 1 - local.Y / args.Viewport.Size.Y;
            local.X /= args.Viewport.Size.X;

            _positions[_count] = local;
            _falloffPower[_count] = wave.FalloffPower;
            _sharpness[_count] = wave.Sharpness;
            _width[_count] = wave.Width;
            _count++;

            if (_count == MaxCount)
                break;
        }

        return _count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye == null)
            return;

        _shader.SetParameter("renderScale", args.Viewport.RenderScale * args.Viewport.Eye.Scale);
        _shader.SetParameter("count", _count);
        _shader.SetParameter("position", _positions);
        _shader.SetParameter("falloffPower", _falloffPower);
        _shader.SetParameter("sharpness", _sharpness);
        _shader.SetParameter("width", _width);
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);

        var worldHandle = args.WorldHandle;
        worldHandle.UseShader(_shader);
        worldHandle.DrawRect(args.WorldBounds, Color.White);
        worldHandle.UseShader(null);
    }
}

public sealed partial class DHExplosionShockWaveSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new DHExplosionShockWaveOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<DHExplosionShockWaveOverlay>();
    }
}
