using System.Numerics;
using Content.Shared._DarkestHour.Atmos.Fire;
using Content.Shared._DarkestHour.Battlefield;
using Content.Shared._DarkestHour.Fortifications;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Battlefield;

/// <summary>
/// Воронки, горящая трава, крыши по материалу и завалы (решения автора 2026-11-19).
/// <list type="bullet">
/// <item>воронки от ОБ (от 300 мм) и тяжёлых бомб — до конца раунда: замедляют пехоту и вдвое — технику, в них
/// укрываются как в окопе;</item>
/// <item>высокая трава загорается от огня рядом и сгорает, огонь идёт по полю;</item>
/// <item>крыши: бетонная держит миномёт и лёгкие ракеты, но не ОБ и тяжёлые бомбы; металл и шифер (лёгкие крыши
/// одноэтажек) ломаются всем;</item>
/// <item>обрушенная крыша оставляет завалы — препятствие и укрытие, разбираются лопатой (руками — дольше); кто
/// стоял под обрушением, может оказаться под завалом.</item>
/// </list>
/// </summary>
public sealed partial class DHBattlefieldSystem
{
    private const string CraterProto = "DHCrater";
    private const string RubbleProto = "DHRubble";
    private static readonly ProtoId<DHFlammableReagentPrototype> GrassFuel = "Ethanol";
    private static readonly ProtoId<Content.Shared.Damage.Prototypes.DamageTypePrototype> Blunt = "Blunt";

    /// <summary>Доля клеток обрушения, где ложится завал, и время, через которое заваленный выбирается сам.</summary>
    private const float RubbleChance = 0.35f;
    private static readonly TimeSpan BuriedTime = TimeSpan.FromSeconds(30);

    /// <summary>Сила удара по крыше: лёгкий (миномёт, ракета) или тяжёлый (ОБ, тяжёлая бомба).</summary>
    public enum RoofHit : byte
    {
        Light,
        Heavy,
    }

    #region Воронки

    /// <summary>Воронки от ОБ и тяжёлых бомб (п. 7) — выключены решением автора 2026-11-26.</summary>
    public static readonly bool CratersEnabled = false;

