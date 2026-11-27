using System.Numerics;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Assault;

/// <summary>
/// Полёт челнока на автопилоте (решение автора 2026-11-18/19, план осады п. 30). Раньше челнок висел в слое атмосферы
/// и по таймеру переставлялся на ЗП. Теперь:
/// <list type="bullet">
/// <item>с авианосца на орбите — переход в атмосферу (<see cref="DHDropshipComponent.TransitTime"/>), если авианосец
/// уже в атмосфере — сразу;</item>
/// <item>вход над полем боя в случайной точке примерно в <see cref="DHDropshipComponent.EntryDistance"/> тайлов от
/// цели и не над городом — на верхнем уровне воздуха (на «Терминусе» +4), полёт к цели со скоростью
/// <see cref="DHDropshipComponent.CruiseSpeed"/>; экипаж видит землю внизу, пилот может взять управление (WASD);</item>
/// <item>над ЗП — постепенное снижение по уровню за <see cref="DHDropshipComponent.LevelStep"/> (4 → 3 → 2 → 1 → 0),
/// если под челноком свободно; если нет — зависает (почему — не сообщается);</item>
/// <item>обратно так же: подъём по уровням, полёт за город, переход на орбиту и стыковка.</item>
/// </list>
/// В воздухе челнок видят РЛС противника и бьёт ПВО; цель можно сменить в полёте.
/// </summary>
public sealed partial class DHDropshipSystem
{
    /// <summary>Запас от края города для точки входа и выхода.</summary>
    private const float CityMargin = 12f;

    /// <summary>Сколько направлений перебирается для точки входа.</summary>
    private const int EntryDirections = 16;

    #region Фазы полёта

    /// <summary>Взлёт по окончании отсчёта.</summary>
    private void Launch(Entity<DHDropshipComponent> ship)
    {
        var comp = ship.Comp;
        var now = _timing.CurTime;

        // С ЗП: подъём по уровням над ней, дальше — к цели. Кабина стоит на стойках (уровень 1); с неподготовленной
        // площадки (посадка вне ЗП) подъём медленнее, временная ЗП убирается.
        if (comp.Zone is { } current && Exists(current) && Transform(current).MapUid is { } here &&
            HasComp<DHPlanetSurfaceMapComponent>(here))
        {
            comp.FieldTakeoff = comp.FieldZone == current;
            if (comp.FieldTakeoff)
            {
                QueueDel(current);
                comp.FieldZone = null;
            }

            comp.State = DHDropshipState.InFlight;
            comp.Zone = null;
            comp.Surface = here;
            if (comp.Level < 0)
                comp.Level = 0;
            comp.CruiseLevel = _aircraft.TopFloor(here, out _) + comp.CruiseHeight;
            comp.Flight = DHDropshipFlight.Climb;
            comp.NextStep = now + ClimbStep(comp);
            SyncDecks(ship);
            comp.NextShake = now;
            _audio.PlayGlobal(LaunchSound, Filter.Empty().AddInGrid(ship), true);
            WarnTarget(ship);
            Announce(ship, Loc.GetString("dh-dropship-launched", ("destination", DestinationName(comp)),
                ("seconds", EstimateSeconds(ship))));
            return;
        }

        // Из ангара: только к ЗП.
        if (comp.ToHangar || comp.Destination is not { } zone || !Exists(zone) ||
            Transform(zone).MapUid is not { } surface ||
            !TryComp<DHPlanetSurfaceMapComponent>(surface, out var surfaceComp) ||
            _landing.EnsureAtmosphere(surfaceComp.System, surfaceComp.Body) is not { } sky)
        {
            comp.State = comp.Zone != null ? DHDropshipState.Landed : DHDropshipState.Docked;
            comp.Destination = null;
            comp.ToHangar = false;
            Announce(ship, Loc.GetString("dh-dropship-zone-unavailable"));
            return;
        }

        _docking.UndockDocks(ship);
        _audio.PlayGlobal(LaunchSound, Filter.Empty().AddInGrid(ship), true);
        comp.State = DHDropshipState.InFlight;
        comp.Zone = null;
        comp.NextShake = now;

        if (CarrierInOrbit(comp))
        {
            // Переход с орбиты: пока он идёт, челнок в небе над точкой входа (там его уже ловит ПВО).
            var entry = EntryPoint(surface, _transform.GetWorldPosition(zone));
            PlaceShip(ship, new EntityCoordinates(sky, entry));
            comp.Flight = DHDropshipFlight.Transit;
            comp.TransitOut = false;
            comp.Surface = surface;
            comp.Level = -1;
            comp.Entry = entry;
            comp.NextStep = now + comp.TransitTime;
        }
        else
        {
            EnterOver(ship, surface);
        }

        WarnTarget(ship);
        Announce(ship, Loc.GetString("dh-dropship-launched", ("destination", DestinationName(comp)),
            ("seconds", EstimateSeconds(ship))));
    }

