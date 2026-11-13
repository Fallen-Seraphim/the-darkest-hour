using System.Numerics;
using Content.Shared._DarkestHour.Aviation;
using Robust.Shared.Audio;
using Robust.Shared.Map;

namespace Content.Server._DarkestHour.Aviation;

/// <summary>
/// Бреющий заход (этап 2, решение автора 2026-11-12): пилот выбирает на тактической карте линию захода (начало и конец,
/// правым кликом), самолёт снижается из атмосферы на поле боя, проходит по линии и снова уходит в атмосферу; заход
/// тратит много топлива. Высота бреющего (вместо Z-уровней: +2, +3, +4) влияет на точность пушки и бомб и на опасность
/// ПВО. Пушка 30 мм, если включена, бьёт по курсу; фугасная бомба сбрасывается с узла подвески с упреждением.
/// </summary>
public sealed partial class DHAircraftSystem
{
    private const string HeavyExplosion = "DHArtillery";
    private const string CannonExplosion = "Default";

    private readonly List<PendingBomb> _bombs = new();

    private readonly record struct PendingBomb(MapCoordinates Target, TimeSpan Impact, DHAircraftMunition Kind, Vector2 Direction);

    /// <summary>Разброс очередей пушки, клеток.</summary>
    private static float CannonSpread(DHAircraftAltitude altitude) => altitude switch
    {
        DHAircraftAltitude.Low => 0.8f,
        DHAircraftAltitude.Medium => 1.6f,
        _ => 2.6f,
    };

    /// <summary>Рассеивание бомб, клеток.</summary>
    private static float BombScatter(DHAircraftAltitude altitude) => altitude switch
    {
        DHAircraftAltitude.Low => 1f,
        DHAircraftAltitude.Medium => 2.5f,
        _ => 4.5f,
    };

    /// <summary>Время падения бомбы, с (самолёт за это время пролетает упреждение).</summary>
    private static float BombFall(DHAircraftAltitude altitude) => altitude switch
    {
        DHAircraftAltitude.Low => 0.8f,
        DHAircraftAltitude.Medium => 1.3f,
        _ => 1.9f,
    };

    /// <summary>Насколько ПВО опаснее (или безопаснее) на этой высоте.</summary>
    private static float AirDefenceFactor(DHAircraftAltitude altitude) => altitude switch
    {
        DHAircraftAltitude.Low => 1.6f,
        DHAircraftAltitude.Medium => 1f,
        _ => 0.55f,
    };

    private Vector2 RandomInDisc(float radius)
    {
        var angle = new Angle(_random.NextFloat() * MathF.Tau);
        return angle.RotateVec(new Vector2(MathF.Sqrt(_random.NextFloat()) * radius, 0f));
    }

    private static Angle HeadingOf(Vector2 direction)
    {
        // Нос = Heading.RotateVec((0, 1)) = (-sin, cos).
        return new Angle(MathF.Atan2(-direction.X, direction.Y));
    }

    #region Линия захода и запуск

    private void OnPassPoint(Entity<DHAircraftComponent> ent, ref DHAircraftPassPointMessage args)
    {
        if (!IsPilot(ent, args.Actor))
            return;

        if (ent.Comp.State is not (DHAircraftState.Airborne or DHAircraftState.Returning))
        {
            Notify(ent, Loc.GetString("dh-aircraft-pass-blocker-state"));
            return;
        }

        if (ent.Comp.PassStart == null || ent.Comp.PassEnd != null)
        {
            ent.Comp.PassStart = args.Point;
            ent.Comp.PassEnd = null;
        }
        else if (Vector2.Distance(ent.Comp.PassStart.Value, args.Point) < ent.Comp.MinPassLength)
        {
            Notify(ent, Loc.GetString("dh-aircraft-pass-too-short", ("min", (int) ent.Comp.MinPassLength)));
            return;
        }
        else
        {
            ent.Comp.PassEnd = args.Point;
        }

        Dirty(ent);
        UpdateUi(ent);
    }

    private void OnClearPass(Entity<DHAircraftComponent> ent, ref DHAircraftClearPassMessage args)
    {
        if (!IsPilot(ent, args.Actor) || ent.Comp.State == DHAircraftState.Pass)
            return;

        ent.Comp.PassStart = null;
        ent.Comp.PassEnd = null;
        Dirty(ent);
        UpdateUi(ent);
    }

