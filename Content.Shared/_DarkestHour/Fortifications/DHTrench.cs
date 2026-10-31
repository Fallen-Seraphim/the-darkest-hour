using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Projectiles;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._DarkestHour.Fortifications;

/// <summary>
/// Инструмент, которым копают окопы (лопата, садовая лопатка). Отдельный компонент, потому что на
/// <c>ShovelComponent</c> + <c>AfterInteractEvent</c> уже подписан ванильный <c>BotanyShovelSystem</c>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHEntrenchingToolComponent : Component;

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

    /// <summary>Сколько длится спуск в окоп или подъём из него (боец почти стоит на месте).</summary>
    [DataField]
    public TimeSpan ClimbTime = TimeSpan.FromSeconds(1);

    /// <summary>Множитель скорости, пока боец перелезает через бруствер.</summary>
    [DataField]
    public float ClimbSpeedModifier = 0.15f;
}

/// <summary>Повреждённый (наполовину засыпанный) окоп: укрытия не даёт, лопатой расчищается обратно в окоп.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHDamagedTrenchComponent : Component
{
    [DataField]
    public EntProtoId Repaired = "DHTrench";

    [DataField]
    public TimeSpan RepairTime = TimeSpan.FromSeconds(4);
}

/// <summary>Боец у окопов: сколько окопов он сейчас касается и когда закончит перелезать бруствер.</summary>
[RegisterComponent]
public sealed partial class DHTrenchClimberComponent : Component
{
    [ViewVariables]
    public int Contacts;

    [ViewVariables]
    public TimeSpan ClimbEnd;

    [ViewVariables]
    public float SpeedModifier = 1f;

    [ViewVariables]
    public bool Climbing;
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

[Serializable, NetSerializable]
public sealed partial class DHTrenchRepairDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Окоп как укрытие (общая логика, чтобы клиент предсказывал так же).</summary>
public sealed partial class DHTrenchCoverSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHTraumaComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<DHTrenchComponent, StartCollideEvent>(OnTrenchStartCollide);
        SubscribeLocalEvent<DHTrenchComponent, EndCollideEvent>(OnTrenchEndCollide);
        SubscribeLocalEvent<DHTrenchClimberComponent, RefreshMovementSpeedModifiersEvent>(OnClimberRefreshSpeed);
    }

    #region Перелезание через бруствер

    // Спуск в окоп и подъём из него занимают время, как перелезание через перила: в момент, когда боец входит
    // в первый окоп с ровной земли (или выходит из последнего), он ненадолго почти останавливается. Ходить по
    // траншее из окопа в соседний — без задержки (касания соседних окопов перекрываются).

    private void OnTrenchStartCollide(Entity<DHTrenchComponent> ent, ref StartCollideEvent args)
    {
        if (!HasComp<MobStateComponent>(args.OtherEntity))
            return;

        var climber = EnsureComp<DHTrenchClimberComponent>(args.OtherEntity);
        climber.Contacts++;
        if (climber.Contacts == 1)
            StartClimb((args.OtherEntity, climber), ent.Comp);
    }

    private void OnTrenchEndCollide(Entity<DHTrenchComponent> ent, ref EndCollideEvent args)
    {
        if (!TryComp<DHTrenchClimberComponent>(args.OtherEntity, out var climber))
            return;

        climber.Contacts = Math.Max(0, climber.Contacts - 1);
        if (climber.Contacts == 0 && !TerminatingOrDeleted(ent))
            StartClimb((args.OtherEntity, climber), ent.Comp);
    }

    private void StartClimb(Entity<DHTrenchClimberComponent> climber, DHTrenchComponent trench)
    {
        climber.Comp.ClimbEnd = _timing.CurTime + trench.ClimbTime;
        climber.Comp.SpeedModifier = trench.ClimbSpeedModifier;
        climber.Comp.Climbing = true;
        _speed.RefreshMovementSpeedModifiers(climber);
    }

    private void OnClimberRefreshSpeed(Entity<DHTrenchClimberComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Climbing)
            args.ModifySpeed(ent.Comp.SpeedModifier, ent.Comp.SpeedModifier);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHTrenchClimberComponent>();
        while (query.MoveNext(out var uid, out var climber))
        {
            if (!climber.Climbing || now < climber.ClimbEnd)
                continue;

            climber.Climbing = false;
            _speed.RefreshMovementSpeedModifiers(uid);
        }
    }

    #endregion

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
