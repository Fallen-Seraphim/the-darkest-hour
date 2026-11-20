using Content.Shared._DarkestHour.Battlefield;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._DarkestHour.Battlefield;

/// <summary>
/// Мосты (решение автора 2026-11-19): пролёт рушится от любого тяжёлого взрыва (ОБ, бомбы, заряды сапёров) — настил
/// становится глубокой водой. Сапёры наводят его обратно понтонной секцией из снабжения.
/// </summary>
public sealed partial class DHBattlefieldSystem
{
    private const string GroundTile = "FloorPlanetDirt";
    private const string DeepWater = "DHWaterDeep";

    private void OnBridgeDamaged(Entity<DHBridgeTileComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.Broken || _damageable.GetTotalDamage((ent.Owner, args.Damageable)).Float() < ent.Comp.BreakDamage)
            return;

        if (Transform(ent).GridUid is not { } grid)
            return;

        CollapseSection(grid, ent.Comp.Section);
    }

    private List<Entity<DHBridgeTileComponent, TransformComponent>> SectionTiles(EntityUid grid, string section)
    {
        var result = new List<Entity<DHBridgeTileComponent, TransformComponent>>();
        var query = EntityQueryEnumerator<DHBridgeTileComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tile, out var xform))
        {
            if (xform.GridUid == grid && tile.Section == section)
                result.Add((uid, tile, xform));
        }

        return result;
    }

    /// <summary>Пролёт рушится: настил — в воду, всё закреплённое на нём (перила, мешки) — в реку.</summary>
    private void CollapseSection(EntityUid grid, string section)
    {
        if (!TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var ground = new Tile(_tileDefs[GroundTile].TileId);
        foreach (var (uid, tile, xform) in SectionTiles(grid, section))
        {
            if (tile.Broken)
                continue;

            tile.Broken = true;
            var indices = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);

            var anchored = new List<EntityUid>();
            var enumerator = _map.GetAnchoredEntitiesEnumerator(grid, gridComp, indices);
            while (enumerator.MoveNext(out var other))
            {
                if (other is { } o && o != uid && !HasComp<DHBridgeTileComponent>(o))
                    anchored.Add(o);
            }

            foreach (var other in anchored)
            {
                QueueDel(other);
            }

            _map.SetTile(grid, gridComp, indices, ground);
            Spawn(DeepWater, _map.GridTileToLocal(grid, gridComp, indices));
            _damageable.SetAllDamage(uid, FixedPoint2.Zero);
        }

        if (SectionTiles(grid, section) is { Count: > 0 } tiles)
        {
            _popup.PopupCoordinates(Loc.GetString("dh-bridge-collapsed"), tiles[0].Comp2.Coordinates,
                PopupType.LargeCaution);
        }
    }

    private void OnPontoonUse(Entity<DHPontoonKitComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (NearestBrokenBridge(args.User, ent.Comp.Range) == null)
        {
            _popup.PopupEntity(Loc.GetString("dh-pontoon-no-bridge"), args.User, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.DeployTime,
            new DHPontoonDeployDoAfterEvent(), ent.Owner, used: ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
        _popup.PopupEntity(Loc.GetString("dh-pontoon-deploying"), args.User, args.User);
    }

    private Entity<DHBridgeTileComponent, TransformComponent>? NearestBrokenBridge(EntityUid user, float range)
    {
        var position = _transform.GetMapCoordinates(user);
        Entity<DHBridgeTileComponent, TransformComponent>? best = null;
        var bestDistance = range;
        var query = EntityQueryEnumerator<DHBridgeTileComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tile, out var xform))
        {
            if (!tile.Broken || xform.MapID != position.MapId)
                continue;

            var distance = (_transform.GetWorldPosition(xform) - position.Position).Length();
            if (distance > bestDistance)
                continue;

            best = (uid, tile, xform);
            bestDistance = distance;
        }

        return best;
    }

    private void OnPontoonDeployed(Entity<DHPontoonKitComponent> ent, ref DHPontoonDeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (NearestBrokenBridge(args.User, ent.Comp.Range) is not { } near || near.Comp2.GridUid is not { } grid ||
            !TryComp<MapGridComponent>(grid, out var gridComp))
        {
            _popup.PopupEntity(Loc.GetString("dh-pontoon-no-bridge"), args.User, args.User);
            return;
        }

        foreach (var (uid, tile, xform) in SectionTiles(grid, near.Comp1.Section))
        {
            if (!tile.Broken)
                continue;

            tile.Broken = false;
            var indices = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
            var water = new List<EntityUid>();
            var enumerator = _map.GetAnchoredEntitiesEnumerator(grid, gridComp, indices);
            while (enumerator.MoveNext(out var other))
            {
                if (other is { } o && HasComp<Content.Shared._DarkestHour.Fluids.DHWaterSourceComponent>(o))
                    water.Add(o);
            }

            foreach (var w in water)
            {
                QueueDel(w);
            }

            _map.SetTile(grid, gridComp, indices, new Tile(_tileDefs[tile.Floor].TileId));
            _damageable.SetAllDamage(uid, FixedPoint2.Zero);
        }

        _popup.PopupEntity(Loc.GetString("dh-pontoon-deployed"), args.User, args.User, PopupType.Medium);
        QueueDel(ent);
    }
}
