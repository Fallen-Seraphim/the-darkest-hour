using System.Numerics;
using Content.Shared._DarkestHour.ThirdParty;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.ThirdParty;

/// <summary>
/// Метки M.A.R.S. (план осады п. 28): красный треугольник над целью и жёлтые над найденными сканом. Видят только
/// десантник и его ИИ.
/// </summary>
public sealed partial class DHMarsMarkOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    private DHMarsMarkOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new DHMarsMarkOverlay();
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }
}

public sealed partial class DHMarsMarkOverlay : Overlay
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly Color MarkColor = Color.FromHex("#ff2a2a");
    private static readonly Color ScanColor = Color.FromHex("#ffd23a");

    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public DHMarsMarkOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entities.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local)
            return;

        var me = _entities.GetNetEntity(local);
        var now = _timing.CurTime;
        var handle = args.WorldHandle;
        var pulse = 0.75f + 0.25f * MathF.Sin((float) now.TotalSeconds * 6f);
        Span<Vector2> tri = stackalloc Vector2[3];

        var query = _entities.EntityQueryEnumerator<DHMarsMarkComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mark, out var xform))
        {
            if (xform.MapID != args.MapId || now >= mark.Until || !mark.Viewers.Contains(me))
                continue;

            var pos = _transform.GetWorldPosition(xform) + new Vector2(0f, 0.85f);
            var size = mark.Scan ? 0.18f : 0.26f;
            tri[0] = pos + new Vector2(-size, size);
            tri[1] = pos + new Vector2(size, size);
            tri[2] = pos + new Vector2(0f, -size * 0.6f);

            var color = (mark.Scan ? ScanColor : MarkColor).WithAlpha(pulse);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, tri, color);
        }
    }
}
