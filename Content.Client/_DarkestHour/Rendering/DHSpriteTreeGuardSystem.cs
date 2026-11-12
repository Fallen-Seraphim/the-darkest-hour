using System.Numerics;
using Content.Shared.Mobs.Components;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.Rendering;

/// <summary>
/// «Невидимые» мобы. Движок (ComponentTreeSystem.UpdateTreePositions) при обработке очереди дерева спрайтов
/// натыкается на сущность, ушедшую на грид/карту без дерева (удаляемый грид, исчезнувший шаттл, самолёт и т. п.),
/// делает <c>return</c> и очищает всю очередь — остальные сущности, сдвинувшиеся в тот же кадр, остаются в дереве
/// на старом месте. Спрайт рисуется только если его запись в дереве попадает в экран, поэтому стоящий моб пропадает,
/// стоит отойти от места, где запись «застряла» (видели на мостике авианосца). Здесь раз в 0,2 с мобы вокруг глаза
/// проверяются: если запись дерева не на месте, она переставляется — по одной, чтобы сбой не задел остальные.
/// </summary>
public sealed partial class DHSpriteTreeGuardSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SpriteTreeSystem _spriteTree = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(0.2);

    /// <summary>Радиус проверки вокруг глаза, тайлов (с запасом на широкий экран и зум).</summary>
    private const float Range = 24f;

    private TimeSpan _next;
    private TimeSpan _loggedAt;
    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private readonly List<Entity<SpriteComponent, TransformComponent>> _hits = new();

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(SpriteTreeSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        var now = _timing.RealTime;
        if (now < _next)
            return;

        _next = now + Interval;
        var eye = _eye.CurrentEye.Position;
        if (eye.MapId == MapId.Nullspace)
            return;

        _mobs.Clear();
        _lookup.GetEntitiesInRange(eye, Range, _mobs);
        var fixedCount = 0;
        foreach (var mob in _mobs)
        {
            if (Check(mob.Owner))
                fixedCount++;
        }

        _mobs.Clear();
        if (fixedCount == 0 || now < _loggedAt + TimeSpan.FromSeconds(10))
            return;

        _loggedAt = now;
        Log.Debug($"Переставлено застрявших в дереве спрайтов мобов: {fixedCount}.");
    }

    /// <returns>true — запись была не на месте и переставлена.</returns>
    private bool Check(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) || !sprite.AddToTree || sprite.TreeUpdateQueued)
            return false;

        var xform = Transform(uid);
        if (xform.MapID == MapId.Nullspace || (xform.GridUid ?? xform.MapUid) is not { } treeUid)
            return false;

        // Дерева у грида/карты нет (а должно быть всегда) — это и ломает очередь; вернуть.
        if (!HasComp<SpriteTreeComponent>(treeUid))
        {
            if (TerminatingOrDeleted(treeUid))
                return false;

            EnsureComp<SpriteTreeComponent>(treeUid);
        }
        else if (sprite.TreeUid == treeUid)
        {
            _hits.Clear();
            var pos = _transform.GetWorldPosition(xform);
            _spriteTree.QueryAabb(_hits, xform.MapID, Box2.CenteredAround(pos, new Vector2(0.25f)));
            foreach (var hit in _hits)
            {
                if (hit.Owner == uid)
                {
                    _hits.Clear();
                    return false;
                }
            }

            _hits.Clear();
        }

        _spriteTree.QueueTreeUpdate(uid, sprite, xform);
        _spriteTree.UpdateTreePositions();
        return true;
    }
}