    /// <summary>Вход над полем боя: верхний уровень воздуха, точка входа, курс на цель.</summary>
    private void EnterOver(Entity<DHDropshipComponent> ship, EntityUid surface)
    {
        var comp = ship.Comp;
        var target = comp.Destination is { } zone && Exists(zone) ? _transform.GetWorldPosition(zone) : Vector2.Zero;
        var entry = comp.Entry ?? EntryPoint(surface, target);
        comp.Entry = null;

        if (_aircraft.AirLevelMap(surface, comp.CruiseHeight) is not { } air)
        {
            // Стопка уровней упёрлась в предел — садимся сразу, как раньше.
            comp.Surface = surface;
            comp.Level = 0;
            TryLand(ship);
            return;
        }

        comp.Surface = surface;
        comp.CruiseLevel = _aircraft.TopFloor(surface, out _) + comp.CruiseHeight;
        comp.Level = comp.CruiseLevel;
        comp.Flight = DHDropshipFlight.Cruise;
        comp.Manual = false;
        PlaceShip(ship, new EntityCoordinates(air, entry));
        _landing.Shake(ship, 0.6f);
        Retarget(ship);
    }

    /// <summary>Курс в зависимости от цели: к ЗП над этой землёй, за город (ангар, другая земля) или висим.</summary>
    private void Retarget(Entity<DHDropshipComponent> ship)
    {
        var comp = ship.Comp;
        if (comp.Flight == DHDropshipFlight.Atmosphere)
        {
            // Выбрали цель, пока шли в слое атмосферы: обратно на высоту полёта, оттуда — по курсу.
            if (comp.Destination == null && !comp.ToHangar)
                return;

            LeaveAtmosphere(ship);
            return;
        }

        if (comp.Flight == DHDropshipFlight.Descend)
        {
            // Снижались к прежней ЗП — сначала обратно наверх.
            comp.Flight = DHDropshipFlight.Climb;
            comp.NextStep = _timing.CurTime + comp.LevelStep;
            return;
        }

        if (comp.Flight != DHDropshipFlight.Cruise || comp.Surface is not { } surface)
            return;

        var position = ShipCenter(ship);
        if (!comp.ToHangar && comp.Destination is { } zone && Exists(zone))
        {
            comp.Waypoint = Transform(zone).MapUid == surface
                ? _transform.GetWorldPosition(zone)
                : ExitPoint(surface, position);
        }
        else if (comp.ToHangar)
        {
            comp.Waypoint = ExitPoint(surface, position);
        }
        else
        {
            comp.Waypoint = null;
        }
    }

    private void UpdateFlight(Entity<DHDropshipComponent> ship, TimeSpan now, float frameTime)
    {
        var comp = ship.Comp;
        switch (comp.Flight)
        {
            case DHDropshipFlight.Transit:
                if (now >= comp.NextStep)
                    EndTransit(ship);
                break;
            case DHDropshipFlight.Cruise:
                Cruise(ship, frameTime, comp.CruiseSpeed);
                break;
            case DHDropshipFlight.Atmosphere:
                // Только руками пилота и медленно: заход для выброски десанта.
                Cruise(ship, frameTime, comp.AtmosphereSpeed);
                break;
            case DHDropshipFlight.Descend:
                if (now >= comp.NextStep)
                {
                    comp.NextStep = now + comp.LevelStep;
                    StepDown(ship);
                }

                break;
            case DHDropshipFlight.Climb:
                if (now >= comp.NextStep)
                {
                    comp.NextStep = now + ClimbStep(comp);
                    StepUp(ship);
                }

                break;
        }
    }

    /// <summary>Полёт на уровне воздуха: автопилот к точке курса или ручное управление пилота.</summary>
    private void Cruise(Entity<DHDropshipComponent> ship, float frameTime, float speed)
    {
        var comp = ship.Comp;
        var position = ShipCenter(ship);
        Vector2? direction = null;
        var step = speed * frameTime;

        if (comp.Manual)
        {
            // Q/E — поворот корпуса вокруг центра (решение автора 2026-11-27).
            if (comp.Pilot is { } turning && TryComp<DHDropshipPilotComponent>(turning, out var pilotKeys))
            {
                var turn = (pilotKeys.RotateLeft ? 1 : 0) - (pilotKeys.RotateRight ? 1 : 0);
                if (turn != 0)
                    RotateShip(ship, Angle.FromDegrees(turn * comp.TurnRate * frameTime));
            }

            if (comp.PilotConsole is { } console && TryComp<InputMoverComponent>(console, out var mover))
            {
                var buttons = mover.HeldMoveButtons;
                var input = Vector2.Zero;
                if ((buttons & MoveButtons.Up) != 0)
                    input.Y += 1f;
                if ((buttons & MoveButtons.Down) != 0)
                    input.Y -= 1f;
                if ((buttons & MoveButtons.Right) != 0)
                    input.X += 1f;
                if ((buttons & MoveButtons.Left) != 0)
                    input.X -= 1f;

                if (input != Vector2.Zero)
                {
                    // Кнопки — по экрану пилота, как его собственная ходьба (поворот грида под ним).
                    var angle = comp.Pilot is { } pilot && TryComp<InputMoverComponent>(pilot, out var pilotMover)
                        ? _mover.GetParentGridAngle(pilotMover)
                        : Angle.Zero;
                    direction = angle.RotateVec(Vector2.Normalize(input));
                }
            }
        }
        else if (comp.Flight == DHDropshipFlight.Cruise && comp.Waypoint is { } waypoint)
        {
            var toWaypoint = waypoint - position;
            var distance = toWaypoint.Length();
            if (distance <= step)
            {
                MoveShip(ship, toWaypoint);
                ReachWaypoint(ship);
                return;
            }

            direction = toWaypoint / distance;
        }

        if (direction is { } dir)
        {
            // Ниже высоты полёта руками — в здания и крыши не влететь.
            if (comp.Manual && comp.Flight == DHDropshipFlight.Cruise && comp.Level < comp.CruiseLevel &&
                comp.Surface is { } surface && LevelMap(surface, comp.Level) is { } here &&
                !Clear(ship, here, dir * step))
            {
                return;
            }

            MoveShip(ship, dir * step);
        }
    }