    /// <summary>Почему нельзя начать заход (ключ локализации) или null.</summary>
    private string? PassBlocker(Entity<DHAircraftComponent> ent)
    {
        if (ent.Comp.State is not (DHAircraftState.Airborne or DHAircraftState.Returning))
            return "dh-aircraft-pass-blocker-state";
        if (ent.Comp.PassStart == null || ent.Comp.PassEnd == null)
            return "dh-aircraft-pass-blocker-route";
        if (GetFuel(ent, out _) < ent.Comp.PassFuel + 10f)
            return "dh-aircraft-pass-blocker-fuel";
        if (_tacmap.GetSurfaceMap() == null)
            return "dh-aircraft-pass-blocker-surface";
        return null;
    }

    private void OnStartPass(Entity<DHAircraftComponent> ent, ref DHAircraftStartPassMessage args)
    {
        if (!IsPilot(ent, args.Actor))
            return;

        if (PassBlocker(ent) is { } blocker)
        {
            Notify(ent, Loc.GetString(blocker));
            return;
        }

        if (_tacmap.GetSurfaceMap() is not { } surface || ent.Comp.PassStart is not { } start || ent.Comp.PassEnd is not { } end)
            return;

        BurnFuel(ent, ent.Comp.PassFuel);
        ent.Comp.PassSky = Transform(ent).MapUid;

        var heading = HeadingOf(Vector2.Normalize(end - start));
        ent.Comp.Heading = heading;
        ent.Comp.PassSurface = surface;
        // Высота настоящая: самолёт — на уровне воздуха над поверхностью, на земле — его тень.
        _transform.SetCoordinates(ent, new EntityCoordinates(AirMap(surface, ent.Comp.Altitude) ?? surface, start));
        _transform.SetWorldRotation(ent, heading);
        SpawnShadow(ent, surface, start);
        ent.Comp.State = DHAircraftState.Pass;
        ent.Comp.NextBurst = _timing.CurTime;
        Dirty(ent);

        var passSound = ent.Comp.Altitude == DHAircraftAltitude.High ? ent.Comp.PassHighSound : ent.Comp.PassSound;
        _audio.PlayPvs(passSound, ent, AudioParams.Default.WithMaxDistance(60f).WithVolume(4f));
        if (ent.Comp.PassShadow is { } shadow)
            _audio.PlayPvs(passSound, shadow, AudioParams.Default.WithMaxDistance(60f).WithVolume(4f));
        Notify(ent, Loc.GetString("dh-aircraft-pass-started"));
        UpdateUi(ent);
    }

    #endregion

    #region Высота, пушка, сброс

    private void OnAltitude(Entity<DHAircraftComponent> ent, ref DHAircraftAltitudeMessage args)
    {
        if (!IsPilot(ent, args.Actor))
            return;

        var level = Math.Clamp((int) ent.Comp.Altitude + Math.Sign(args.Delta), (int) DHAircraftAltitude.Low, (int) DHAircraftAltitude.High);
        ent.Comp.Altitude = (DHAircraftAltitude) level;
        Dirty(ent);
        if (ent.Comp.State == DHAircraftState.Pass)
            MoveToAltitude(ent);
        Notify(ent, Loc.GetString("dh-aircraft-altitude-" + ent.Comp.Altitude.ToString().ToLowerInvariant()));
        UpdateUi(ent);
    }

    private void OnCannonAction(Entity<DHAircraftPilotComponent> ent, ref DHAircraftCannonActionEvent args)
    {
        if (args.Handled || !TryComp<DHAircraftComponent>(ent.Comp.Aircraft, out var aircraft))
            return;

        args.Handled = true;
        SetCannon((ent.Comp.Aircraft, aircraft), !aircraft.CannonOn);
    }

    private void OnCannonMessage(Entity<DHAircraftComponent> ent, ref DHAircraftCannonMessage args)
    {
        if (IsPilot(ent, args.Actor))
            SetCannon(ent, args.On);
    }

