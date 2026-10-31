using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;

namespace Content.Server._DarkestHour.Vehicles;

/// <summary>
/// Боекомплект техники: орудия сами не перезаряжаются, ящики боеприпасов закладывают в механизм заряжания в отсеке.
/// Ящик подходит тому орудию машины, которое стреляет его снарядами.
/// </summary>
public sealed partial class DHVehicleServerSystem
{
    [Dependency] private GunSystem _gun = default!;

    private static readonly SoundSpecifier AmmoLoadSound = new SoundPathSpecifier("/Audio/Weapons/Guns/MagIn/batrifle_magin.ogg");

    private void InitializeAmmo()
    {
        SubscribeLocalEvent<DHVehicleAmmoRackComponent, InteractUsingEvent>(OnRackInteractUsing);
        SubscribeLocalEvent<DHVehicleAmmoRackComponent, DHVehicleAmmoLoadDoAfterEvent>(OnRackLoadDoAfter);
        SubscribeLocalEvent<DHVehicleAmmoRackComponent, ExaminedEvent>(OnRackExamined);
    }

    private void OnRackInteractUsing(Entity<DHVehicleAmmoRackComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<DHVehicleAmmoComponent>(args.Used, out var ammo))
            return;

        args.Handled = true;
        if (FindAmmoTarget(ent, ammo) is not { } target)
        {
            _popup.PopupEntity(Loc.GetString("dh-vehicle-ammo-no-gun"), ent, args.User);
            return;
        }

        if (target.Comp.Count >= target.Comp.Capacity)
        {
            _popup.PopupEntity(Loc.GetString("dh-vehicle-ammo-full", ("gun", target.Owner)), ent, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ammo.LoadTime,
            new DHVehicleAmmoLoadDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnRackLoadDoAfter(Entity<DHVehicleAmmoRackComponent> ent, ref DHVehicleAmmoLoadDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used ||
            !TryComp<DHVehicleAmmoComponent>(used, out var ammo) || FindAmmoTarget(ent, ammo) is not { } target)
        {
            return;
        }

        args.Handled = true;
        var count = target.Comp.Count ?? 0;
        var capacity = target.Comp.Capacity ?? count;
        var loaded = Math.Min(ammo.Amount, capacity - count);
        if (loaded <= 0)
            return;

        _gun.UpdateBasicEntityAmmoCount((target.Owner, target.Comp), count + loaded);
        ammo.Amount -= loaded;
        Dirty(used, ammo);
        if (ammo.Amount <= 0)
            QueueDel(used);

        _audio.PlayPvs(AmmoLoadSound, ent);
        _popup.PopupEntity(Loc.GetString("dh-vehicle-ammo-loaded", ("count", loaded), ("gun", target.Owner)),
            ent, args.User);
    }

    private void OnRackExamined(Entity<DHVehicleAmmoRackComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Vehicle is not { } vehicle || !TryComp<DHVehicleComponent>(vehicle, out var vehicleComp))
            return;

        using (args.PushGroup(nameof(DHVehicleAmmoRackComponent)))
        {
            foreach (var weapon in vehicleComp.WeaponEntities.Values)
            {
                if (!TryComp<BasicEntityAmmoProviderComponent>(weapon, out var provider))
                    continue;

                args.PushMarkup(Loc.GetString("dh-vehicle-ammo-examine", ("gun", weapon),
                    ("count", provider.Count ?? 0), ("capacity", provider.Capacity ?? 0)));
            }
        }
    }

    /// <summary>Орудие машины, которое стреляет снарядами из этого ящика (сначала — не полное).</summary>
    private Entity<BasicEntityAmmoProviderComponent>? FindAmmoTarget(Entity<DHVehicleAmmoRackComponent> rack,
        DHVehicleAmmoComponent ammo)
    {
        if (rack.Comp.Vehicle is not { } vehicle || !TryComp<DHVehicleComponent>(vehicle, out var vehicleComp))
            return null;

        Entity<BasicEntityAmmoProviderComponent>? full = null;
        foreach (var weapon in vehicleComp.WeaponEntities.Values)
        {
            if (!TryComp<BasicEntityAmmoProviderComponent>(weapon, out var provider) || provider.Proto != ammo.Ammo)
                continue;

            if (provider.Count < provider.Capacity)
                return (weapon, provider);

            full = (weapon, provider);
        }

        return full;
    }
}