    private void MoveShip(Entity<DHDropshipComponent> ship, Vector2 delta)
    {
        var xform = Transform(ship);
        _transform.SetWorldPosition((ship.Owner, xform), _transform.GetWorldPosition(xform) + delta);
        SyncDecks(ship);
    }

    /// <summary>Поставить кабину центром в точку (и ярусы вслед за ней).</summary>
    private void PlaceShip(Entity<DHDropshipComponent> ship, EntityCoordinates center)
    {
        _landing.PlaceShuttle(ship, center);
        SyncDecks(ship);
    }

    /// <summary>Повернуть корпус вокруг центра масс.</summary>
    private void RotateShip(Entity<DHDropshipComponent> ship, Angle delta)
    {
        RotateShipTo(ship, _transform.GetWorldRotation(ship) + delta);
    }

    private void RotateShipTo(Entity<DHDropshipComponent> ship, Angle rotation)
    {
        var center = ShipCenter(ship);
        var local = TryComp<PhysicsComponent>(ship, out var physics) ? physics.LocalCenter : Vector2.Zero;
        _transform.SetWorldPositionRotation(ship, center - rotation.RotateVec(local), rotation);
        SyncDecks(ship);
    }

    /// <summary>Перед посадкой корпус выравнивается по сторонам света, чтобы встать по клеткам.</summary>
    private void SnapRotation(Entity<DHDropshipComponent> ship)
    {
        var rotation = _transform.GetWorldRotation(ship);
        var quarter = Math.PI / 2;
        RotateShipTo(ship, new Angle(Math.Round(rotation.Theta / quarter) * quarter));
    }

    /// <summary>Шаг подъёма: с неподготовленной площадки — дольше.</summary>
    private TimeSpan ClimbStep(DHDropshipComponent comp)
    {
        return comp.FieldTakeoff ? comp.LevelStep * comp.FieldTakeoffFactor : comp.LevelStep;
    }

    /// <summary>Долетели до точки курса: над ЗП — снижение, за городом — уход на орбиту (или к авианосцу).</summary>
    private void ReachWaypoint(Entity<DHDropshipComponent> ship)
    {
        var comp = ship.Comp;
        comp.Waypoint = null;
        var now = _timing.CurTime;

        if (!comp.ToHangar && comp.Destination is { } zone && Exists(zone) && Transform(zone).MapUid == comp.Surface)
        {
            comp.Flight = DHDropshipFlight.Descend;
            comp.NextStep = now + comp.LevelStep;
            Announce(ship, Loc.GetString("dh-dropship-descending", ("zone", Name(zone))));
            return;
        }

        if (!comp.ToHangar && comp.Destination == null)
            return;

        // За городом: уход с поля боя.
        if (comp.ToHangar && !CarrierInOrbit(comp))
        {
            FinishAtHangar(ship);
            return;
        }

        var sky = SkyOf(comp.Surface);
        if (sky == null)
        {
            FinishAtHangar(ship);
            return;
        }

        ReleasePilot(ship);
        PlaceShip(ship, new EntityCoordinates(sky.Value, ShipCenter(ship)));
        comp.Flight = DHDropshipFlight.Transit;
        comp.TransitOut = true;
        comp.Level = -1;
        comp.NextStep = now + comp.TransitTime;
        _audio.PlayGlobal(LaunchSound, Filter.Empty().AddInGrid(ship), true);
    }

    private void EndTransit(Entity<DHDropshipComponent> ship)
    {
        var comp = ship.Comp;
        if (comp.ToHangar)
        {
            FinishAtHangar(ship);
            return;
        }

        // К ЗП (в т. ч. на другой земле той же системы): вход над её полем боя.
        if (comp.Destination is { } zone && Exists(zone) && Transform(zone).MapUid is { } surface &&
            HasComp<DHPlanetSurfaceMapComponent>(surface))
        {
            if (comp.TransitOut)
                comp.Entry = null;

            EnterOver(ship, surface);
            return;
        }

        // Цель пропала, пока шёл переход: висим над прежней землёй.
        comp.Destination = null;
        if (comp.Surface is { } previous && Exists(previous))
        {
            EnterOver(ship, previous);
        }
        else
        {
            comp.Flight = DHDropshipFlight.Cruise;
            comp.Waypoint = null;
        }

        Announce(ship, Loc.GetString("dh-dropship-hovering-lost"));
    }

