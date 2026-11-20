using Content.Server._DarkestHour.HeavyGuns;
using Content.Shared._DarkestHour.Atmos.Fire;
using Content.Shared.Trigger;

namespace Content.Server._DarkestHour.Atmos.Fire;

/// <summary>Зажигательные гранаты и прочее: при срабатывании — пожар на клетках вокруг.</summary>
public sealed partial class DHFireOnTriggerSystem : XOnTriggerSystem<DHFireOnTriggerComponent>
{
    [Dependency] private DHHeavyGunSystem _heavyGuns = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    protected override void OnTrigger(Entity<DHFireOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        _heavyGuns.Ignite(_transform.GetMapCoordinates(target), ent.Comp.Radius, ent.Comp.Duration, ent.Comp.Fuel,
            ent.Comp.Chance);
        args.Handled = true;
    }
}
