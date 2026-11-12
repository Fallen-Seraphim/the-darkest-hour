using System.Numerics;
using Content.Shared._DarkestHour.Levels;
using Content.Shared._DarkestHour.Squads;
using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Squads;

/// <summary>
/// Трекер командира отряда, как у морпехов CM (решение автора 2026-11-12): у каждого бойца отряда в алертах стрелка
/// цвета отряда, указывающая на командира отряда. Командира нет (не назначен, погиб и удалён, на другой карте) —
/// трекер погашен. Уровни одной Z-стопки считаются одной картой: стрелка показывает направление по горизонтали.
/// </summary>
public sealed partial class DHSquadSystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DHZLevelSystem _zLevels = default!;

    private static readonly ProtoId<AlertCategoryPrototype> TrackerCategory = "DHSquadTracker";

    private static readonly TimeSpan TrackerInterval = TimeSpan.FromSeconds(0.5);

    /// <summary>Ближе этого (в тайлах) — «рядом», без направления.</summary>
    private const float TrackerDirectRange = 1.5f;

    private TimeSpan _nextTracker;

    private readonly Dictionary<string, (EntityUid Map, int? Stack, Vector2 Position)> _leaders = new();

    private void ClearTracker(EntityUid uid)
    {
        _alerts.ClearAlertCategory(uid, TrackerCategory);
    }

    private (EntityUid Map, int? Stack, Vector2 Position)? Locate(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.MapUid is not { } map)
            return null;

        int? stack = _zLevels.TryGetLevel(map, out var level) ? level.Stack : null;
        return (map, stack, _transform.GetWorldPosition(xform));
    }

    private void UpdateTrackers()
    {
        var now = _timing.CurTime;
        if (now < _nextTracker)
            return;

        _nextTracker = now + TrackerInterval;

        // Где командиры отрядов.
        _leaders.Clear();
        var leaders = EntityQueryEnumerator<DHSquadMemberComponent>();
        while (leaders.MoveNext(out var uid, out var member))
        {
            if (member.Leader && Locate(uid) is { } at)
                _leaders[member.Squad.Id] = at;
        }

        var query = EntityQueryEnumerator<DHSquadMemberComponent, AlertsComponent>();
        while (query.MoveNext(out var uid, out var member, out var alerts))
        {
            if (member.Leader || !_proto.TryIndex(member.Squad, out var squad) || squad.TrackerAlert is not { } alert)
                continue;

            short severity = 0;
            if (_leaders.TryGetValue(member.Squad.Id, out var leader) && Locate(uid) is { } self &&
                (self.Map == leader.Map || self.Stack != null && self.Stack == leader.Stack))
            {
                severity = Direction(leader.Position - self.Position);
            }

            _alerts.ShowAlert((uid, alerts), alert, severity);
        }
    }

    /// <summary>
    /// Тяжесть алерта по направлению на командира: 1 — рядом, дальше по порядку иконок: юг, юго-восток, восток,
    /// северо-восток, север, северо-запад, запад, юго-запад.
    /// </summary>
    private static short Direction(Vector2 delta)
    {
        if (delta.Length() <= TrackerDirectRange)
            return 1;

        // Угол от юга против часовой стрелки (юг → восток → север → запад), по 45°.
        var angle = MathF.Atan2(delta.X, -delta.Y); // 0 — юг, π/2 — восток
        if (angle < 0)
            angle += MathF.Tau;

        var sector = (int) MathF.Round(angle / (MathF.PI / 4)) % 8;
        return (short) (2 + sector);
    }
}
