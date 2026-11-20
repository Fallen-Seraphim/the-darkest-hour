// Тень и струи челнока над полем боя. Устройство (снимок силуэта, тень и струи на земле под челноком, звук у тени) —
// по образцу RussianCM (CMU14, github.com/flex5hybrid/RussianCM, AGPL v3): DropshipTacticalLandSystem
// (UpdateHoverEffects, CaptureHoverSilhouette). Добавлены копии эффекта на этажах (крыши), сила по высоте и скрытие
// под крышей.
using System.Numerics;
using Content.Shared._DarkestHour.Assault;
using Content.Shared.Audio;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._DarkestHour.Assault;

public sealed partial class DHDropshipSystem
{
    private const string ShadowProto = "DHDropshipHoverShadow";
    private const string DownwashProto = "DHDropshipHoverDownwash";

    /// <summary>Как часто пересчитываются сила и скрытие эффектов (положение — каждый тик).</summary>
    private static readonly TimeSpan EffectsInterval = TimeSpan.FromSeconds(0.5);

    /// <summary>Положения сопел — доли половины размера корпуса от его центра.</summary>
    private static readonly Vector2[] NozzleFractions =
    {
        new(-0.55f, -0.55f), new(0.55f, -0.55f), new(-0.55f, 0.55f), new(0.55f, 0.55f),
    };

    /// <summary>Эффекты на одном уровне земли под челноком: тень и струи.</summary>
    private sealed class HoverLayer
    {
        public EntityUid Map;
        public int Level;
        public EntityUid Shadow;
        public readonly List<(EntityUid Uid, Vector2 Offset)> Downwashes = new();
    }

    private readonly Dictionary<EntityUid, List<HoverLayer>> _hover = new();

    /// <summary>
    /// Эффекты есть, пока челнок на уровне над полем боя: на каждом наземном уровне (земля, этажи) ниже него — своя
    /// тень, а при снижении и подъёме — струи двигателей. Чем ниже челнок над уровнем, тем сильнее.
    /// </summary>
    private void UpdateHover(Entity<DHDropshipComponent> ship, TimeSpan now)
    {
        var comp = ship.Comp;
        if (comp.State != DHDropshipState.InFlight || comp.Level <= 0 || comp.Surface is not { } surface ||
            !Exists(surface))
        {
            ClearHover(ship);
            return;
        }

        var lift = comp.Flight is DHDropshipFlight.Descend or DHDropshipFlight.Climb || comp.Level < comp.CruiseLevel;
        if (!_hover.TryGetValue(ship, out var layers) || layers.Count == 0 || layers[0].Map != surface ||
            comp.HoverLevel != comp.Level || comp.HoverLift != lift)
        {
            layers = SpawnHover(ship, surface, lift);
            comp.HoverLevel = comp.Level;
            comp.HoverLift = lift;
            comp.NextHoverRefresh = TimeSpan.Zero;
        }

        if (!TryComp<MapGridComponent>(ship, out var grid))
            return;

        var shipXform = Transform(ship);
        var (origin, rotation) = _transform.GetWorldPositionRotation(shipXform);
        var matrix = _transform.GetWorldMatrix(shipXform);
        var refresh = now >= comp.NextHoverRefresh;
        if (refresh)
            comp.NextHoverRefresh = now + EffectsInterval;

        foreach (var layer in layers)
        {
            if (!TerminatingOrDeleted(layer.Shadow))
            {
                _transform.SetWorldPositionRotation(layer.Shadow, origin, rotation);
                if (refresh)
                {
                    var center = Vector2.Transform(grid.LocalAABB.Center, matrix);
                    SetEffect(layer.Shadow, Strength(comp.Level - layer.Level),
                        Hidden(surface, layer.Level, comp.Level, center));
                }
            }

            foreach (var (uid, offset) in layer.Downwashes)
            {
                if (TerminatingOrDeleted(uid))
                    continue;

                var position = Vector2.Transform(offset, matrix);
                _transform.SetWorldPosition(uid, position);
                if (refresh)
                    SetEffect(uid, Strength(comp.Level - layer.Level), Hidden(surface, layer.Level, comp.Level, position));
            }
        }
    }