    private void FinishAtHangar(Entity<DHDropshipComponent> ship)
    {
        var comp = ship.Comp;
        ReleasePilot(ship);
        ClearFlight(comp);
        comp.ToHangar = false;
        if (comp.Carrier is { } carrier && Exists(carrier))
        {
            Dock(ship);
            _audio.PlayGlobal(LandSound, Filter.Empty().AddInGrid(ship), true);
            Announce(ship, Loc.GetString("dh-dropship-docked", ("carrier", Name(carrier))));
        }
        else
        {
            Announce(ship, Loc.GetString("dh-dropship-hangar-lost"));
        }
    }

    /// <summary>Снижение на уровень ниже, если под челноком свободно. Нет — висим и пробуем снова.</summary>
    private void StepDown(Entity<DHDropshipComponent> ship)
    {
        var comp = ship.Comp;
        if (comp.Destination is not { } zone || !Exists(zone) || comp.Surface is not { } surface ||
            Transform(zone).MapUid != surface)
        {
            // ЗП пропала: висим на этой высоте, пилот выбирает другую.
            comp.Destination = null;
            comp.Flight = DHDropshipFlight.Climb;
            Announce(ship, Loc.GetString("dh-dropship-hovering-lost"));
            return;
        }

        for (var next = comp.Level - 1; next >= 0; next--)
        {
            // Кабина садится на стойки уровнем выше земли (нижний ярус — на земле).
            if (next <= LandedLevel)
            {
                TryLand(ship);
                return;
            }

            // Пропуск в стопке (нет такого уровня) — проходим сразу ниже.
            if (LevelMap(surface, next) is not { } map)
                continue;

            if (!Clear(ship, map))
                return;

            SetLevel(ship, map, next);
            return;
        }
    }

    /// <summary>Подъём на уровень выше (занятые уровни проходятся насквозь); наверху — курс к цели.</summary>
    private void StepUp(Entity<DHDropshipComponent> ship)
    {
        var comp = ship.Comp;
        if (comp.Surface is not { } surface || !Exists(surface))
        {
            comp.Flight = DHDropshipFlight.Cruise;
            return;
        }

        for (var next = comp.Level + 1; next <= comp.CruiseLevel; next++)
        {
            var map = LevelMap(surface, next);
            if (map == null || next < comp.CruiseLevel && !Clear(ship, map.Value))
                continue;

            SetLevel(ship, map.Value, next);
            if (next >= comp.CruiseLevel)
                break;

            return;
        }

        comp.Flight = DHDropshipFlight.Cruise;
        comp.FieldTakeoff = false;
        Retarget(ship);
    }

    private void SetLevel(Entity<DHDropshipComponent> ship, EntityUid map, int level)
    {
        var xform = Transform(ship);
        var (position, rotation) = _transform.GetWorldPositionRotation(xform);
        _transform.SetCoordinates(ship, xform, new EntityCoordinates(map, position), rotation);
        ship.Comp.Level = level;
        _landing.Shake(ship, 0.25f);
        SyncDecks(ship);
    }

    /// <summary>Посадка на ЗП (с последнего уровня). Занята — висим и пробуем снова.</summary>
    private void TryLand(Entity<DHDropshipComponent> ship)
    {
        var comp = ship.Comp;
        if (comp.Destination is not { } zone || !Exists(zone))
        {
            comp.Destination = null;
            Announce(ship, Loc.GetString("dh-dropship-hovering-lost"));
            return;
        }

        if (!ZoneAvailable(ship, zone, out _))
            return;

        // Кабина — на стойках уровнем выше земли, нижний ярус с рампой — на земле (как «Мидуэй» в CMU-14).
        var zonePosition = _transform.GetWorldPosition(zone);
        var cabinMap = comp.Surface is { } surface && comp.Underside != null ? LevelMap(surface, LandedLevel) : null;
        if (cabinMap is { } blocked && !Clear(ship, blocked))
            return;

        ReleasePilot(ship);
        ClearFlight(comp);
        SnapRotation(ship);
        comp.State = DHDropshipState.Landed;
        comp.Zone = zone;
        comp.Destination = null;
        if (cabinMap is { } upper)
        {
            comp.Level = LandedLevel;
            PlaceShip(ship, new EntityCoordinates(upper, zonePosition));
            GearDown(ship);
        }
        else
        {
            PlaceShip(ship, Transform(zone).Coordinates);
        }

        _landing.Shake(ship, 1f);
        _audio.PlayGlobal(LandSound, Filter.Empty().AddInGrid(ship), true);
        Announce(ship, Loc.GetString(zone == comp.FieldZone ? "dh-dropship-landed-field" : "dh-dropship-landed",
            ("zone", Name(zone))));
    }

