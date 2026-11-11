using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;

namespace Content.Server._DarkestHour.Vehicles;

/// <summary>
/// Экипаж и десант, как в CM-SS13 / RMC: замок люков изнутри, гудок и фары у механика-водителя, смотровые приборы
/// в отсеке (посмотреть наружу, пока не отойдёшь).
/// </summary>
public sealed partial class DHVehicleServerSystem
{
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private PointLightSystem _light = default!;

    private void InitializeCrew()
    {
        SubscribeLocalEvent<DHVehicleHatchComponent, GetVerbsEvent<AlternativeVerb>>(OnHatchVerbs);
        SubscribeLocalEvent<DHVehicleComponent, DHVehicleHornActionEvent>(OnHorn);
        SubscribeLocalEvent<DHVehicleComponent, DHVehicleLightsActionEvent>(OnLights);
        SubscribeLocalEvent<DHVehiclePeriscopeComponent, InteractHandEvent>(OnPeriscopeInteract);
        SubscribeLocalEvent<DHVehicleViewerComponent, MoveEvent>(OnViewerMoved);
        SubscribeLocalEvent<DHVehicleViewerComponent, ComponentShutdown>(OnViewerShutdown);
    }

    #region Замок люков

    private void OnHatchVerbs(Entity<DHVehicleHatchComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Vehicle is not { } vehicle ||
            !TryComp<DHVehicleComponent>(vehicle, out var comp))
        {
            return;
        }

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(comp.Locked ? "dh-vehicle-verb-unlock" : "dh-vehicle-verb-lock"),
            Icon = new SpriteSpecifier.Texture(new ResPath(comp.Locked
                ? "/Textures/Interface/VerbIcons/unlock.svg.192dpi.png"
                : "/Textures/Interface/VerbIcons/lock.svg.192dpi.png")),
            Act = () =>
            {
                comp.Locked = !comp.Locked;
                Dirty(vehicle, comp);
                _popup.PopupEntity(Loc.GetString(comp.Locked ? "dh-vehicle-locked-now" : "dh-vehicle-unlocked-now"),
                    ent, user);
            },
        });
    }

    #endregion

    #region Гудок и фары

    private void OnHorn(Entity<DHVehicleComponent> ent, ref DHVehicleHornActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _audio.PlayPvs(ent.Comp.HornSound, ent);
    }

    private void OnLights(Entity<DHVehicleComponent> ent, ref DHVehicleLightsActionEvent args)
    {
        if (args.Handled || !TryComp<PointLightComponent>(ent, out var light))
            return;

        args.Handled = true;
        _light.SetEnabled(ent, !light.Enabled, light);
    }

    #endregion

    #region Смотровые приборы

    private void OnPeriscopeInteract(Entity<DHVehiclePeriscopeComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (TryComp<DHVehicleViewerComponent>(args.User, out _))
        {
            RemComp<DHVehicleViewerComponent>(args.User);
            return;
        }

        // Прибор берёт машину отсека, в котором стоит.
        var vehicle = ent.Comp.Vehicle;
        if (vehicle == null && Transform(ent).GridUid is { } grid &&
            TryComp<DHVehicleInteriorComponent>(grid, out var interior))
        {
            vehicle = interior.Vehicle;
        }

        if (vehicle is not { } target || TerminatingOrDeleted(target))
            return;

        var viewer = EnsureComp<DHVehicleViewerComponent>(args.User);
        viewer.Vehicle = target;
        _eye.SetTarget(args.User, target);
        _popup.PopupEntity(Loc.GetString("dh-vehicle-periscope-look"), ent, args.User);
    }

    /// <summary>Отошёл от прибора — взгляд вернулся в отсек.</summary>
    private void OnViewerMoved(Entity<DHVehicleViewerComponent> ent, ref MoveEvent args)
    {
        if (args.OldPosition.EntityId == args.NewPosition.EntityId &&
            (args.OldPosition.Position - args.NewPosition.Position).LengthSquared() < 0.0001f)
        {
            return;
        }

        RemCompDeferred<DHVehicleViewerComponent>(ent);
    }

    private void OnViewerShutdown(Entity<DHVehicleViewerComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        // У стрелка и водителя взгляд ведёт пульт — его не трогаем.
        if (HasComp<DHVehicleCrewComponent>(ent))
            return;

        _eye.SetTarget(ent, null);
    }

    #endregion
}
