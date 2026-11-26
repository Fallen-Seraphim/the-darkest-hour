using Content.Server._DarkestHour.Assault;
using Content.Server._DarkestHour.Supply;
using Content.Shared._DarkestHour.Paradrop;
using Content.Shared._DarkestHour.Vehicles;
using Robust.Shared.Map;

namespace Content.Server._DarkestHour.Vehicles;

/// <summary>
/// Доставка заказанной техники своим ходом (план осады п. 3, решение автора 2026-11-19): хамви Республики пригоняют
/// в гараж базы, а не поднимают лифтом снабжения.
/// </summary>
public sealed partial class DHVehicleServerSystem
{
    [Dependency] private DHAssaultRuleSystem _assault = default!;
    [Dependency] private DHVehicleLiftSystem _vehicleLift = default!;

    private void InitializeDelivery()
    {
        SubscribeLocalEvent<DHVehicleDeliveryComponent, MapInitEvent>(OnDeliveryMapInit);
    }

    private void OnDeliveryMapInit(Entity<DHVehicleDeliveryComponent> ent, ref MapInitEvent args)
    {
        // Заказ — в трюм лифта техники своей стороны (решение автора 2026-11-26).
        if (ent.Comp.Lift is { } kind && _vehicleLift.FindLift(ent.Comp.Faction, kind) is { } lift)
        {
            var stored = Spawn(ent.Comp.Spawn, MapCoordinates.Nullspace);
            var name = TryComp<DHDropPalletComponent>(stored, out var pallet) &&
                       _proto.TryIndex(pallet.Spawn, out var vehicleProto)
                ? vehicleProto.Name
                : Name(stored);
            _vehicleLift.Store(lift, stored);
            _assault.AnnounceToFaction(ent.Comp.Faction, Loc.GetString("dh-vehicle-delivery-sender"),
                Loc.GetString("dh-vehicle-delivery-lift", ("vehicle", name)));
            QueueDel(ent);
            return;
        }

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
