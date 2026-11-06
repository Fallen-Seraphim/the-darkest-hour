using System.Numerics;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._DarkestHour.Levels;

/// <summary>
/// Общие правила Z-уровней: какая карта на каком уровне, есть ли пол в точке, крыша над головой (свой флаг крыши
/// грида или пол уровнем выше), куда падает снаряд сверху (на самый верхний пол в точке), выстрелы между уровнями.
/// </summary>
public sealed partial class DHZLevelSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedRoofSystem _roof = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>Насколько дальше края выносится пуля, вылетающая на другом уровне (чтобы не бить в стену под ногами).</summary>
    private const float CrossLevelMuzzle = 1.2f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHZViewerComponent, DHCrossLevelShotEvent>(OnCrossLevelShot);
    }

    #region Стопка

    public bool TryGetLevel(EntityUid? map, out DHZLevelComponent level)
    {
        level = default!;
        if (map == null || !TryComp(map, out DHZLevelComponent? comp))
            return false;

        level = comp;
        return true;
    }

    public bool TryGetLevel(MapId mapId, out DHZLevelComponent level)
    {
        return TryGetLevel(_map.GetMapOrInvalid(mapId), out level);
    }

    /// <summary>Карта уровня <paramref name="level"/> стопки <paramref name="stack"/>.</summary>
    public EntityUid? LevelMap(int stack, int level)
    {
        var query = EntityQueryEnumerator<DHZLevelComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Stack == stack && comp.Level == level)
                return uid;
        }

        return null;
    }

    /// <summary>Все уровни стопки по возрастанию.</summary>
    public List<(EntityUid Map, int Level)> Levels(int stack)
    {
        var result = new List<(EntityUid, int)>();
        var query = EntityQueryEnumerator<DHZLevelComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Stack == stack)
                result.Add((uid, comp.Level));
        }

        result.Sort((a, b) => a.Item2.CompareTo(b.Item2));
        return result;
    }

    /// <summary>Свободный номер для новой стопки.</summary>
    public int NewStack()
    {
        var max = 0;
        var query = EntityQueryEnumerator<DHZLevelComponent>();
        while (query.MoveNext(out var comp))
        {
            max = Math.Max(max, comp.Stack);
        }

        return max + 1;
    }

    public MapCoordinates? OnLevel(MapCoordinates coords, int level)
    {
        if (!TryGetLevel(coords.MapId, out var here) || LevelMap(here.Stack, level) is not { } map)
            return null;

        return new MapCoordinates(coords.Position, Transform(map).MapID);
    }

    /// <summary>
    /// Какие нижние уровни на самом деле видны вокруг точки (оптимизация, решения автора 2026-11-06): берутся клетки
    /// без пола на своём уровне в радиусе <paramref name="radius"/> (через <paramref name="step"/>), и уровни вниз
    /// перебираются не глубже <paramref name="maxDepth"/> уровней от своего. Берутся только уровни, у которых под
    /// дырами есть пол (то, что реально видно); клетка, накрытая полом, дальше вниз не смотрит. Результат — по
    /// возрастанию (первым самый нижний).
    /// </summary>
    /// <returns>
    /// Остались дыры, под которыми в пределах глубины пола нет (вниз дальше трёх этажей, край крыши) — там клиент
    /// рисует дымку неба вместо уровней ниже.
    /// </returns>
    public bool VisibleLowerLevels(MapCoordinates at, int radius, int step, int maxDepth,
        List<(EntityUid Map, int Level)> result)
    {
        result.Clear();
        if (!TryGetLevel(at.MapId, out var own) || own.Level == 0)
            return false;

        var holes = new List<Vector2>();
        var center = new Vector2(MathF.Floor(at.Position.X) + 0.5f, MathF.Floor(at.Position.Y) + 0.5f);
        for (var x = -radius; x <= radius; x += step)
        {
            for (var y = -radius; y <= radius; y += step)
            {
                var point = center + new Vector2(x, y);
                if (!HasFloor(new MapCoordinates(point, at.MapId)))
                    holes.Add(point);
            }
        }

        if (holes.Count == 0)
            return false;

        var levels = Levels(own.Stack);
        for (var i = levels.Count - 1; i >= 0 && holes.Count > 0; i--)
        {
            var (map, level) = levels[i];
            if (level >= own.Level || own.Level > 0 && level < 0)
                continue;

            if (own.Level - level > maxDepth)
                break;

            // Уровень виден, только если под какой-то дырой у него есть пол; пустой (воздух у края башни) — пропуск.
            var mapId = Transform(map).MapID;
            if (holes.RemoveAll(point => HasFloor(new MapCoordinates(point, mapId))) > 0)
                result.Add((map, level));
        }

        result.Reverse();
        return holes.Count > 0;
    }

    #endregion

    #region Пол и крыша

    /// <summary>Есть ли пол (тайл грида) в точке.</summary>
    public bool HasFloor(MapCoordinates coords)
    {
        if (!_map.TryFindGridAt(coords, out var gridUid, out var grid))
            return false;

        var tile = _map.GetTileRef(gridUid, grid, coords);
        return !tile.Tile.IsEmpty;
    }

    /// <summary>Флаг крыши на гриде в точке (крыша одноэтажной постройки, нарисованная на карте).</summary>
    public bool HasRoofFlag(MapCoordinates coords)
    {
        if (!_map.TryFindGridAt(coords, out var gridUid, out var grid) || !TryComp<RoofComponent>(gridUid, out var roof))
            return false;

        return _roof.IsRooved((gridUid, grid, roof), _map.TileIndicesFor(gridUid, grid, coords));
    }

    /// <summary>Над точкой крыша: флаг крыши или пол на любом уровне выше. Под крышей не стреляют миномёты и т. п.</summary>
    public bool IsCovered(MapCoordinates coords)
    {
        if (HasRoofFlag(coords))
            return true;

        if (!TryGetLevel(coords.MapId, out var here))
            return false;

        foreach (var (map, level) in Levels(here.Stack))
        {
            if (level > here.Level && HasFloor(new MapCoordinates(coords.Position, Transform(map).MapID)))
                return true;
        }

        return false;
    }

    public bool IsCovered(EntityUid uid)
    {
        return IsCovered(_transform.GetMapCoordinates(uid));
    }

    /// <summary>
    /// Куда приходит удар сверху (мина, навесной снаряд, удар с орбиты) в точку XY: на самый верхний пол среди
    /// наземных уровней (крыша дома), иначе — на землю. Под землю сверху не попасть.
    /// </summary>
    public MapCoordinates ImpactPoint(MapCoordinates target, out int level)
    {
        level = 0;
        if (!TryGetLevel(target.MapId, out var here))
            return target;

        var levels = Levels(here.Stack);
        for (var i = levels.Count - 1; i >= 0; i--)
        {
            var (map, lvl) = levels[i];
            if (lvl < 0)
                break;

            var point = new MapCoordinates(target.Position, Transform(map).MapID);
            if (lvl == 0 || HasFloor(point))
            {
                level = lvl;
                return point;
            }
        }

        return target;
    }

    #endregion

    #region Прицел

    /// <summary>На сколько уровней видно с края: 1, с прицелом в двух руках — по прицелу (x8 — 3, x16 — все).</summary>
    public int SightDepth(EntityUid user)
    {
        var depth = 1;
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (!TryComp<DHZSightComponent>(held, out var sight))
                continue;

            if (TryComp<WieldableComponent>(held, out var wield) && !wield.Wielded)
                continue;

            depth = Math.Max(depth, sight.Depth);
        }

        return depth;
    }

    #endregion

    #region Стрельба между уровнями

    private void OnCrossLevelShot(Entity<DHZViewerComponent> ent, ref DHCrossLevelShotEvent args)
    {
        var from = _transform.ToMapCoordinates(args.Origin);
        var to = _transform.ToMapCoordinates(args.Target);
        if (from.MapId == to.MapId ||
            !TryGetLevel(from.MapId, out var shooterLevel) ||
            !TryGetLevel(to.MapId, out var targetLevel) ||
            shooterLevel.Stack != targetLevel.Stack ||
            ent.Comp.Camera is not { } camera || Transform(camera).MapID != to.MapId)
        {
            return;
        }

        var direction = to.Position - from.Position;
        var muzzle = direction.LengthSquared() > 0.01f
            ? from.Position + Vector2.Normalize(direction) * MathF.Min(CrossLevelMuzzle, direction.Length() * 0.5f)
            : from.Position;

        args.Origin = new EntityCoordinates(_map.GetMapOrInvalid(to.MapId), muzzle);
    }

    #endregion
}
