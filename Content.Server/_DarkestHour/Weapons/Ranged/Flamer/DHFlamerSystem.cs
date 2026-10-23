using System.Numerics;
using Content.Server._DarkestHour.Atmos.Fire;
using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._DarkestHour.Atmos.Fire;
using Content.Shared._DarkestHour.Weapons.Ranged.Flamer;
using Content.Shared.Database;
using Content.Shared.Maps;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Weapons.Ranged.Flamer;

/// <summary>
/// Серверная часть огнемёта: по выстрелу считает тайлы струи, списывает горючее и
/// поджигает тайлы по очереди, чтобы огонь «бежал» от ствола.
/// </summary>
public sealed partial class DHFlamerSystem : SharedDHFlamerSystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private DHFireSystem _fire = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    private readonly List<PendingFire> _pending = new();

    private record struct PendingFire(EntityUid Grid, Vector2i Tile, TimeSpan At, DHFlammableReagentPrototype Fuel);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHFlamerShotComponent, HitscanTraceEvent>(OnShot);
    }

    private void OnShot(Entity<DHFlamerShotComponent> ent, ref HitscanTraceEvent args)
    {
        if (!TryComp(args.Gun, out DHFlamerComponent? flamerComp))
            return;

        var flamer = new Entity<DHFlamerComponent>(args.Gun, flamerComp);
        if (!TryGetFuel(flamer, out var solution, out var fuel))
            return;

        var fromMap = _transform.ToMapCoordinates(args.FromCoordinates);
        if (!_map.TryFindGridAt(fromMap, out var gridUid, out var grid))
            return;

        var dir = args.ShotDirection;
        if (dir.LengthSquared() <= 0.0001f)
            return;

        dir = Vector2.Normalize(dir);

        var maxTiles = Math.Min(flamerComp.MaxRange, fuel.Range);
        maxTiles = Math.Min(maxTiles, (solution.Value.Comp.Solution.Volume / flamerComp.CostPer).Int());
        if (maxTiles <= 0)
            return;

        // Стреляем не дальше точки прицела.
        var reach = maxTiles;
        if (TryComp(args.Gun, out GunComponent? gun) && gun.ShootCoordinates is { } aim)
        {
            var aimMap = _transform.ToMapCoordinates(aim);
            if (aimMap.MapId == fromMap.MapId)
            {
                var distance = (aimMap.Position - fromMap.Position).Length();
                reach = Math.Clamp((int) MathF.Ceiling(distance), 1, maxTiles);
            }
        }

        var origin = _map.TileIndicesFor(gridUid, grid, fromMap);
        var seen = new HashSet<Vector2i> { origin };
        var line = new List<Vector2i>();

        for (var d = 0.25f; line.Count < reach && d <= reach + 1.5f; d += 0.25f)
        {
            var point = new MapCoordinates(fromMap.Position + dir * d, fromMap.MapId);
            var tile = _map.TileIndicesFor(gridUid, grid, point);
            if (!seen.Add(tile))
                continue;

            if (!CanBurn(gridUid, grid, tile, out var blocked))
                break;

            if (blocked)
            {
                // Термит прожигает преграду, остальное горючее растекается перед ней.
                if (fuel.BurnsBlockers)
                    line.Add(tile);
                break;
            }

            line.Add(tile);
        }

        if (line.Count == 0)
            return;

        SolutionContainer.SplitSolution(solution.Value, flamerComp.CostPer * line.Count);
        UpdateAppearance(flamer);

        var now = _timing.CurTime;
        var side = new Vector2(-dir.Y, dir.X);
        for (var i = 0; i < line.Count; i++)
        {
            var at = now + flamerComp.DelayPer * i;
            _pending.Add(new PendingFire(gridUid, line[i], at, fuel));

            // Растекающееся горючее (напалм): полоса шириной 3 начиная с третьего тайла.
            if (!fuel.Spread || i < 2)
                continue;

            var center = _map.GridTileToWorldPos(gridUid, grid, line[i]);
            foreach (var offset in new[] { side, -side })
            {
                var sideTile = _map.TileIndicesFor(gridUid, grid, new MapCoordinates(center + offset, fromMap.MapId));
                if (!seen.Add(sideTile) || !CanBurn(gridUid, grid, sideTile, out var sideBlocked) || sideBlocked)
                    continue;

                _pending.Add(new PendingFire(gridUid, sideTile, at, fuel));
            }
        }

        var shooter = args.Shooter ?? args.Gun;
        _adminLog.Add(LogType.Flammable, LogImpact.Medium,
            $"{ToPrettyString(shooter):actor} fired {ToPrettyString(args.Gun):tool} with {fuel.ID} over {line.Count} tiles");
    }

    /// <summary>False — дальше гореть нельзя (космос/нет тайла). Blocked — тайл перекрыт стеной/дверью.</summary>
    private bool CanBurn(EntityUid gridUid, MapGridComponent grid, Vector2i tile, out bool blocked)
    {
        blocked = false;
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || _turf.IsSpace(tileRef))
            return false;

        blocked = _atmosphere.IsTileAirBlockedCached(gridUid, tile);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pending.Count == 0)
            return;

        var now = _timing.CurTime;
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var fire = _pending[i];
            if (now < fire.At)
                continue;

            _pending.RemoveAt(i);
            if (!TerminatingOrDeleted(fire.Grid))
                _fire.SpawnFire(fire.Grid, fire.Tile, fire.Fuel, fire.Fuel.Duration);
        }
    }
}
