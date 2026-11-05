using System.Numerics;
using Content.Shared._DarkestHour.Movement;
using Robust.Client.GameObjects;
using DrawDepthTag = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._DarkestHour.Movement;

/// <summary>Прижавшийся к стене рисуется сдвинутым к ней (с «верхней» стороны — под стеной).</summary>
public sealed partial class DHWallLeanVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, (Vector2 Offset, int DrawDepth)> _base = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHWallLeaningComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<DHWallLeaningComponent> ent, ref ComponentShutdown args)
    {
        if (!_base.Remove(ent, out var original) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.SetOffset((ent, sprite), original.Offset);
        _sprite.SetDrawDepth((ent, sprite), original.DrawDepth);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<DHWallLeaningComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var leaning, out var sprite))
        {
            if (!_base.TryGetValue(uid, out var original))
            {
                original = (sprite.Offset, sprite.DrawDepth);
                _base[uid] = original;
            }

            if (leaning.Wall == null)
                continue;

            _sprite.SetOffset((uid, sprite), original.Offset + leaning.Offset);
            _sprite.SetDrawDepth((uid, sprite), leaning.BehindWall ? (int) DrawDepthTag.SmallMobs : original.DrawDepth);
        }
    }
}