    private void SetCannon(Entity<DHAircraftComponent> ent, bool on)
    {
        if (on && ent.Comp.CannonPods == 0)
        {
            Notify(ent, Loc.GetString("dh-aircraft-cannon-no-pod"), true);
            on = false;
        }
        else if (on && !CanBurst(ent.Comp))
        {
            Notify(ent, Loc.GetString("dh-aircraft-cannon-empty"), true);
            on = false;
        }

        ent.Comp.CannonOn = on;
        Dirty(ent);
        Notify(ent, Loc.GetString(on ? "dh-aircraft-cannon-on" : "dh-aircraft-cannon-off"));
        UpdateUi(ent);
    }

    private void OnDropAction(Entity<DHAircraftPilotComponent> ent, ref DHAircraftDropActionEvent args)
    {
        if (args.Handled || !TryComp<DHAircraftComponent>(ent.Comp.Aircraft, out var aircraft))
            return;

        args.Handled = true;
        Drop((ent.Comp.Aircraft, aircraft));
    }

    private void OnDropMessage(Entity<DHAircraftComponent> ent, ref DHAircraftDropMessage args)
    {
        if (IsPilot(ent, args.Actor))
            Drop(ent);
    }

    /// <summary>Сбросить бомбу с первого заряженного узла: на бреющем, с упреждением по курсу и рассеиванием.</summary>
    private void Drop(Entity<DHAircraftComponent> ent)
    {
        if (ent.Comp.State != DHAircraftState.Pass)
        {
            Notify(ent, Loc.GetString("dh-aircraft-drop-not-pass"));
            return;
        }

        // Сначала выбранный узел, если на нём свободнопадающая бомба, иначе — первый такой.
        var selected = ent.Comp.SelectedHardpoint;
        var slot = selected >= 0 && selected < ent.Comp.Hardpoints.Count && IsFreeFall(ent.Comp.Hardpoints[selected])
            ? selected
            : ent.Comp.Hardpoints.FindIndex(IsFreeFall);
        if (slot < 0)
        {
            Notify(ent, Loc.GetString("dh-aircraft-drop-empty"), true);
            return;
        }

        var xform = Transform(ent);
        var fall = BombFall(ent.Comp.Altitude);
        var nose = ent.Comp.Heading.RotateVec(Vector2.UnitY);
        var target = _transform.GetWorldPosition(xform) + nose * ent.Comp.PassSpeed * fall +
                     RandomInDisc(BombScatter(ent.Comp.Altitude));

        var kind = ent.Comp.Hardpoints[slot];
        ent.Comp.Hardpoints[slot] = DHAircraftMunition.None;
        Dirty(ent);
        _bombs.Add(new PendingBomb(new MapCoordinates(target, GroundMapId(ent)), _timing.CurTime + TimeSpan.FromSeconds(fall),
            kind, nose));
        Notify(ent, Loc.GetString("dh-aircraft-drop-released", ("slot", slot + 1)));
        UpdateUi(ent);
    }

    private void UpdateBombs(TimeSpan now)
    {
        for (var i = _bombs.Count - 1; i >= 0; i--)
        {
            var bomb = _bombs[i];
            if (now < bomb.Impact)
                continue;

            _bombs.RemoveAt(i);
            Detonate(bomb.Kind, bomb.Target, bomb.Direction, null);
        }
    }

    #endregion

    #region Проход

    private void UpdatePass(Entity<DHAircraftComponent> ent, float frameTime, TimeSpan now)
    {
        var comp = ent.Comp;
        if (comp.PassStart is not { } start || comp.PassEnd is not { } end)
        {
            EndPass(ent);
            return;
        }

        if (!BurnFuel(ent, comp.FuelPerSecond * (1f + comp.Speed) * frameTime))
        {
            Notify(ent, Loc.GetString("dh-aircraft-no-fuel"), true);
            Crash(ent, ejected: false);
            return;
        }

        var xform = Transform(ent);
        var direction = Vector2.Normalize(end - start);
        var position = _transform.GetWorldPosition(xform) + direction * comp.PassSpeed * frameTime;
        _transform.SetWorldPosition(ent, position);
        MoveShadow(ent, position);

        if (comp.CannonOn && now >= comp.NextBurst)
        {
            comp.NextBurst = now + comp.CannonInterval;
            if (!CanBurst(comp))
            {
                comp.CannonOn = false;
                Dirty(ent);
                Notify(ent, Loc.GetString("dh-aircraft-cannon-empty"), true);
            }
            else
            {
                FireBurst(ent, position, direction, GroundMapId(ent), now);
            }
        }

        if (Vector2.Dot(end - position, direction) <= 0f)
            EndPass(ent);
    }

