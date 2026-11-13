using System.Numerics;
using Content.Shared._DarkestHour.CCVar;
using Content.Shared._DarkestHour.Levels;
using Robust.Shared.Configuration;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Levels;

/// <summary>
/// Видимость нижних Z-уровней: игроку на этаже (или в подземелье) с проёмами вокруг ставятся «зонды» на уровнях
/// ниже в той же точке — сервер шлёт ему и то, что там, а клиент рисует нижние уровни под своим.
/// </summary>
public sealed partial class DHZRenderSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ViewSubscriberSystem _view = default!;
    [Dependency] private DHZLevelSystem _z = default!;

    private const string ProbeProto = "DHZLevelProbe";

    /// <summary>Радиус поиска проёмов вокруг игрока и шаг выборки, тайлов (радиус — не меньше, чем у клиента).</summary>
    private const int OpeningRadius = 14;
    private const int OpeningStep = 2;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _next;

    private readonly Dictionary<EntityUid, (ICommonSession Session, Dictionary<int, EntityUid> Probes)> _viewers = new();
    private readonly List<(EntityUid Map, int Level)> _visible = new();
    private int _probeDepth;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, DHCVars.ZLevelsProbeDepth, value => _probeDepth = Math.Clamp(value, 0, 3), true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _next)
            return;

        _next = now + Interval;

        var seen = new HashSet<EntityUid>();
        var wanted = new List<(int Level, MapId Map)>();
        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var actor, out _))
        {
            seen.Add(uid);

            // Смотрит через камеру (кнопка взгляда) — нижние уровни под камерой.
            var view = TryComp<EyeComponent>(uid, out var eye) && eye.Target is { } target && Exists(target)
                ? target
                : uid;
            var at = _transform.GetMapCoordinates(view);
            Wanted(at, wanted);
            Sync(uid, actor.PlayerSession, at.Position, wanted);
        }

        foreach (var uid in new List<EntityUid>(_viewers.Keys))
        {
            if (!seen.Contains(uid))
                Clear(uid);
        }
    }

    /// <summary>
    /// Уровни, на которых нужен зонд: только реально видимые сквозь проёмы вокруг (как у клиента), не больше
    /// <c>dh.zlevels_probe_depth</c>. Раньше зонд ставился на каждый уровень ниже — с крыши 8-этажки это 8 полных
    /// областей видимости на игрока.
    /// </summary>
    private void Wanted(MapCoordinates at, List<(int Level, MapId Map)> wanted)
    {
        wanted.Clear();
        _z.VisibleLowerLevels(at, OpeningRadius, OpeningStep, _z.ViewDepth(at.MapId, _probeDepth), _visible);
        foreach (var (map, level) in _visible)
        {
            wanted.Add((level, Transform(map).MapID));
        }
    }

    private void Sync(EntityUid viewer, ICommonSession session, Vector2 position,
        List<(int Level, MapId Map)> wanted)
    {
        if (wanted.Count == 0)
        {
            Clear(viewer);
            return;
        }

        if (!_viewers.TryGetValue(viewer, out var entry) || entry.Session != session)
        {
            Clear(viewer);
            entry = (session, new Dictionary<int, EntityUid>());
            _viewers[viewer] = entry;
        }

        foreach (var (level, probe) in new Dictionary<int, EntityUid>(entry.Probes))
        {
            if (wanted.Exists(w => w.Level == level) && Exists(probe))
                continue;

            RemoveProbe(session, probe);
            entry.Probes.Remove(level);
        }

        foreach (var (level, map) in wanted)
        {
            var coords = new MapCoordinates(position, map);
            if (entry.Probes.TryGetValue(level, out var probe))
            {
                if (Transform(probe).MapID == map)
                {
                    _transform.SetMapCoordinates(probe, coords);
                    continue;
                }

                RemoveProbe(session, probe);
            }

            probe = Spawn(ProbeProto, coords);
            _view.AddViewSubscriber(probe, session);
            entry.Probes[level] = probe;
        }
    }

    private void Clear(EntityUid viewer)
    {
        if (!_viewers.Remove(viewer, out var entry))
            return;

        foreach (var probe in entry.Probes.Values)
        {
            RemoveProbe(entry.Session, probe);
        }
    }

    private void RemoveProbe(ICommonSession session, EntityUid probe)
    {
        if (!Exists(probe))
            return;

        _view.RemoveViewSubscriber(probe, session);
        if (!TerminatingOrDeleted(probe))
            QueueDel(probe);
    }
}
