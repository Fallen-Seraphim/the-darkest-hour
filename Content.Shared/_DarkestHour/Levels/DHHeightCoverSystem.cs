using Content.Shared.Projectiles;
using Content.Shared.Standing;
using Robust.Shared.Map;

namespace Content.Shared._DarkestHour.Levels;

/// <summary>
/// Высота как преимущество (план осады п. 21, решения автора 2026-11-19), общая логика для предсказания:
/// <list type="bullet">
/// <item>пуля с верхнего уровня летит над баррикадами и мешками, а укрытие окопа и воронки от неё вдвое слабее;</item>
/// <item>снизу по цели на крыше (этаже без потолка над ней) не попасть, если она дальше клетки от края; у самого края
/// — промах в 40%; лежащую на крыше снизу не достать вовсе.</item>
/// </list>
/// </summary>
public sealed partial class DHHeightCoverSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private DHZLevelSystem _zLevels = default!;

    /// <summary>Шанс промаха снизу по цели у самого края крыши.</summary>
    public const float EdgeMissChance = 0.4f;

    /// <summary>Во сколько раз слабее укрытие окопа от огня сверху.</summary>
    public const float PlungingTrenchFactor = 0.5f;

    /// <summary>На сколько уровней стрелок пули выше (больше нуля) или ниже (меньше нуля) места, где летит пуля.</summary>
    public int ShooterHeight(EntityUid projectile)
    {
        if (!TryComp<ProjectileComponent>(projectile, out var comp) || comp.Shooter is not { } shooter ||
            !Exists(shooter))
        {
            return 0;
        }

        var bullet = Transform(projectile).MapID;
        var from = Transform(shooter).MapID;
        if (bullet == from || !_zLevels.TryGetLevel(bullet, out var here) || !_zLevels.TryGetLevel(from, out var there) ||
            here.Stack != there.Stack)
        {
            return 0;
        }

        return there.Level - here.Level;
    }

    /// <summary>Огонь сверху (с крыши, этажа) по нижнему уровню.</summary>
    public bool FromAbove(EntityUid projectile)
    {
        return ShooterHeight(projectile) > 0;
    }

    /// <summary>
    /// Пуля снизу не задевает цель на крыше: та дальше клетки от края, лежит, или (у края) повезло — 40%.
    /// Бросок детерминированный по паре сущностей, чтобы клиент и сервер решали одинаково.
    /// </summary>
    public bool MissesFromBelow(EntityUid target, EntityUid projectile)
    {
        if (ShooterHeight(projectile) >= 0)
            return false;

        var coords = _transform.GetMapCoordinates(target);
        if (!_zLevels.TryGetLevel(coords.MapId, out var level) || level.Level <= 0 || _zLevels.IsAirLevel(coords.MapId))
            return false;

        if (_standing.IsDown(target))
            return true;

        // У края: рядом клетка без пола (видно снизу).
        var nearEdge = false;
        for (var dx = -1; dx <= 1 && !nearEdge; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                var point = new MapCoordinates(coords.Position + new System.Numerics.Vector2(dx, dy), coords.MapId);
                if (!_zLevels.HasFloor(point))
                {
                    nearEdge = true;
                    break;
                }
            }
        }

        if (!nearEdge)
            return true;

        var roll = (int) ((uint) (target.Id * 7919 + projectile.Id * 104729) % 100u);
        return roll < (int) (EdgeMissChance * 100f);
    }
}