    private void ClearFlight(DHDropshipComponent comp)
    {
        comp.Flight = DHDropshipFlight.None;
        comp.Waypoint = null;
        comp.Entry = null;
        comp.Level = -1;
        comp.Manual = false;
    }

    #endregion

    #region Ручное управление

    /// <summary>Пилот берёт челнок на себя (WASD) или отдаёт автопилоту — только в полёте на уровне воздуха.</summary>
    private void OnManual(Entity<DHDropshipConsoleComponent> ent, ref DHDropshipManualMessage args)
    {
        if (ShipOf(ent) is not { } ship)
            return;

        var comp = ship.Comp;
        if (!args.Enable)
        {
            ReleasePilot(ship);
            comp.Manual = false;
            // Опустились руками ниже высоты полёта — автопилот сначала поднимает обратно.
            if (comp.Flight == DHDropshipFlight.Cruise && comp.Level < comp.CruiseLevel)
            {
                comp.Flight = DHDropshipFlight.Climb;
                comp.NextStep = _timing.CurTime + comp.LevelStep;
            }
            // В слое атмосферы автопилота нет: отпустили управление — челнок висит.
            else if (comp.Flight != DHDropshipFlight.Atmosphere)
                Retarget(ship);
            UpdateUi(ent, ship);
            return;
        }

        if (comp.State != DHDropshipState.InFlight ||
            comp.Flight is not (DHDropshipFlight.Cruise or DHDropshipFlight.Atmosphere))
        {
            _popup.PopupEntity(Loc.GetString("dh-dropship-manual-unavailable"), ent, args.Actor);
            return;
        }

        TakeControls(ship, ent.Owner, args.Actor);
        Announce(ship, Loc.GetString("dh-dropship-manual-on"));
        UpdateUi(ent, ship);
    }

    private void TakeControls(Entity<DHDropshipComponent> ship, EntityUid console, EntityUid pilot)
    {
        ReleasePilot(ship);
        ship.Comp.Manual = true;
        ship.Comp.Pilot = pilot;
        ship.Comp.PilotConsole = console;
        _mover.SetRelay(pilot, console);
        EnsureComp<DHDropshipPilotComponent>(pilot).Ship = ship;
    }

    /// <summary>Клавиши пилота: Q/E держат поворот, R/F — уровень выше/ниже.</summary>
    private void OnPilotKey(DHDropshipPilotKey key)
    {
        if (!TryComp<DHDropshipPilotComponent>(key.Pilot, out var pilot) ||
            !TryComp<DHDropshipComponent>(pilot.Ship, out var comp) || comp.Pilot != key.Pilot)
        {
            return;
        }

        if (key.Function == Content.Shared.Input.ContentKeyFunctions.ShuttleRotateLeft)
            pilot.RotateLeft = key.Down;
        else if (key.Function == Content.Shared.Input.ContentKeyFunctions.ShuttleRotateRight)
            pilot.RotateRight = key.Down;
        else if (key.Down && key.Function == DHDropshipKeyFunctions.Ascend)
            ManualStep((pilot.Ship, comp), 1, key.Pilot);
        else if (key.Down && key.Function == DHDropshipKeyFunctions.Descend)
            ManualStep((pilot.Ship, comp), -1, key.Pilot);
    }

    private void OnAltitudeMessage(Entity<DHDropshipConsoleComponent> ent, ref DHDropshipAltitudeMessage args)
    {
        if (ShipOf(ent) is not { } ship)
            return;

        ManualStep(ship, Math.Sign(args.Step), args.Actor);
        UpdateUi(ent, ship);
    }

