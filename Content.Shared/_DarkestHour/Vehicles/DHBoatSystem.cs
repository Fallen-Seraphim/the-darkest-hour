using System.Numerics;
using Content.Shared._DarkestHour.ControlPoints;
using Content.Shared._DarkestHour.Fluids;
using Content.Shared._DarkestHour.Terrain;
using Content.Shared.Buckle.Components;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared._DarkestHour.Vehicles;

/// <summary>
/// Лодки (план осады п. 10, решения автора 2026-11-19): вода под корпусом, берег рядом, сойти — только у берега или на
/// мелководье.
/// </summary>
public sealed partial class DHBoatSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DHTerrainTileSystem _terrain = default!;

    public enum WaterKind : byte
    {
        None,
        Shallow,
        Deep,
    }

    /// <summary>Кого сейчас снимают с лодки принудительно (лодку потопили) — проверку берега не делаем.</summary>
    public readonly HashSet<EntityUid> Forced = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHVehicleSeatComponent, UnstrapAttemptEvent>(OnUnstrapAttempt);
        SubscribeLocalEvent<DHWaterSourceComponent, PreventCollideEvent>(OnWaterPreventCollide);
    }

    /// <summary>Сидящих в лодке над глубокой водой вода не выталкивает.</summary>
    private void OnWaterPreventCollide(Entity<DHWaterSourceComponent> ent, ref PreventCollideEvent args)
    {
        if (!args.Cancelled && TryComp<BuckleComponent>(args.OtherEntity, out var rider) &&
            rider.BuckledTo is { } seat && HasComp<DHVehicleSeatComponent>(seat))
        {
            args.Cancelled = true;
        }
    }

    /// <summary>Сойти с лодки можно, только если рядом берег или мелководье.</summary>
    private void OnUnstrapAttempt(Entity<DHVehicleSeatComponent> ent, ref UnstrapAttemptEvent args)
    {
        if (args.Cancelled || Forced.Contains(args.Buckle.Owner))
            return;

        var boat = Transform(ent).ParentUid;
        if (!TryComp<DHBoatComponent>(boat, out var boatComp))
            return;

        if (NearestDry(boat, Transform(ent).Coordinates, boatComp.ShoreRange) != null)
            return;

        args.Cancelled = true;
        if (args.Popup && args.User is { } user)
            _popup.PopupClient(Loc.GetString("dh-boat-no-shore"), user, user);
    }

    public WaterKind WaterAt(EntityUid grid, MapGridComponent gridComp, Vector2i tile)
    {
        var kind = WaterKind.None;
        var anchored = _map.GetAnchoredEntitiesEnumerator(grid, gridComp, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (!HasComp<DHWaterSourceComponent>(uid))
                continue;

            if (TryComp<DHTacticalTerrainComponent>(uid, out var terrain) && terrain.Kind == DHTacticalTerrain.DeepWater)
                return WaterKind.Deep;

            kind = WaterKind.Shallow;
        }

        // Середина моря и озера на большой карте — тайл, без сущности.
        if (kind == WaterKind.None && _terrain.IsDeepWater(grid, gridComp, tile))
            return WaterKind.Deep;

        return kind;
    }

    /// <summary>Точки корпуса в долях половины размера: центр, углы, середины носа и кормы.</summary>
    /// <remarks>Массив, а не stackalloc: песочница клиента не пропускает непроверяемый IL.</remarks>
    private static readonly Vector2[] HullPoints =
    {
        Vector2.Zero,
        new(-1f, -1f),
        new(1f, -1f),
        new(-1f, 1f),
        new(1f, 1f),
        new(0f, -1f),
        new(0f, 1f),
    };

    /// <summary>Под всем корпусом (центр и углы) — вода.</summary>
    public bool OnWater(MapId map, Vector2 center, Angle rotation, Vector2 halfSize)
    {
        if (!_map.TryFindGridAt(new MapCoordinates(center, map), out var grid, out var gridComp))
            return false;

        var shrink = halfSize - new Vector2(0.2f, 0.2f);
        foreach (var offset in HullPoints)
        {
            var world = center + rotation.RotateVec(offset * shrink);
            var tile = _map.WorldToTile(grid, gridComp, world);
            if (WaterAt(grid, gridComp, tile) == WaterKind.None)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Ближайшая клетка, куда можно встать (не глубокая вода, не стена и не под корпусом лодки), не дальше
    /// <paramref name="range"/> от точки.
    /// </summary>
    public EntityCoordinates? NearestDry(EntityUid boat, EntityCoordinates from, float range)
    {
        var mapCoords = _transform.ToMapCoordinates(from);
        if (!_map.TryFindGridAt(mapCoords, out var grid, out var gridComp))
            return null;

        Box2Rotated? hull = null;
        if (TryComp<DHVehicleComponent>(boat, out var vehicle))
        {
            var xform = Transform(boat);
            var center = _transform.GetWorldPosition(xform);
            hull = new Box2Rotated(Box2.CenteredAround(center, (vehicle.HullHalfSize + new Vector2(0.3f, 0.3f)) * 2f),
                _transform.GetWorldRotation(xform), center);
        }

        var origin = _map.WorldToTile(grid, gridComp, mapCoords.Position);
        var reach = (int) MathF.Ceiling(range);
        EntityCoordinates? best = null;
        var bestDistance = float.MaxValue;
        for (var dx = -reach; dx <= reach; dx++)
        {
            for (var dy = -reach; dy <= reach; dy++)
            {
                var tile = origin + new Vector2i(dx, dy);
                var local = _map.GridTileToLocal(grid, gridComp, tile);
                var world = _transform.ToMapCoordinates(local).Position;
                var distance = Vector2.Distance(world, mapCoords.Position);
                if (distance > range + 0.5f || distance >= bestDistance)
                    continue;

                if (hull is { } box && box.Contains(world))
                    continue;

                if (!Standable(grid, gridComp, tile))
                    continue;

                best = local;
                bestDistance = distance;
            }
        }

        return best;
    }

    private bool Standable(EntityUid grid, MapGridComponent gridComp, Vector2i tile)
    {
        if (!_map.TryGetTileRef(grid, gridComp, tile, out var tileRef) || tileRef.Tile.IsEmpty ||
            _terrain.IsDeepWater(tileRef.Tile) || _terrain.IsRock(tileRef.Tile))
        {
            return false;
        }

        var anchored = _map.GetAnchoredEntitiesEnumerator(grid, gridComp, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (TryComp<DHTacticalTerrainComponent>(uid, out var terrain) && terrain.Kind == DHTacticalTerrain.DeepWater)
                return false;

            if (TryComp<PhysicsComponent>(uid, out var physics) && physics.CanCollide && physics.Hard &&
                !HasComp<DHWaterSourceComponent>(uid) &&
                (physics.CollisionLayer & (int) CollisionGroup.MidImpassable) != 0)
            {
                return false;
            }
        }

        return true;
    }
}
