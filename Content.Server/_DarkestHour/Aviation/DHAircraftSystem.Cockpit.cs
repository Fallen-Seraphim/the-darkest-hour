using Content.Shared._DarkestHour.Aviation;
using Content.Shared._DarkestHour.Skills;
using Robust.Shared.Prototypes;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Map;

namespace Content.Server._DarkestHour.Aviation;

/// <summary>
/// Кабина «как криокапсула» (план осады п. 16, решение автора 2026-11-20): клик по самолёту на палубе — пилот за
/// <see cref="DHAircraftComponent.BoardTime"/> залезает внутрь и снаружи не виден, фонарь закрыт и матовый. Способность
/// «Вылезти» — выбраться за <see cref="DHAircraftComponent.ExitTime"/> (только на палубе); сбитый или разбитый самолёт
/// выбрасывает пилота сразу (<see cref="Crash"/>). Способность «Вооружение» открывает управление вооружением.
/// </summary>
public sealed partial class DHAircraftSystem
{
    private static readonly ProtoId<DHSkillPrototype> PilotingSkill = "DHSkillPiloting";

    [Dependency] private DHSkillsSystem _skills = default!;

    private void InitializeCockpit()
    {
        SubscribeLocalEvent<DHAircraftComponent, InteractHandEvent>(OnAircraftInteractHand);
        SubscribeLocalEvent<DHAircraftComponent, DHAircraftBoardDoAfterEvent>(OnBoardDoAfter);

        SubscribeLocalEvent<DHAircraftPilotComponent, DHAircraftWeaponsActionEvent>(OnWeaponsAction);
        SubscribeLocalEvent<DHAircraftPilotComponent, DHAircraftExitActionEvent>(OnExitAction);
        SubscribeLocalEvent<DHAircraftPilotComponent, DHAircraftExitDoAfterEvent>(OnExitDoAfter);
    }

    private bool OnDeck(DHAircraftComponent comp, EntityUid aircraft)
    {
        return comp.State is DHAircraftState.Parked or DHAircraftState.Stowed && Transform(aircraft).GridUid != null;
    }

    private void OnAircraftInteractHand(Entity<DHAircraftComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || ent.Comp.Seat is not { } seat || !HasComp<HandsComponent>(args.User) ||
            HasComp<DHAircraftPilotComponent>(args.User))
        {
            return;
        }

        args.Handled = true;
        if (!OnDeck(ent.Comp, ent))
            return;

        // Штурмовик — только пилотам (навык «Пилотирование», план осады п. 12).
        if (!_skills.HasSkill(args.User, PilotingSkill, 1))
        {
            _skills.PopupMissing(args.User, PilotingSkill, 1);
            return;
        }

        if (Pilot(ent.Comp) != null || !HasComp<StrapComponent>(seat))
        {
            _popup.PopupEntity(Loc.GetString("dh-aircraft-cockpit-occupied"), ent, args.User);
            return;
        }

        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.BoardTime,
            new DHAircraftBoardDoAfterEvent(), ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DistanceThreshold = 4f, // самолёт большой: до центра корпуса от борта далеко
        });

        if (started)
            _popup.PopupEntity(Loc.GetString("dh-aircraft-boarding", ("user", args.User)), ent, PopupType.Small);
    }

    private void OnBoardDoAfter(Entity<DHAircraftComponent> ent, ref DHAircraftBoardDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Seat is not { } seat || !OnDeck(ent.Comp, ent) || Pilot(ent.Comp) != null)
            return;

        // Кресло внутри фюзеляжа: сначала в кабину, потом пристегнуться (пристёгивание проверяет расстояние).
        var user = args.User;
        _transform.SetCoordinates(user, new EntityCoordinates(seat, System.Numerics.Vector2.Zero));
        _transform.AttachToGridOrMap(user);
        if (!_buckle.TryBuckle(user, user, seat, popup: false))
            _popup.PopupEntity(Loc.GetString("dh-aircraft-cockpit-occupied"), ent, user);
    }

    private void OnWeaponsAction(Entity<DHAircraftPilotComponent> ent, ref DHAircraftWeaponsActionEvent args)
    {
        if (args.Handled || !TryComp<DHAircraftComponent>(ent.Comp.Aircraft, out var aircraft))
            return;

        args.Handled = true;
        _ui.TryToggleUi(ent.Comp.Aircraft, DHAircraftUiKey.Weapons, ent);
        UpdateUi((ent.Comp.Aircraft, aircraft));
    }

    private void OnExitAction(Entity<DHAircraftPilotComponent> ent, ref DHAircraftExitActionEvent args)
    {
        if (args.Handled || !TryComp<DHAircraftComponent>(ent.Comp.Aircraft, out var aircraft))
            return;

        args.Handled = true;
        StartExit((ent.Comp.Aircraft, aircraft), ent);
    }

    /// <summary>Вылезти из кабины по таймеру — только на палубе; в воздухе — только катапультироваться.</summary>
    private void StartExit(Entity<DHAircraftComponent> aircraft, EntityUid pilot)
    {
        if (!OnDeck(aircraft.Comp, aircraft))
        {
            _popup.PopupEntity(Loc.GetString("dh-aircraft-cannot-unbuckle"), pilot, pilot);
            return;
        }

        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, pilot, aircraft.Comp.ExitTime,
            new DHAircraftExitDoAfterEvent(), pilot)
        {
            BreakOnMove = false,
            BreakOnDamage = true,
            NeedHand = false,
        });

        if (started)
            _popup.PopupEntity(Loc.GetString("dh-aircraft-exiting"), pilot, pilot);
    }

    private void OnExitDoAfter(Entity<DHAircraftPilotComponent> ent, ref DHAircraftExitDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (TryComp<DHAircraftComponent>(ent.Comp.Aircraft, out var aircraft) && OnDeck(aircraft, ent.Comp.Aircraft))
            _buckle.TryUnbuckle(ent.Owner, ent.Owner, popup: false);
    }
}
