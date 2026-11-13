using System.Numerics;
using Content.Server._DarkestHour.Levels;
using Content.Shared._DarkestHour.Aviation;
using Content.Shared._DarkestHour.Environment;
using Content.Shared._DarkestHour.Levels;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._DarkestHour.Aviation;

/// <summary>
/// Настоящая высота бреющего (решение автора 2026-11-13). Раньше на заходе самолёт ставился прямо на карту
/// поверхности, и пилот видел то же, что пехота: комнаты и людей внутри. Теперь он идёт на уровне воздуха над полем
/// боя — пустой карте той же стопки Z-уровней: низко — на 1 уровень выше верхнего этажа (на «Терминусе» +2), средне —
/// на 2 (+3), высоко — на 3 (+4). Пилот смотрит вниз сквозь всю стопку до земли, здания закрыты крышами (клиент,
/// DHZRoofOverlay). Пехота самолёта не видит: по земле бежит его тень, рёв захода слышен у тени. Пушка, бомбы,
/// ПВО и ПЗРК работают по карте поверхности под заходом.
/// </summary>
public sealed partial class DHAircraftSystem
{
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private DHZLevelSystem _zLevels = default!;
    [Dependency] private DHZTransitSystem _zTransit = default!;

    /// <summary>Карта, по которой бьют пушка и бомбы и на которой самолёт ищут ПВО и ПЗРК (поверхность под заходом).</summary>
    private MapId GroundMapId(Entity<DHAircraftComponent> ent)
    {
        if (ent.Comp.PassSurface is { } surface && TryComp<MapComponent>(surface, out var map))
            return map.MapId;

        return Transform(ent).MapID;
    }

    private EntityUid? GroundMapUid(DHAircraftComponent comp, TransformComponent xform)
    {
        return comp.PassSurface ?? xform.MapUid;
    }

    /// <summary>
    /// Уровень воздуха на высоте <paramref name="altitude"/> над поверхностью: на 1–3 уровня выше её верхнего этажа.
    /// Создаётся при первом заходе (пустая карта с воздухом, тяжестью и сменой суток поверхности). null — некуда
    /// (стопка уже доходит до верхнего предела уровней).
    /// </summary>
    private EntityUid? AirMap(EntityUid surface, DHAircraftAltitude altitude)
    {
        if (!_zLevels.TryGetLevel(surface, out var ground))
        {
            ground = EnsureComp<DHZLevelComponent>(surface);
            ground.Stack = _zLevels.NewStack();
            ground.Level = 0;
            Dirty(surface, ground);
        }

        var top = 0;
        foreach (var (map, level) in _zLevels.Levels(ground.Stack))
        {
            if (!HasComp<DHZAirLevelComponent>(map))
                top = Math.Max(top, level);
        }

        var target = top + 1 + (int) altitude;
        if (target > DHZLevelComponent.MaxLevel)
            return null;

        if (_zLevels.LevelMap(ground.Stack, target) is { } existing)
            return HasComp<DHZAirLevelComponent>(existing) ? existing : null;

        if (_zTransit.CreateEmptyLevel(surface, target, out _) is not { } air)
            return null;

        EnsureComp<DHZAirLevelComponent>(air);
        _meta.SetEntityName(air, Loc.GetString("dh-aircraft-air-map", ("level", target)));

        // Сутки — как на земле, чтобы самолёт и земля под ним светились одинаково.
        if (TryComp<DHDayNightCycleComponent>(surface, out var groundCycle))
        {
            var cycle = EnsureComp<DHDayNightCycleComponent>(air);
            cycle.Duration = groundCycle.Duration;
            cycle.Start = groundCycle.Start;
            cycle.StartFraction = groundCycle.StartFraction;
            cycle.BaseColor = groundCycle.BaseColor;
            cycle.Entries = new(groundCycle.Entries);
            Dirty(air, cycle);
        }

        return air;
    }

    /// <summary>Перевести самолёт на бреющем на уровень воздуха своей высоты (в той же точке XY).</summary>
    private void MoveToAltitude(Entity<DHAircraftComponent> ent)
    {
        if (ent.Comp.PassSurface is not { } surface || TerminatingOrDeleted(surface))
            return;

        var target = AirMap(surface, ent.Comp.Altitude) ?? surface;
        var xform = Transform(ent);
        if (xform.MapUid == target)
            return;

        var position = _transform.GetWorldPosition(xform);
        _transform.SetCoordinates(ent, new EntityCoordinates(target, position));
        _transform.SetWorldRotation(ent, ent.Comp.Heading);
    }

    /// <summary>Тень самолёта на земле под ним.</summary>
    private void SpawnShadow(Entity<DHAircraftComponent> ent, EntityUid surface, Vector2 position)
    {
        RemoveShadow(ent);
        var shadow = Spawn(ent.Comp.ShadowPrototype, new EntityCoordinates(surface, position));
        _transform.SetWorldRotation(shadow, ent.Comp.Heading);
        ent.Comp.PassShadow = shadow;
    }

    private void MoveShadow(Entity<DHAircraftComponent> ent, Vector2 position)
    {
        if (ent.Comp.PassShadow is not { } shadow || TerminatingOrDeleted(shadow))
            return;

        _transform.SetWorldPosition(shadow, position);
        _transform.SetWorldRotation(shadow, ent.Comp.Heading);
    }

    private void RemoveShadow(Entity<DHAircraftComponent> ent)
    {
        if (ent.Comp.PassShadow is { } shadow && !TerminatingOrDeleted(shadow))
            QueueDel(shadow);

        ent.Comp.PassShadow = null;
    }
}