    /// <summary>Хотя бы в одном пушечном контейнере хватает снарядов на очередь.</summary>
    private static bool CanBurst(DHAircraftComponent comp)
    {
        for (var i = 0; i < comp.Hardpoints.Count; i++)
        {
            if (comp.Hardpoints[i] == DHAircraftMunition.CannonPod && PodAmmo(comp, i) >= comp.RoundsPerBurst)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Очередь 30 мм: каждый заряженный контейнер даёт свою очередь впереди по курсу, со смещением в сторону своего крыла
    /// (узлы 1–2 — левое крыло, 3–4 — правое), с разбросом по высоте.
    /// </summary>
    private void FireBurst(Entity<DHAircraftComponent> ent, Vector2 position, Vector2 direction, MapId map, TimeSpan now)
    {
        var comp = ent.Comp;
        var right = new Vector2(direction.Y, -direction.X);
        Vector2? lastTarget = null;

        for (var i = 0; i < comp.Hardpoints.Count; i++)
        {
            if (comp.Hardpoints[i] != DHAircraftMunition.CannonPod || PodAmmo(comp, i) < comp.RoundsPerBurst)
                continue;

            SetPodAmmo(comp, i, PodAmmo(comp, i) - comp.RoundsPerBurst);
            var side = i < comp.Hardpoints.Count / 2 ? -1f : 1f;
            var target = position + direction * comp.CannonLead + right * side * 1.5f + RandomInDisc(CannonSpread(comp.Altitude));
            _explosion.QueueExplosion(new MapCoordinates(target, map), CannonExplosion, 14f, 7f, 7f, null, canCreateVacuum: false);
            lastTarget = target;
        }

        Dirty(ent);
        if (lastTarget is not { } impact || now < comp.NextCannonSound)
            return;

        comp.NextCannonSound = now + TimeSpan.FromSeconds(1);
        _audio.PlayPvs(comp.CannonSound, ent, AudioParams.Default.WithMaxDistance(40f));
        if (_map.TryGetMap(map, out var mapUid))
            _audio.PlayPvs(comp.CannonImpactSound, new EntityCoordinates(mapUid.Value, impact));
    }

    /// <summary>Конец линии захода: набор высоты обратно в слой атмосферы.</summary>
    private void EndPass(Entity<DHAircraftComponent> ent)
    {
        var comp = ent.Comp;
        var position = _transform.GetWorldPosition(Transform(ent));
        comp.CannonOn = false;
        comp.PassStart = null;
        comp.PassEnd = null;
        comp.PassSurface = null;
        RemoveShadow(ent);

        if (comp.PassSky is not { } sky || TerminatingOrDeleted(sky))
        {
            Notify(ent, Loc.GetString("dh-aircraft-no-sky"), true);
            Crash(ent, ejected: false);
            return;
        }

        _transform.SetCoordinates(ent, new EntityCoordinates(sky, position));
        _transform.SetWorldRotation(ent, comp.Heading);
        comp.State = DHAircraftState.Airborne;
        Dirty(ent);
        SetControl(ent, true);
        Notify(ent, Loc.GetString("dh-aircraft-pass-complete"));
    }

    /// <summary>Штурмовик на бреющем над ПВО (та же карта поверхности, в пределах дальности, без ЛТЦ) и множитель опасности по высоте.</summary>
    public (EntityUid Aircraft, float Factor)? FindPassTarget(EntityUid? map, Vector2 position, float range)
    {
        if (map == null)
            return null;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHAircraftComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.State != DHAircraftState.Pass || GroundMapUid(comp, xform) != map || now < comp.FlaresUntil ||
                Vector2.Distance(_transform.GetWorldPosition(xform), position) > range)
            {
                continue;
            }

            return (uid, AirDefenceFactor(comp.Altitude));
        }

        return null;
    }

    #endregion
}