    /// <summary>Воронка радиусом <paramref name="radius"/> клеток на земле (не на воде, не в стене, не в окопе).</summary>
    public void SpawnCrater(MapCoordinates center, int radius)
    {
        // Решение автора 2026-11-26: воронки от взрывов убраны. Код оставлен выключенным — вернуть, сняв флаг.
        if (!CratersEnabled)
            return;

        if (_zLevels.TryGetLevel(center.MapId, out var level) && level.Level != 0)
            return;

        if (!_map.TryFindGridAt(center, out var grid, out var gridComp))
            return;

        var origin = _map.TileIndicesFor(grid, gridComp, center);
        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                if (dx * dx + dy * dy > radius * radius + radius)
                    continue;

                var tile = origin + new Vector2i(dx, dy);
                if (!_map.TryGetTileRef(grid, gridComp, tile, out var tileRef) || tileRef.Tile.IsEmpty ||
                    Blocked(grid, gridComp, tile) || HasTrenchOrWater(grid, gridComp, tile))
                {
                    continue;
                }

                Spawn(CraterProto, _map.GridTileToLocal(grid, gridComp, tile));
            }
        }
    }

    private bool HasTrenchOrWater(EntityUid grid, MapGridComponent gridComp, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(grid, gridComp, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<DHTrenchComponent>(uid) || HasComp<DHCraterComponent>(uid) ||
                HasComp<Content.Shared._DarkestHour.Fluids.DHWaterSourceComponent>(uid))
            {
                return true;
            }
        }

        return _terrainTiles.IsImpassableTerrain(grid, gridComp, tile);
    }

    /// <summary>Множитель скорости машины: воронка под ней — медленнее.</summary>
    public float CraterSpeed(EntityUid vehicle)
    {
        var factor = 1f;
        foreach (var crater in _lookup.GetEntitiesInRange<DHCraterComponent>(Transform(vehicle).Coordinates, 1.2f))
        {
            factor = MathF.Min(factor, crater.Comp.VehicleSpeed);
        }

        return factor;
    }

    #endregion

    #region Горящая трава

    /// <summary>Высокая трава (RMC) и всё, что помечено <see cref="DHFlammableGrassComponent"/>.</summary>
    private bool IsGrass(EntityUid uid, out float chance)
    {
        chance = 0f;
        if (TryComp<DHFlammableGrassComponent>(uid, out var grass))
        {
            chance = grass.CatchChance;
            return true;
        }

        if (MetaData(uid).EntityPrototype?.ID.StartsWith("RMCGrassTall") == true)
        {
            chance = 0.35f;
            return true;
        }

        return false;
    }

    private void SpreadGrassFire()
    {
        if (!_proto.TryIndex(GrassFuel, out var fuel))
            return;

        var catching = new HashSet<EntityUid>();
        var fires = EntityQueryEnumerator<DHTileFireComponent, TransformComponent>();
        while (fires.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid == null)
                continue;

            foreach (var uid in _lookup.GetEntitiesInRange(xform.Coordinates, 1.5f, LookupFlags.Static))
            {
                if (!catching.Contains(uid) && IsGrass(uid, out var chance) && _random.Prob(chance))
                    catching.Add(uid);
            }
        }

        foreach (var grass in catching)
        {
            var xform = Transform(grass);
            if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
                continue;

            var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
            _fire.SpawnFire(grid, tile, fuel, _random.NextFloat(12f, 20f));
            QueueDel(grass);
        }
    }

    #endregion

    #region Крыши и завалы

    /// <summary>
    /// Удар сверху по точке поверхности: ломает крышу по материалу и обрушивает её в завалы. Возвращает точку, где
    /// рвётся заряд (крыша, если она устояла; иначе как была).
    /// </summary>
    public MapCoordinates RoofStrike(MapCoordinates target, RoofHit hit, float radius)
    {
        var impact = _zLevels.ImpactPoint(target, out var level);
        if (level > 0)
        {
            // Бетонная крыша держит миномёт и лёгкие ракеты.
            if (hit == RoofHit.Light && IsConcrete(impact))
                return impact;

            _zTransit.BreakRoof(impact, radius);
            if (_zLevels.TryGetLevel(impact.MapId, out var here))
            {
                foreach (var (map, lvl) in _zLevels.Levels(here.Stack))
                {
                    if (lvl == level - 1)
                    {
                        Collapse(new MapCoordinates(impact.Position, Transform(map).MapID), radius);
                        break;
                    }
                }
            }

            return impact;
        }

        // Лёгкая крыша одноэтажки (флаг крыши) — ломается всем.
        if (_zLevels.HasRoofFlag(impact))
        {
            _zTransit.ClearRoofFlags(impact, radius);
            Collapse(impact, radius);
        }

        return impact;
    }

    private bool IsConcrete(MapCoordinates point)
    {
        if (!_map.TryFindGridAt(point, out var grid, out var gridComp) ||
            !_map.TryGetTileRef(grid, gridComp, _map.TileIndicesFor(grid, gridComp, point), out var tileRef))
        {
            return false;
        }

        var id = _tileDefs[tileRef.Tile.TypeId].ID;
        return id.Contains("Concrete") || id.Contains("Reinforced") || id.Contains("Plastitanium");
    }

    /// <summary>Обломки обрушенной крыши ложатся на пол под ней; кто стоял там — может оказаться под завалом.</summary>
    public void Collapse(MapCoordinates landing, float radius)
    {
        if (!_map.TryFindGridAt(landing, out var grid, out var gridComp))
            return;

        var now = _timing.CurTime;
        foreach (var tileRef in _map.GetTilesIntersecting(grid, gridComp, new Circle(landing.Position, radius)))
        {
            if (!_random.Prob(RubbleChance) || Blocked(grid, gridComp, tileRef.GridIndices) ||
                HasTrenchOrWater(grid, gridComp, tileRef.GridIndices))
            {
                continue;
            }

            var coords = _map.GridTileToLocal(grid, gridComp, tileRef.GridIndices);
            var rubble = Spawn(RubbleProto, coords);
            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 0.6f))
            {
                if (HasComp<DHBuriedComponent>(mob))
                    continue;

                var buried = EnsureComp<DHBuriedComponent>(mob);
                buried.Rubble = rubble;
                buried.FreeAt = now + BuriedTime;
                Dirty(mob, buried);
                _damageable.TryChangeDamage(mob.Owner, new DamageSpecifier(_proto.Index(Blunt), FixedPoint2.New(15)));
                _popup.PopupEntity(Loc.GetString("dh-rubble-buried"), mob, mob, PopupType.LargeCaution);
            }
        }
    }

    private void OnRubbleInteractUsing(Entity<DHRubbleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<DHEntrenchingToolComponent>(args.Used))
            return;

        args.Handled = StartClearing(ent, args.User, args.Used, ent.Comp.ClearTime);
    }

    private void OnRubbleInteractHand(Entity<DHRubbleComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        // Руками — втрое дольше, чем лопатой.
        args.Handled = StartClearing(ent, args.User, null, ent.Comp.ClearTime * 3f);
    }

    private bool StartClearing(Entity<DHRubbleComponent> ent, EntityUid user, EntityUid? used, float time)
    {
        if (HasComp<DHBuriedComponent>(user))
            return false;

        _popup.PopupEntity(Loc.GetString("dh-rubble-clearing"), ent, user);
        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, time, new DHRubbleClearDoAfterEvent(),
            ent.Owner, ent.Owner, used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnRubbleCleared(Entity<DHRubbleComponent> ent, ref DHRubbleClearDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        FreeBuried(ent);
        QueueDel(ent);
    }

    private void FreeBuried(EntityUid rubble)
    {
        var query = EntityQueryEnumerator<DHBuriedComponent>();
        var freed = new List<EntityUid>();
        while (query.MoveNext(out var uid, out var buried))
        {
            if (buried.Rubble == rubble)
                freed.Add(uid);
        }

        foreach (var uid in freed)
        {
            RemComp<DHBuriedComponent>(uid);
            _popup.PopupEntity(Loc.GetString("dh-rubble-freed"), uid, uid);
        }
    }

    private void UpdateBuried(TimeSpan now)
    {
        var freed = new List<EntityUid>();
        var query = EntityQueryEnumerator<DHBuriedComponent>();
        while (query.MoveNext(out var uid, out var buried))
        {
            if (now >= buried.FreeAt || buried.Rubble is not { } rubble || TerminatingOrDeleted(rubble))
                freed.Add(uid);
        }

        foreach (var uid in freed)
        {
            RemComp<DHBuriedComponent>(uid);
            _popup.PopupEntity(Loc.GetString("dh-rubble-freed"), uid, uid);
        }
    }

    #endregion
}
