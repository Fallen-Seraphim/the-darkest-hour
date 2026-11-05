using System.Numerics;
using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Server._DarkestHour.Vehicles;

/// <summary>
/// Буксировка: орудие (пехотная пушка, зенитка) цепляют к грузовику действием в меню; пока прицеплено, оно едет за
/// машиной на сцепке — тянется за ней на постоянном расстоянии и разворачивается следом. Отцепить — тем же меню;
/// сцепка рвётся, если орудие закрепили ключом, кто-то начал тянуть его руками или машина уехала далеко (другая карта).
/// </summary>
public sealed partial class DHVehicleTowSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>Дальше этого от машины — сцепка рвётся (телепорт, смена карты).</summary>
    private const float BreakDistance = 8f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHHitchableComponent, GetVerbsEvent<Verb>>(OnHitchVerbs);
        SubscribeLocalEvent<DHHitchableComponent, ComponentShutdown>(OnHitchableShutdown);
        SubscribeLocalEvent<DHVehicleTowComponent, ComponentShutdown>(OnTowShutdown);
    }

    private void OnHitchVerbs(Entity<DHHitchableComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        if (ent.Comp.Vehicle != null)
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("dh-tow-unhitch-verb"),
                Act = () =>
                {
                    Unhitch(ent);
                    _popup.PopupEntity(Loc.GetString("dh-tow-unhitched", ("gun", ent.Owner)), ent, user);
                },
            });
            return;
        }

        var xform = Transform(ent);
        if (xform.Anchored || FindVehicle(ent) is not { } vehicle)
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("dh-tow-hitch-verb"),
            Act = () =>
            {
                if (Transform(ent).Anchored || ent.Comp.Vehicle != null || vehicle.Comp.Towed != null)
                    return;

                ent.Comp.Vehicle = vehicle;
                vehicle.Comp.Towed = ent;
                _popup.PopupEntity(Loc.GetString("dh-tow-hitched", ("gun", ent.Owner), ("vehicle", vehicle.Owner)),
                    ent, user);
            },
        });
    }

    /// <summary>Свободная машина с фаркопом рядом с орудием.</summary>
    private Entity<DHVehicleTowComponent>? FindVehicle(EntityUid gun)
    {
        var position = _transform.GetMapCoordinates(gun);
        var query = EntityQueryEnumerator<DHVehicleTowComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tow, out var xform))
        {
            if (tow.Towed != null || xform.MapID != position.MapId)
                continue;

            if (Vector2.Distance(_transform.GetWorldPosition(xform), position.Position) <= tow.HitchRange)
                return (uid, tow);
        }

        return null;
    }

    private void Unhitch(Entity<DHHitchableComponent> ent)
    {
        if (ent.Comp.Vehicle is { } vehicle && TryComp<DHVehicleTowComponent>(vehicle, out var tow) &&
            tow.Towed == ent.Owner)
        {
            tow.Towed = null;
        }

        ent.Comp.Vehicle = null;
    }

    private void OnHitchableShutdown(Entity<DHHitchableComponent> ent, ref ComponentShutdown args)
    {
        Unhitch(ent);
    }

    private void OnTowShutdown(Entity<DHVehicleTowComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Towed is { } towed && TryComp<DHHitchableComponent>(towed, out var hitchable))
            hitchable.Vehicle = null;

        ent.Comp.Towed = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DHHitchableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var hitch, out var xform))
        {
            if (hitch.Vehicle is not { } vehicle)
                continue;

            if (TerminatingOrDeleted(vehicle) || !TryComp<DHVehicleTowComponent>(vehicle, out var tow) ||
                xform.Anchored || TryComp<PullableComponent>(uid, out var pullable) && pullable.BeingPulled)
            {
                Unhitch((uid, hitch));
                continue;
            }

            var vehicleXform = Transform(vehicle);
            if (vehicleXform.MapID != xform.MapID)
            {
                Unhitch((uid, hitch));
                continue;
            }

            var vehiclePos = _transform.GetWorldPosition(vehicleXform);
            var gunPos = _transform.GetWorldPosition(xform);
            var offset = gunPos - vehiclePos;
            var distance = offset.Length();
            if (distance > BreakDistance)
            {
                Unhitch((uid, hitch));
                continue;
            }

            // Сцепка: держится на постоянном расстоянии — тянется за машиной и заходит за неё на поворотах.
            if (distance < 0.01f)
                continue;

            var direction = offset / distance;
            if (MathF.Abs(distance - tow.Distance) > 0.05f)
                _transform.SetWorldPosition((uid, xform), vehiclePos + direction * tow.Distance);

            // Лафет смотрит от машины (ствол назад), как у прицепа.
            _transform.SetWorldRotation(uid, direction.ToWorldAngle());
        }
    }
}
