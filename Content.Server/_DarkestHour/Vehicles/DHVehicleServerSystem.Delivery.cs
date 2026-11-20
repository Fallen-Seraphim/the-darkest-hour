using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Vehicles;

namespace Content.Server._DarkestHour.Vehicles;

/// <summary>
/// Доставка заказанной техники своим ходом (план осады п. 3, решение автора 2026-11-19): хамви Республики пригоняют
/// в гараж базы, а не поднимают лифтом снабжения.
/// </summary>
public sealed partial class DHVehicleServerSystem
{
    [Dependency] private DHAssaultRuleSystem _assault = default!;

    private void InitializeDelivery()
    {
        SubscribeLocalEvent<DHVehicleDeliveryComponent, MapInitEvent>(OnDeliveryMapInit);
    }

    private void OnDeliveryMapInit(Entity<DHVehicleDeliveryComponent> ent, ref MapInitEvent args)
    {
        var coordinates = Transform(ent).Coordinates;
        var rotation = Angle.Zero;
        var points = EntityQueryEnumerator<DHVehicleDeliveryPointComponent, TransformComponent>();
        while (points.MoveNext(out _, out var point, out var xform))
        {
            if (point.Faction != ent.Comp.Faction)
                continue;

            coordinates = xform.Coordinates;
            rotation = xform.LocalRotation;
            break;
        }

        var vehicle = Spawn(ent.Comp.Spawn, coordinates);
        _transform.SetLocalRotation(vehicle, rotation);
        _assault.AnnounceToFaction(ent.Comp.Faction, Loc.GetString("dh-vehicle-delivery-sender"),
            Loc.GetString("dh-vehicle-delivery-arrived", ("vehicle", vehicle)));
        QueueDel(ent);
    }
}
