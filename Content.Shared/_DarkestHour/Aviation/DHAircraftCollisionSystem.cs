using Robust.Shared.Physics.Events;

namespace Content.Shared._DarkestHour.Aviation;

/// <summary>
/// Корпус штурмовика на палубе твёрдый (решение автора 2026-11-15: через самолёт нельзя пройти) — кроме пилота,
/// который сидит внутри фюзеляжа. Общий для сервера и клиента, чтобы предсказание не выталкивало пилота из кресла.
/// </summary>
public sealed partial class DHAircraftCollisionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHAircraftComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<DHAircraftComponent> ent, ref PreventCollideEvent args)
    {
        if (TryComp<DHAircraftPilotComponent>(args.OtherEntity, out var pilot) && pilot.Aircraft == ent.Owner)
            args.Cancelled = true;
    }
}