    /// <summary>
    /// Ручная смена высоты (решение автора 2026-11-27): уровень выше (не выше высоты полёта) или ниже, если там
    /// свободно; ниже первого уровня — посадка прямо здесь.
    /// </summary>
    private void ManualStep(Entity<DHDropshipComponent> ship, int step, EntityUid? user)
    {
        var comp = ship.Comp;
        var now = _timing.CurTime;
        if (step == 0 || !comp.Manual || comp.State != DHDropshipState.InFlight ||
            comp.Flight != DHDropshipFlight.Cruise || comp.Surface is not { } surface || !Exists(surface))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("dh-dropship-altitude-unavailable"), user.Value, user.Value);
            return;
        }

        if (now < comp.NextManualLevel)
            return;

        comp.NextManualLevel = now + comp.ManualLevelCooldown;
        var next = comp.Level + step;
        if (step < 0 && next <= LandedLevel)
        {
            LandHere(ship, user);
            return;
        }

        if (next > comp.CruiseLevel)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("dh-dropship-altitude-max"), user.Value, user.Value);
            return;
        }

        if (LevelMap(surface, next) is not { } map || !Clear(ship, map) ||
            LevelMap(surface, next + 1) is { } above && !Clear(ship, above))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("dh-dropship-altitude-blocked"), user.Value, user.Value);
            return;
        }

        SetLevel(ship, map, next);
    }

    /// <summary>
    /// Посадка вне ЗП: под челноком на первом уровне ничего нет, на земле — ни стен, ни других кораблей. На месте
    /// посадки ставится временная ЗП; взлёт отсюда дольше (<see cref="DHDropshipComponent.FieldTakeoffFactor"/>).
    /// </summary>
    private void LandHere(Entity<DHDropshipComponent> ship, EntityUid? user)
    {
        var comp = ship.Comp;
        if (comp.Surface is not { } surface || comp.Underside == null ||
            LevelMap(surface, LandedLevel) is not { } cabinMap)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("dh-dropship-land-here-blocked"), user.Value, user.Value);
            return;
        }

        SnapRotation(ship);
        var center = ShipCenter(ship);
        if (!Clear(ship, cabinMap) || !_rift.ExitFree(ship, new EntityCoordinates(surface, center)) ||
            !GroundFree(ship, surface))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("dh-dropship-land-here-blocked"), user.Value, user.Value);
            return;
        }

        var zone = _landing.SpawnOnGround(FieldZoneProto, surface, center);
        comp.FieldZone = zone;
        comp.Destination = zone;
        comp.ToHangar = false;
        TryLand(ship);
        if (comp.State != DHDropshipState.Landed)
        {
            // Не сели — временную площадку убрать.
            QueueDel(zone);
            comp.FieldZone = null;
            comp.Destination = null;
        }
    }

    private const string FieldZoneProto = "DHLandingZoneField";

    /// <summary>На земле под кабиной нет стен и прочих закреплённых твёрдых препятствий.</summary>
    private bool GroundFree(Entity<DHDropshipComponent> ship, EntityUid surface)
    {
        if (!TryComp<MapGridComponent>(ship, out var grid) || !TryComp<MapComponent>(surface, out var mapComp))
            return true;

        var tiles = _mapSystem.GetAllTilesEnumerator(ship, grid);
        while (tiles.MoveNext(out var tile))
        {
            var world = _mapSystem.GridTileToWorldPos(ship, grid, tile.Value.GridIndices);
            var box = Box2.CenteredAround(world, new Vector2(0.8f, 0.8f));
            foreach (var uid in _lookup.GetEntitiesIntersecting(mapComp.MapId, box, LookupFlags.Static))
            {
                if (HasComp<MapGridComponent>(uid) || !Transform(uid).Anchored ||
                    !TryComp<PhysicsComponent>(uid, out var physics) || !physics.CanCollide || !physics.Hard)
                {
                    continue;
                }

                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Полёт в слое атмосферы (решение автора 2026-11-19): отдельной кнопкой из полёта над полем боя челнок
    /// поднимается в атмосферу над той же точкой и идёт медленно, только под управлением пилота. Отсюда, как и
    /// раньше, выбрасывается десант и грузы на парашютах. Повторное нажатие или выбор цели — обратно на высоту полёта.
    /// </summary>
    private void OnAtmosphere(Entity<DHDropshipConsoleComponent> ent, ref DHDropshipAtmosphereMessage args)
    {
        if (ShipOf(ent) is not { } ship)
            return;

        var comp = ship.Comp;
        if (!args.Enable)
        {
            if (comp.Flight == DHDropshipFlight.Atmosphere)
            {
                LeaveAtmosphere(ship);
                Announce(ship, Loc.GetString("dh-dropship-atmosphere-left"));
            }

            UpdateUi(ent, ship);
            return;
        }

        if (comp.State != DHDropshipState.InFlight || comp.Flight != DHDropshipFlight.Cruise ||
            SkyOf(comp.Surface) is not { } sky)
        {
            _popup.PopupEntity(Loc.GetString("dh-dropship-atmosphere-unavailable"), ent, args.Actor);
            return;
        }

        PlaceShip(ship, new EntityCoordinates(sky, ShipCenter(ship)));
        comp.Flight = DHDropshipFlight.Atmosphere;
        comp.Level = -1;
        comp.Waypoint = null;
        // Курс сбрасывается: новая цель выводит обратно из атмосферы.
        comp.Destination = null;
        comp.ToHangar = false;
        _landing.Shake(ship, 0.4f);
        TakeControls(ship, ent.Owner, args.Actor);
        Announce(ship, Loc.GetString("dh-dropship-atmosphere-entered"));
        UpdateUi(ent, ship);
    }

    /// <summary>Из слоя атмосферы обратно на высоту полёта над той же точкой, дальше — по курсу.</summary>
    private void LeaveAtmosphere(Entity<DHDropshipComponent> ship)
    {
        var comp = ship.Comp;
        if (comp.Surface is not { } surface || _aircraft.AirLevelMap(surface, comp.CruiseHeight) is not { } air)
            return;

        ReleasePilot(ship);
        comp.Manual = false;
        comp.CruiseLevel = _aircraft.TopFloor(surface, out _) + comp.CruiseHeight;
        comp.Level = comp.CruiseLevel;
        comp.Flight = DHDropshipFlight.Cruise;
        PlaceShip(ship, new EntityCoordinates(air, ShipCenter(ship)));
        _landing.Shake(ship, 0.4f);
        Retarget(ship);
    }

    private void OnShipShutdown(Entity<DHDropshipComponent> ent, ref ComponentShutdown args)
    {
        ReleasePilot(ent);
        ClearHover(ent);
    }

    private void OnConsoleClosed(Entity<DHDropshipConsoleComponent> ent, ref BoundUIClosedEvent args)
    {
        // Пилот отошёл от пульта: челнок висит, пока кто-то снова не возьмёт управление или не включит автопилот.
        if (ShipOf(ent) is { } ship && ship.Comp.Pilot == args.Actor)
            ReleasePilot(ship);
    }

    /// <summary>Вернуть пилоту его ноги. Сам режим (ручной/автопилот) не меняется.</summary>
    private void ReleasePilot(Entity<DHDropshipComponent> ship)
    {
        var comp = ship.Comp;
        if (comp.Pilot is { } pilot && TryComp<RelayInputMoverComponent>(pilot, out var relay) &&
            relay.RelayEntity == comp.PilotConsole)
        {
            RemComp<RelayInputMoverComponent>(pilot);
        }

        if (comp.Pilot is { } keys)
            RemComp<DHDropshipPilotComponent>(keys);

        comp.Pilot = null;
        comp.PilotConsole = null;
    }

    #endregion

    #region Геометрия

    /// <summary>Центр масс челнока в мире.</summary>
    private Vector2 ShipCenter(EntityUid ship)
    {
        var xform = Transform(ship);
        var position = _transform.GetWorldPosition(xform);
        if (TryComp<Robust.Shared.Physics.Components.PhysicsComponent>(ship, out var physics))
            position += _transform.GetWorldRotation(xform).RotateVec(physics.LocalCenter);
        return position;
    }

    /// <summary>Карта уровня стопки поверхности: земля, этаж или воздух (воздух создаётся по требованию).</summary>
    private EntityUid? LevelMap(EntityUid surface, int level)
    {
        if (level == 0)
            return surface;

        var top = _aircraft.TopFloor(surface, out var stack);
        if (level > top)
            return _aircraft.AirLevelMap(surface, level - top);

        return _zLevels.LevelMap(stack, level);
    }

    /// <summary>
    /// Под всеми клетками челнока (со сдвигом <paramref name="offset"/>) на карте уровня нет пола (крыш, этажей,
    /// других челноков). Свои ярусы не мешают.
    /// </summary>
    private bool Clear(Entity<DHDropshipComponent> ship, EntityUid map, Vector2 offset = default)
    {
        if (!TryComp<MapGridComponent>(ship, out var grid) || !TryComp<MapComponent>(map, out var mapComp))
            return true;

        var tiles = _mapSystem.GetAllTilesEnumerator(ship, grid);
        while (tiles.MoveNext(out var tile))
        {
            var world = _mapSystem.GridTileToWorldPos(ship, grid, tile.Value.GridIndices) + offset;
            var point = new MapCoordinates(world, mapComp.MapId);
            if (!_zLevels.HasFloor(point))
                continue;

            if (_mapSystem.TryFindGridAt(point, out var other, out _) &&
                (other == ship.Comp.Roof || other == ship.Comp.Underside || other == ship.Owner))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// Точка входа над полем боя: примерно в <see cref="DHDropshipComponent.EntryDistance"/> от цели и не над городом
    /// (за краем грида поверхности). Из нескольких направлений берётся одно из самых коротких.
    /// </summary>
    private Vector2 EntryPoint(EntityUid surface, Vector2 target)
    {
        return OutsideCity(surface, target, true);
    }

    /// <summary>Точка выхода за город с курсом от <paramref name="from"/>.</summary>
    private Vector2 ExitPoint(EntityUid surface, Vector2 from)
    {
        return OutsideCity(surface, from, false);
    }

    private Vector2 OutsideCity(EntityUid surface, Vector2 from, bool entry)
    {
        // Город — грид самой карты поверхности.
        Box2? city = TryComp<MapGridComponent>(surface, out var grid) && !grid.LocalAABB.IsEmpty()
            ? grid.LocalAABB.Enlarged(CityMargin)
            : null;

        // Точка входа поближе к заданным 200 тайлам; выхода — не дальше, чем нужно, чтобы покинуть город.
        var minimum = entry ? DHDropshipComponent.EntryDistance : DHDropshipComponent.EntryDistance / 4f;
        var maximum = DHDropshipComponent.EntryDistance * 2f;

        var candidates = new List<(Vector2 Direction, float Distance)>();
        var phase = _random.NextFloat(0f, MathF.Tau);
        var best = float.MaxValue;
        for (var i = 0; i < EntryDirections; i++)
        {
            var angle = phase + i * MathF.Tau / EntryDirections;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var distance = minimum;
            if (city is { } box)
                distance = Math.Max(distance, RayExit(box, from, direction));

            distance = Math.Min(distance, maximum);
            candidates.Add((direction, distance));
            best = Math.Min(best, distance);
        }

        candidates.RemoveAll(c => c.Distance > best * 1.15f + 1f);
        var (dir, dist) = _random.Pick(candidates);
        return from + dir * dist;
    }

    /// <summary>Сколько пройти из точки по направлению, чтобы выйти из прямоугольника (0 — уже снаружи).</summary>
    private static float RayExit(Box2 box, Vector2 from, Vector2 direction)
    {
        if (!box.Contains(from))
            return 0f;

        var t = float.MaxValue;
        if (direction.X > 1e-4f)
            t = Math.Min(t, (box.Right - from.X) / direction.X);
        else if (direction.X < -1e-4f)
            t = Math.Min(t, (box.Left - from.X) / direction.X);

        if (direction.Y > 1e-4f)
            t = Math.Min(t, (box.Top - from.Y) / direction.Y);
        else if (direction.Y < -1e-4f)
            t = Math.Min(t, (box.Bottom - from.Y) / direction.Y);

        return t == float.MaxValue ? 0f : t;
    }

    private EntityUid? SkyOf(EntityUid? surface)
    {
        if (surface == null || !TryComp<DHPlanetSurfaceMapComponent>(surface, out var comp))
            return null;

        return _landing.EnsureAtmosphere(comp.System, comp.Body);
    }

    /// <summary>Авианосец на орбите (а не в атмосфере): нужен переход.</summary>
    private bool CarrierInOrbit(DHDropshipComponent comp)
    {
        return comp.Carrier is { } carrier && Exists(carrier) &&
               !HasComp<DHPlanetAtmosphereMapComponent>(Transform(carrier).MapUid);
    }

    private void WarnTarget(Entity<DHDropshipComponent> ship)
    {
        // РЛС противника на карте ЗП замечает челнок на подлёте.
        if (!ship.Comp.ToHangar && ship.Comp.Destination is { } target)
            _guns.WarnIncomingDropship(target, DHAssaultFaction.Empire, Name(ship));
    }

    /// <summary>Сколько секунд примерно до прибытия: переход, полёт, уровни.</summary>
    private int EstimateSeconds(Entity<DHDropshipComponent> ship)
    {
        var comp = ship.Comp;
        var now = _timing.CurTime;
        var seconds = 0.0;
        var levelStep = comp.LevelStep.TotalSeconds;
        var target = !comp.ToHangar && comp.Destination is { } zone && Exists(zone)
            ? _transform.GetWorldPosition(zone)
            : (Vector2?) null;

        switch (comp.Flight)
        {
            case DHDropshipFlight.Transit:
                seconds += Math.Max(0, (comp.NextStep - now).TotalSeconds);
                if (!comp.ToHangar && target is { } t1)
                {
                    var from = comp.Entry ?? ShipCenter(ship);
                    seconds += Vector2.Distance(from, t1) / comp.CruiseSpeed + comp.CruiseHeight * levelStep + levelStep;
                }

                break;
            case DHDropshipFlight.Atmosphere:
                if (target is { } t3)
                    seconds += Vector2.Distance(ShipCenter(ship), t3) / comp.CruiseSpeed + comp.CruiseLevel * levelStep;
                break;
            case DHDropshipFlight.Cruise:
                if (comp.Waypoint is { } waypoint)
                    seconds += Vector2.Distance(ShipCenter(ship), waypoint) / comp.CruiseSpeed;
                if (target != null)
                    seconds += Math.Max(1, comp.Level) * levelStep;
                else if (comp.ToHangar && CarrierInOrbit(comp))
                    seconds += comp.TransitTime.TotalSeconds;
                break;
            case DHDropshipFlight.Descend:
                seconds += Math.Max(0, (comp.NextStep - now).TotalSeconds) + Math.Max(0, comp.Level - 1) * levelStep;
                break;
            case DHDropshipFlight.Climb:
                seconds += Math.Max(0, (comp.NextStep - now).TotalSeconds) +
                           Math.Max(0, comp.CruiseLevel - comp.Level - 1) * levelStep;
                if (target is { } t2)
                    seconds += Vector2.Distance(ShipCenter(ship), t2) / comp.CruiseSpeed + comp.CruiseLevel * levelStep;
                else
                    seconds += DHDropshipComponent.EntryDistance / comp.CruiseSpeed +
                               (CarrierInOrbit(comp) ? comp.TransitTime.TotalSeconds : 0);
                break;
            default:
                // Ещё в ангаре или на ЗП: прикидка всего пути.
                if (comp.Zone != null)
                    seconds += comp.CruiseHeight * levelStep;
                else if (CarrierInOrbit(comp))
                    seconds += comp.TransitTime.TotalSeconds;
                seconds += DHDropshipComponent.EntryDistance / comp.CruiseSpeed + (comp.CruiseHeight + 1) * levelStep;
                break;
        }

        return (int) Math.Ceiling(seconds);
    }

    #endregion
}
