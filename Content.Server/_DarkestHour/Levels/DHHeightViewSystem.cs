using System.Numerics;
using Content.Shared._DarkestHour.Levels;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Levels;

/// <summary>
/// С высоты видно дальше (план осады п. 21, решение автора 2026-11-19): на этаже или крыше выше земли камера сама
/// отдаляется — +20% на 1-м уровне, +35% на 2-м и выше (уровни воздуха не считаются). Если зум уже поменяло что-то
/// другое (машина, пульт, бинокль) — не трогаем, пока оно не вернёт обычный.
/// </summary>
public sealed partial class DHHeightViewSystem : EntitySystem
{
    [Dependency] private SharedContentEyeSystem _contentEye = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DHZLevelSystem _zLevels = default!;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextUpdate;

    /// <summary>Кому какой зум выставлен из-за высоты.</summary>
    private readonly Dictionary<EntityUid, Vector2> _applied = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + Interval;

        var query = EntityQueryEnumerator<ActorComponent, ContentEyeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var eye, out var xform))
        {
            var factor = Factor(xform);
            var applied = _applied.TryGetValue(uid, out var was) ? was : (Vector2?) null;

            if (factor <= 1f)
            {
                // Спустился: вернуть обычный зум, если его никто не менял.
                if (applied is { } previous)
                {
                    _applied.Remove(uid);
                    if (eye.TargetZoom == previous)
                        _contentEye.ResetZoom(uid, eye);
                }

                continue;
            }

            var target = SharedContentEyeSystem.DefaultZoom * factor;
            if (applied == target)
                continue;

            // Зум сменило что-то другое — не перебиваем.
            if (eye.TargetZoom != SharedContentEyeSystem.DefaultZoom && eye.TargetZoom != applied)
                continue;

            _contentEye.SetZoom(uid, target, ignoreLimits: true, eye: eye);
            _eye.SetPvsScale(uid, factor);
            _applied[uid] = target;
        }

        // Убрать ушедших.
        var gone = new List<EntityUid>();
        foreach (var uid in _applied.Keys)
        {
            if (TerminatingOrDeleted(uid) || !HasComp<ActorComponent>(uid))
                gone.Add(uid);
        }

        foreach (var uid in gone)
        {
            _applied.Remove(uid);
        }
    }

    private float Factor(TransformComponent xform)
    {
        if (!_zLevels.TryGetLevel(xform.MapID, out var level) || level.Level <= 0 || _zLevels.IsAirLevel(xform.MapID))
            return 1f;

        return level.Level >= 2 ? 1.35f : 1.2f;
    }
}
