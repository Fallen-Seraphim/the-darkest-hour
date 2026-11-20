using Content.Shared._DarkestHour.Aviation;
using Content.Shared._DarkestHour.Supply;
using Content.Shared.Examine;
using Content.Shared.Interaction;

namespace Content.Server._DarkestHour.Aviation;

/// <summary>
/// Компьютер лифта штурмовика (план осады п. 4, решение автора 2026-11-19): одна кнопка — «Поднять» самолёт из
/// ангара на палубу. Опускают, как раньше, действием по самому лифту.
/// </summary>
public sealed partial class DHAircraftSystem
{
    [Dependency] private Content.Server.Power.EntitySystems.PowerReceiverSystem _liftPower = default!;

    private void InitializeLiftConsole()
    {
        SubscribeLocalEvent<DHAircraftLiftConsoleComponent, ActivateInWorldEvent>(OnLiftConsoleActivate);
        SubscribeLocalEvent<DHAircraftLiftConsoleComponent, ExaminedEvent>(OnLiftConsoleExamined);
    }

    private Entity<DHAircraftLiftComponent>? ConsoleLift(Entity<DHAircraftLiftConsoleComponent> console)
    {
        var xform = Transform(console);
        Entity<DHAircraftLiftComponent>? best = null;
        var bestDistance = console.Comp.LiftRange;
        var query = EntityQueryEnumerator<DHAircraftLiftComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var lift, out var liftXform))
        {
            if (liftXform.GridUid != xform.GridUid || liftXform.GridUid == null)
                continue;

            var distance = (liftXform.LocalPosition - xform.LocalPosition).Length();
            if (distance > bestDistance)
                continue;

            best = (uid, lift);
            bestDistance = distance;
        }

        return best;
    }

    private void OnLiftConsoleActivate(Entity<DHAircraftLiftConsoleComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        if (!_liftPower.IsPowered(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("dh-aircraft-lift-console-unpowered"), ent, args.User);
            return;
        }

        if (ConsoleLift(ent) is not { } lift)
        {
            _popup.PopupEntity(Loc.GetString("dh-aircraft-lift-console-no-lift"), ent, args.User);
            return;
        }

        SyncLift(lift);
        switch (lift.Comp.Mode)
        {
            case DHAircraftLiftMode.Lowered:
                TryRaise(lift, args.User);
                if (lift.Comp.Mode == DHAircraftLiftMode.Raising)
                    _popup.PopupEntity(Loc.GetString("dh-aircraft-lift-console-raising"), ent, args.User);
                break;
            case DHAircraftLiftMode.Raised:
                _popup.PopupEntity(Loc.GetString("dh-aircraft-lift-console-raised"), ent, args.User);
                break;
            default:
                _popup.PopupEntity(Loc.GetString("dh-aircraft-lift-console-moving"), ent, args.User);
                break;
        }
    }

    private void OnLiftConsoleExamined(Entity<DHAircraftLiftConsoleComponent> ent, ref ExaminedEvent args)
    {
        if (ConsoleLift(ent) is { } lift)
            args.PushMarkup(Loc.GetString("dh-aircraft-lift-examine-" + lift.Comp.Mode.ToString().ToLowerInvariant()));
    }
}
