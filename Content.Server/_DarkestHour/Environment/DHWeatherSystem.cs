using Content.Shared._DarkestHour.Environment;
using Content.Shared._DarkestHour.Movement;
using Content.Shared.Damage.Systems;
using Content.Shared.Light.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Weather;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Environment;

/// <summary>
/// Погода карты планеты: по расписанию (<see cref="DHWeatherScheduleComponent"/>) ставит ванильную погоду на время,
/// а опасности погоды (<see cref="DHWeatherHazardComponent"/>) раз в секунду действуют на живых под открытым небом:
/// замедление и урон, нарастающие и спадающие вместе с погодой. На этажах (Z-уровни выше земли) — погода земли:
/// под открытым небом на крыше она тоже бьёт, под потолком верхнего этажа — нет.
/// </summary>
public sealed partial class DHWeatherSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DHTimedSlowSystem _slow = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedWeatherSystem _weather = default!;
    [Dependency] private Content.Shared._DarkestHour.Levels.DHZLevelSystem _zLevels = default!;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SlowDuration = TimeSpan.FromSeconds(1.6);

    private TimeSpan _next;

    private readonly List<(EntityUid Map, EntityUid Weather, DHWeatherHazardComponent Hazard, float Percent)> _active = new();
    private readonly HashSet<EntityUid> _announced = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _next)
            return;

        _next = now + Interval;
        UpdateSchedules(now);
        UpdateHazards();
    }

    private void UpdateSchedules(TimeSpan now)
    {
        var query = EntityQueryEnumerator<DHWeatherScheduleComponent, MapComponent>();
        while (query.MoveNext(out var map, out var schedule, out _))
        {
            if (schedule.NextChange == TimeSpan.Zero)
            {
                schedule.NextChange = now + schedule.FirstDelay;
                continue;
            }

            if (now < schedule.NextChange || schedule.Options.Count == 0)
                continue;

            var option = Pick(schedule.Options);
            var duration = Between(option.MinDuration, option.MaxDuration);
            _weather.TryAddWeather(map, option.Weather, out _, duration);
            schedule.NextChange = now + duration + Between(schedule.MinClear, schedule.MaxClear);
        }
    }

    private DHWeatherOption Pick(List<DHWeatherOption> options)
    {
        var total = 0f;
        foreach (var option in options)
        {
            total += MathF.Max(0f, option.Weight);
        }

        var roll = _random.NextFloat(0f, MathF.Max(total, 0.001f));
        foreach (var option in options)
        {
            roll -= MathF.Max(0f, option.Weight);
            if (roll <= 0f)
                return option;
        }

        return options[^1];
    }

    private TimeSpan Between(TimeSpan min, TimeSpan max)
    {
        if (max <= min)
            return min;

        return min + TimeSpan.FromSeconds(_random.NextDouble() * (max - min).TotalSeconds);
    }

    private void UpdateHazards()
    {
        _active.Clear();
        var weathers = EntityQueryEnumerator<DHWeatherHazardComponent, StatusEffectComponent, TransformComponent>();
        while (weathers.MoveNext(out var uid, out var hazard, out var status, out var xform))
        {
            if (!HasComp<MapComponent>(xform.ParentUid))
                continue;

            var percent = _weather.GetWeatherPercent((uid, status));
            if (percent <= 0f)
                continue;

            _active.Add((xform.ParentUid, uid, hazard, percent));

            // Предупреждение — один раз за погоду.
            if (hazard.StartMessage is { } message && _announced.Add(uid))
                Announce(xform.ParentUid, Loc.GetString(message));
        }

        _announced.RemoveWhere(uid => TerminatingOrDeleted(uid));
        if (_active.Count == 0)
            return;

        var mobs = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobs.MoveNext(out var mob, out var state, out var xform))
        {
            if (state.CurrentState == MobState.Dead)
                continue;

            var weatherMap = WeatherMap(xform.MapUid);
            foreach (var (map, weather, hazard, percent) in _active)
            {
                if (weatherMap != map || !Exposed(mob, xform))
                    continue;

                if (hazard.Damage is { } damage)
                    _damageable.TryChangeDamage(mob, damage * percent, interruptsDoAfters: false, origin: weather);

                if (hazard.SpeedMultiplier < 1f)
                    _slow.Slow(mob, 1f - (1f - hazard.SpeedMultiplier) * percent, SlowDuration);
            }
        }
    }

    /// <summary>Чья погода действует на карте: на этаже — погода земли его стопки.</summary>
    private EntityUid? WeatherMap(EntityUid? map)
    {
        if (!_zLevels.TryGetLevel(map, out var level) || level.Level <= 0)
            return map;

        return _zLevels.LevelMap(level.Stack, 0) ?? map;
    }

    /// <summary>
    /// Под открытым небом: не в контейнере (машине, шкафу), над головой нет этажа и крыши; на земле — клетка, куда
    /// достаёт погода, на этаже — любой пол (крыша дома).
    /// </summary>
    private bool Exposed(EntityUid mob, TransformComponent xform)
    {
        if (_container.IsEntityInContainer(mob) || xform.GridUid is not { } grid ||
            !TryComp<MapGridComponent>(grid, out var gridComp))
        {
            return false;
        }

        if (_zLevels.IsCovered(mob))
            return false;

        if (_zLevels.TryGetLevel(xform.MapUid, out var level) && level.Level > 0)
            return true;

        var tile = _map.GetTileRef(grid, gridComp, xform.Coordinates);
        return _weather.CanWeatherAffect((grid, gridComp, CompOrNull<RoofComponent>(grid)), tile);
    }

    private void Announce(EntityUid map, string text)
    {
        var actors = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actors.MoveNext(out var uid, out _, out var xform))
        {
            if (WeatherMap(xform.MapUid) == map)
                _popup.PopupEntity(text, uid, uid, PopupType.LargeCaution);
        }
    }
}
