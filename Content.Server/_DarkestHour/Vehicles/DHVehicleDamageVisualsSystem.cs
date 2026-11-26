using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;

namespace Content.Server._DarkestHour.Vehicles;

/// <summary>Прочность корпуса для слоёв повреждений (<see cref="DHVehicleDamageVisualsComponent"/>).</summary>
public sealed partial class DHVehicleDamageVisualsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHVehicleDamageVisualsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DHVehicleDamageVisualsComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnMapInit(Entity<DHVehicleDamageVisualsComponent> ent, ref MapInitEvent args)
    {
        Refresh(ent);
    }

    private void OnDamageChanged(Entity<DHVehicleDamageVisualsComponent> ent, ref DamageChangedEvent args)
    {
        Refresh(ent);
    }

    private void Refresh(EntityUid uid)
    {
        if (!TryComp<DHVehicleComponent>(uid, out var vehicle) || vehicle.MaxIntegrity <= 0f ||
            !TryComp<DamageableComponent>(uid, out var damageable))
        {
            return;
        }

        var total = _damageable.GetTotalDamage((uid, damageable)).Float();
        _appearance.SetData(uid, DHVehicleDamageVisuals.Hull, DHVehicleDamage.Quantize(1f - total / vehicle.MaxIntegrity));
    }
}
