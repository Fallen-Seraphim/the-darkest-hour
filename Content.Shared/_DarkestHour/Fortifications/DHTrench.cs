using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Fortifications;

/// <summary>
/// Окоп (решение автора 2026-10-30): копается лопатой на грунте, засыпается обратно. Сидящего в окопе пули издалека
/// задевают только с шансом, взрывы снаружи окопа бьют слабее. Соседние окопы соединяются в траншею.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHTrenchComponent : Component
{
    /// <summary>Шанс, что пуля издалека пролетит над бойцом в окопе.</summary>
    [DataField]
    public float BulletMissChance = 0.5f;

    /// <summary>Стрелок ближе этого (в метрах) бьёт в окоп без промаха.</summary>
    [DataField]
    public float PointBlankRange = 2.5f;

    /// <summary>Множитель урона от взрыва, если взрыв не в окопе (рядом с ним).</summary>
    [DataField]
    public float ExplosionCoefficient = 0.35f;

    /// <summary>Сколько тайлов от эпицентра считается «взрыв в этом окопе».</summary>
    [DataField]
    public float ExplosionInsideRange = 1.5f;

    [DataField]
    public TimeSpan FillTime = TimeSpan.FromSeconds(4);
}

[Serializable, NetSerializable]
public sealed partial class DHTrenchDigDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetCoordinates Coordinates;

    private DHTrenchDigDoAfterEvent()
    {
    }

    public DHTrenchDigDoAfterEvent(NetCoordinates coordinates)
    {
        Coordinates = coordinates;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class DHTrenchFillDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Окоп как укрытие (общая логика, чтобы клиент предсказывал так же).</summary>
public sealed partial class DHTrenchCoverSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHTraumaComponent, PreventCollideEvent>(OnPreventCollide);
    }

    /// <summary>Окоп на тайле, где стоит сущность.</summary>
    public Entity<DHTrenchComponent>? TrenchAt(EntityCoordinates coordinates)
    {
        if (_transform.GetGrid(coordinates) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return null;

        var tile = _map.TileIndicesFor(gridUid, grid, coordinates);
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (TryComp<DHTrenchComponent>(uid, out var trench))
                return (uid.Value, trench);
        }

        return null;
    }

    public Entity<DHTrenchComponent>? TrenchOf(EntityUid uid)
    {
        return TrenchAt(Transform(uid).Coordinates);
    }

    private void OnPreventCollide(Entity<DHTraumaComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || !TryComp<ProjectileComponent>(args.OtherEntity, out var projectile))
            return;

        if (TrenchOf(ent) is not { } trench)
            return;

        // Вплотную (или из этого же окопа) — попадание как обычно.
        if (projectile.Shooter is { } shooter && Exists(shooter) &&
            _transform.InRange(Transform(shooter).Coordinates, Transform(ent).Coordinates, trench.Comp.PointBlankRange))
        {
            return;
        }

        // Детерминированный бросок по паре сущностей — клиент и сервер решают одинаково.
        var roll = (int) ((uint) (ent.Owner.Id * 7919 + args.OtherEntity.Id * 104729) % 100u);
        if (roll < (int) (trench.Comp.BulletMissChance * 100f))
            args.Cancelled = true;
    }
}