    private List<HoverLayer> SpawnHover(Entity<DHDropshipComponent> ship, EntityUid surface, bool lift)
    {
        ClearHover(ship);
        var layers = new List<HoverLayer>();
        _hover[ship] = layers;
        if (!TryComp<MapGridComponent>(ship, out var grid))
            return layers;

        var top = _aircraft.TopFloor(surface, out var stack);
        var origin = _transform.GetWorldPosition(ship);
        var bounds = grid.LocalAABB;
        var (tiles, parts) = CaptureSilhouette(ship, grid);

        for (var level = 0; level <= Math.Min(top, ship.Comp.Level - 1); level++)
        {
            var map = level == 0 ? surface : _zLevels.LevelMap(stack, level);
            if (map == null)
                continue;

            var layer = new HoverLayer { Map = map.Value, Level = level };
            layer.Shadow = Spawn(ShadowProto, new EntityCoordinates(map.Value, origin));
            var shadow = EnsureComp<DHDropshipHoverShadowComponent>(layer.Shadow);
            shadow.HullBounds = bounds.Enlarged(2f);
            shadow.Tiles = tiles;
            shadow.Parts = parts;
            Dirty(layer.Shadow, shadow);
            SetEffect(layer.Shadow, 0f, true);

            if (lift)
            {
                var half = bounds.Size / 2f;
                foreach (var fraction in NozzleFractions)
                {
                    var offset = bounds.Center + fraction * half;
                    var uid = Spawn(DownwashProto, new EntityCoordinates(map.Value, origin));
                    var wash = EnsureComp<DHDropshipHoverDownwashComponent>(uid);
                    wash.Offset = offset;
                    Dirty(uid, wash);
                    SetEffect(uid, 0f, true);
                    layer.Downwashes.Add((uid, offset));
                }
            }

            layers.Add(layer);
        }

        // Первая по счёту — земля: по ней проверяется, что эффекты над нужной поверхностью.
        if (layers.Count == 0 || layers[0].Map != surface)
            layers.Insert(0, new HoverLayer { Map = surface, Level = 0, Shadow = EntityUid.Invalid });

        return layers;
    }

    private void ClearHover(EntityUid ship)
    {
        if (!_hover.Remove(ship, out var layers))
            return;

        foreach (var layer in layers)
        {
            if (layer.Shadow.IsValid() && !TerminatingOrDeleted(layer.Shadow))
                QueueDel(layer.Shadow);

            foreach (var (uid, _) in layer.Downwashes)
            {
                if (!TerminatingOrDeleted(uid))
                    QueueDel(uid);
            }
        }

        if (TryComp<DHDropshipComponent>(ship, out var comp))
            comp.HoverLevel = -1;
    }

    /// <summary>Сила по высоте над уровнем: вплотную — 1, на 4 уровня выше — четверть.</summary>
    private static float Strength(int height)
    {
        return Math.Clamp(1.25f - 0.25f * height, 0.1f, 1f);
    }

    /// <summary>
    /// Эффект на уровне <paramref name="level"/> скрыт, если над точкой крыша (флаг или пол этажа между уровнем и
    /// челноком), а на этаже — и если там нет пола (тогда его видно ниже, на земле).
    /// </summary>
    private bool Hidden(EntityUid surface, int level, int shipLevel, Vector2 position)
    {
        var top = _aircraft.TopFloor(surface, out var stack);
        var map = level == 0 ? surface : _zLevels.LevelMap(stack, level);
        if (map == null || !TryComp<MapComponent>(map, out var mapComp))
            return true;

        var here = new MapCoordinates(position, mapComp.MapId);
        if (level > 0 && !_zLevels.HasFloor(here))
            return true;

        if (_zLevels.HasRoofFlag(here))
            return true;

        for (var above = level + 1; above < shipLevel && above <= top; above++)
        {
            if (_zLevels.LevelMap(stack, above) is { } upper && TryComp<MapComponent>(upper, out var upperMap) &&
                _zLevels.HasFloor(new MapCoordinates(position, upperMap.MapId)))
            {
                return true;
            }
        }

        return false;
    }

    private void SetEffect(EntityUid uid, float strength, bool hidden)
    {
        var effect = EnsureComp<DHDropshipHoverEffectComponent>(uid);
        if (MathHelper.CloseTo(effect.Strength, strength) && effect.Hidden == hidden)
            return;

        effect.Strength = strength;
        effect.Hidden = hidden;
        Dirty(uid, effect);

        // Рёв у тени: ниже — громче и дальше слышно. Под крышей тоже слышно.
        if (TryComp<AmbientSoundComponent>(uid, out var ambient))
        {
            _ambient.SetVolume(uid, -22f + 14f * strength, ambient);
            _ambient.SetRange(uid, 14f + 14f * strength, ambient);
        }

        if (TryComp<PointLightComponent>(uid, out var light))
        {
            _lights.SetEnabled(uid, !hidden, light);
            _lights.SetEnergy(uid, 2.5f * strength, light);
        }
    }

    /// <summary>Снимок корпуса для тени: тайлы с их текстурами и закреплённые на гриде детали.</summary>
    private (List<DHDropshipShadowTile> Tiles, List<DHDropshipShadowPart> Parts) CaptureSilhouette(EntityUid ship,
        MapGridComponent grid)
    {
        var tiles = new List<DHDropshipShadowTile>();
        var enumerator = _mapSystem.GetAllTilesEnumerator(ship, grid);
        while (enumerator.MoveNext(out var tile))
        {
            if (_tileDefs[tile.Value.Tile.TypeId] is not ContentTileDefinition { Sprite: { } texture })
                continue;

            tiles.Add(new DHDropshipShadowTile(_mapSystem.TileCenterToVector(ship, grid, tile.Value.GridIndices),
                texture.ToString(), tile.Value.Tile.Variant));
        }

        var parts = new List<DHDropshipShadowPart>();
        var children = Transform(ship).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            var xform = Transform(child);
            if (!xform.Anchored || MetaData(child).EntityPrototype is not { } prototype)
                continue;

            parts.Add(new DHDropshipShadowPart(prototype.ID, xform.LocalPosition, xform.LocalRotation));
        }

        return (tiles, parts);
    }
}
