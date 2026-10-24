using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Server.RoundEnd;
using Content.Server.Station.Events;
using Content.Shared._DarkestHour.Orbit;
using Content.Shared.AlertLevel;
using Content.Shared.Atmos.Components;
using Content.Shared.Station.Components;
using Content.Shared.Station.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Orbit;

/// <summary>
/// Орбита станции: высота, снижение, коррекция манёвровыми двигателями, автоматические коды угрозы «Терринум» и
/// «Экс», удары обломков по высоте орбиты. Цифры подобраны на глаз — меняются в компоненте станции.
/// </summary>
[RegisterComponent]
public sealed partial class DHStationOrbitComponent : Component
{
    /// <summary>Тело, над которым висит станция.</summary>
    [DataField]
    public LocId Body = "dh-celestial-rep27-5d";

    /// <summary>Высота орбиты, км.</summary>
    [DataField]
    public float Altitude = 300f;

    /// <summary>Снижение на исходной высоте, км в минуту.</summary>
    [DataField]
    public float BaseDecay = 2.5f;

    /// <summary>Высота, на которой снижение равно базовому; ниже — растёт (плотнее атмосфера).</summary>
    [DataField]
    public float ReferenceAltitude = 300f;

    /// <summary>Наибольшая высота, на которую можно поднять станцию.</summary>
    [DataField]
    public float MaxAltitude = 400f;

    /// <summary>Ниже — код «Терринум» (траектория ведёт к падению, но исправима).</summary>
    [DataField]
    public float TerrinumAltitude = 150f;

    /// <summary>Ниже — код «Экс»: орбита неисправима.</summary>
    [DataField]
    public float ExAltitude = 100f;

    /// <summary>Запас над порогом «Терринум», после которого код снимается.</summary>
    [DataField]
    public float RecoveryMargin = 20f;

    /// <summary>От кода «Экс» до падения.</summary>
    [DataField]
    public TimeSpan ImpactDelay = TimeSpan.FromMinutes(10);

    [DataField]
    public bool Burning;

    [DataField]
    public float Throttle = 1f;

    [ViewVariables]
    public float LastRatePerMinute;

    [ViewVariables]
    public float LastMolesPerSecond;

    /// <summary>Подъём, накопленный двигателями с прошлого тика системы.</summary>
    [ViewVariables]
    public float PendingLift;

    [ViewVariables]
    public bool Lost;

    [ViewVariables]
    public TimeSpan? ImpactAt;

    /// <summary>Код «Терринум» поставлен системой орбиты (и ею же снимается).</summary>
    [ViewVariables]
    public bool TerrinumSet;

    [ViewVariables]
    public TimeSpan NextUpdate;

    [ViewVariables]
    public TimeSpan NextDebris;

    [ViewVariables]
    public bool Impacted;
}

public sealed partial class DHOrbitSystem : EntitySystem
{
    [Dependency] private AlertLevelSystem _alert = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private NodeContainerSystem _nodes = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public static readonly ProtoId<AlertLevelPrototype> Terrinum = "DHTerrinum";
    public static readonly ProtoId<AlertLevelPrototype> Ex = "DHEx";
    public static readonly ProtoId<AlertLevelPrototype> Ellow = "DHEllow";
    public static readonly ProtoId<AlertLevelPrototype> Indomitus = "DHIndomitus";
    public static readonly ProtoId<AlertLevelPrototype> Deadend = "DHDeadend";
    public static readonly ProtoId<AlertLevelPrototype> DeadendRevolution = "DHDeadendRevolution";

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
    private static readonly EntProtoId[] Debris = { "MeteorSpaceDust", "MeteorSmall", "MeteorSmall", "MeteorMedium" };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationPostInitEvent>(OnStationPostInit);
        SubscribeLocalEvent<DHOrbitalThrusterComponent, AtmosDeviceUpdateEvent>(OnThrusterUpdate);

        Subs.BuiEvents<DHOrbitConsoleComponent>(DHOrbitUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<DHOrbitSetBurnMessage>(OnSetBurn);
            subs.Event<DHOrbitSetThrottleMessage>(OnSetThrottle);
        });
    }

    private void OnStationPostInit(ref StationPostInitEvent ev)
    {
        // только станции с экипажем (не ЦентКом и не корабли)
        if (!HasComp<StationJobsComponent>(ev.Station))
            return;

        var orbit = EnsureComp<DHStationOrbitComponent>(ev.Station);
        orbit.NextDebris = _timing.CurTime + TimeSpan.FromMinutes(5);
    }

    #region Двигатели

    private void OnThrusterUpdate(Entity<DHOrbitalThrusterComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        var burning = false;
        ent.Comp.LastMolesPerSecond = 0f;

        if (_station.GetOwningStation(ent) is { } station &&
            TryComp<DHStationOrbitComponent>(station, out var orbit) &&
            orbit.Burning && !orbit.Lost && orbit.Altitude < orbit.MaxAltitude &&
            Transform(ent).Anchored &&
            (!TryComp<ApcPowerReceiverComponent>(ent, out var power) || power.Powered) &&
            _nodes.TryGetNode(ent.Owner, ent.Comp.InletNode, out PipeNode? inlet) &&
            inlet.Air.TotalMoles > 0.01f)
        {
            var want = ent.Comp.MaxMolesPerSecond * Math.Clamp(orbit.Throttle, 0f, 1f) * args.dt;
            var burned = inlet.Air.Remove(MathF.Min(want, inlet.Air.TotalMoles));
            var moles = burned.TotalMoles;
            if (moles > 0f)
            {
                orbit.PendingLift += moles * ent.Comp.LiftPerMole;
                ent.Comp.LastMolesPerSecond = moles / MathF.Max(args.dt, 0.001f);
                burning = true;
            }
        }

        _appearance.SetData(ent, DHOrbitalThrusterVisuals.Burning, burning);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHStationOrbitComponent>();
        while (query.MoveNext(out var station, out var orbit))
        {
            if (now < orbit.NextUpdate || orbit.Impacted)
                continue;

            var dt = orbit.NextUpdate == TimeSpan.Zero ? 1f : (float) (now - orbit.NextUpdate + UpdateInterval).TotalSeconds;
            orbit.NextUpdate = now + UpdateInterval;
            UpdateOrbit((station, orbit), dt, now);
        }
    }

    private void UpdateOrbit(Entity<DHStationOrbitComponent> ent, float dt, TimeSpan now)
    {
        var orbit = ent.Comp;

        // Карта без компьютера орбиты ещё не переделана под орбиту — высоту не трогаем.
        if (!HasOrbitConsole(ent.Owner))
            return;
        var before = orbit.Altitude;

        // снижение: ниже опорной высоты атмосфера плотнее — тормозит сильнее
        var density = 1f + MathF.Max(0f, orbit.ReferenceAltitude - orbit.Altitude) / 150f;
        var decay = orbit.BaseDecay / 60f * density * dt;
        if (orbit.Lost)
            decay *= 3f;

        orbit.Altitude = MathF.Min(orbit.MaxAltitude, orbit.Altitude - decay + (orbit.Lost ? 0f : orbit.PendingLift));
        orbit.PendingLift = 0f;
        orbit.LastRatePerMinute = (orbit.Altitude - before) / dt * 60f;

        // суммарный расход двигателей станции
        var moles = 0f;
        var thrusters = EntityQueryEnumerator<DHOrbitalThrusterComponent>();
        while (thrusters.MoveNext(out var uid, out var thruster))
        {
            if (_station.GetOwningStation(uid) == ent.Owner)
                moles += thruster.LastMolesPerSecond;
        }
        orbit.LastMolesPerSecond = moles;

        UpdateAlerts(ent, now);
        UpdateDebris(ent, now);
        UpdateConsoles(ent);
    }

    private bool HasOrbitConsole(EntityUid station)
    {
        var consoles = EntityQueryEnumerator<DHOrbitConsoleComponent>();
        while (consoles.MoveNext(out var uid, out _))
        {
            if (_station.GetOwningStation(uid) == station)
                return true;
        }

        return false;
    }

    #region Коды угрозы

    private void UpdateAlerts(Entity<DHStationOrbitComponent> ent, TimeSpan now)
    {
        var orbit = ent.Comp;

        if (!orbit.Lost && orbit.Altitude <= orbit.ExAltitude)
        {
            orbit.Lost = true;
            orbit.ImpactAt = now + orbit.ImpactDelay;
            _alert.SetLevel(ent.Owner, Ex, true, true, true, true);
            Announce(ent, "dh-orbit-announce-ex", Color.DarkRed);
        }

        if (orbit.Lost)
        {
            if (orbit.ImpactAt is { } impact && now >= impact)
            {
                orbit.Impacted = true;
                Announce(ent, "dh-orbit-announce-impact", Color.DarkRed);
                _roundEnd.EndRound(TimeSpan.FromSeconds(30));
            }
            return;
        }

        _alert.TryGetLevel(ent.Owner, out var current);
        if (!orbit.TerrinumSet && orbit.Altitude <= orbit.TerrinumAltitude)
        {
            orbit.TerrinumSet = true;
            // Внешние коды (Индомитус, Деаденд) орбита не перебивает
            if (current != Ex && current != Indomitus && current != Deadend && current != DeadendRevolution)
                _alert.SetLevel(ent.Owner, Terrinum, true, true, true, false);
            Announce(ent, "dh-orbit-announce-terrinum", Color.FromHex("#2F6FDB"));
        }
        else if (orbit.TerrinumSet && orbit.Altitude >= orbit.TerrinumAltitude + orbit.RecoveryMargin)
        {
            orbit.TerrinumSet = false;
            if (current == Terrinum)
                _alert.SetLevel(ent.Owner, Ellow, true, true, true, false);
            Announce(ent, "dh-orbit-announce-recovered", Color.FromHex("#3FBF3F"));
        }
    }

    private void Announce(Entity<DHStationOrbitComponent> ent, string key, Color color)
    {
        _chat.DispatchStationAnnouncement(ent.Owner,
            Loc.GetString(key, ("altitude", (int) ent.Comp.Altitude)),
            Loc.GetString("dh-orbit-announce-sender"),
            true,
            null,
            color);
    }

    #endregion

    #region Обломки

    /// <summary>
    /// Чем ниже орбита, тем чаще обломки: на исходной высоте — редкая пыль, у порога «Терринум» — каждые пару минут.
    /// </summary>
    private void UpdateDebris(Entity<DHStationOrbitComponent> ent, TimeSpan now)
    {
        var orbit = ent.Comp;
        if (now < orbit.NextDebris)
            return;

        var danger = Math.Clamp((orbit.ReferenceAltitude - orbit.Altitude) / (orbit.ReferenceAltitude - orbit.ExAltitude), 0f, 1f);
        var interval = MathHelper.Lerp(600f, 90f, danger);
        orbit.NextDebris = now + TimeSpan.FromSeconds(interval * _random.NextFloat(0.6f, 1.4f));

        if (_station.GetLargestGrid(ent.Owner) is not { } grid)
            return;

        var count = 1 + (int) MathF.Round(danger * 3f);
        SpawnDebris(grid, count, danger);
    }

    private void SpawnDebris(EntityUid grid, int count, float danger)
    {
        var mapId = Transform(grid).MapID;
        var area = _physics.GetWorldAABB(grid);
        var minDistance = (area.TopRight - area.Center).Length() + 50f;
        var angle = _random.NextAngle();

        for (var i = 0; i < count; i++)
        {
            var proto = Debris[Math.Min(Debris.Length - 1, _random.Next((int) (danger * Debris.Length) + 1))];
            var offset = angle.RotateVec(new Vector2(minDistance + _random.NextFloat(0f, 60f), 0f));
            var side = new Angle(angle.Theta + Math.PI / 2).RotateVec(new Vector2(_random.NextFloat(-30f, 30f), 0f));
            var meteor = Spawn(proto, new MapCoordinates(area.Center + offset + side, mapId));
            if (TryComp<PhysicsComponent>(meteor, out var physics))
                _physics.ApplyLinearImpulse(meteor, -offset.Normalized() * 10f * physics.Mass, body: physics);
        }
    }

    #endregion

    #region Компьютер

    private void OnUiOpened(Entity<DHOrbitConsoleComponent> ent, ref BoundUIOpenedEvent args) => UpdateConsole(ent);

    private void OnSetBurn(Entity<DHOrbitConsoleComponent> ent, ref DHOrbitSetBurnMessage args)
    {
        if (GetOrbit(ent) is not { } orbit)
            return;

        orbit.Comp.Burning = args.Burning;
        UpdateConsoles(orbit);
    }

    private void OnSetThrottle(Entity<DHOrbitConsoleComponent> ent, ref DHOrbitSetThrottleMessage args)
    {
        if (GetOrbit(ent) is not { } orbit)
            return;

        orbit.Comp.Throttle = Math.Clamp(args.Throttle, 0f, 1f);
        UpdateConsoles(orbit);
    }

    private Entity<DHStationOrbitComponent>? GetOrbit(EntityUid uid)
    {
        if (_station.GetOwningStation(uid) is not { } station || !TryComp<DHStationOrbitComponent>(station, out var orbit))
            return null;
        return (station, orbit);
    }

    private void UpdateConsoles(Entity<DHStationOrbitComponent> orbit)
    {
        var consoles = EntityQueryEnumerator<DHOrbitConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            if (_station.GetOwningStation(uid) == orbit.Owner && _ui.IsUiOpen(uid, DHOrbitUiKey.Key))
                UpdateConsole((uid, console));
        }
    }

    private void UpdateConsole(Entity<DHOrbitConsoleComponent> ent)
    {
        if (GetOrbit(ent) is not { } orbit)
        {
            _ui.SetUiState(ent.Owner, DHOrbitUiKey.Key,
                new DHOrbitConsoleState(string.Empty, 0f, 0f, 0f, 0f, 0f, false, null, 0, 0, false, 0f, 0f));
            return;
        }

        var o = orbit.Comp;
        var working = 0;
        var total = 0;
        var thrusters = EntityQueryEnumerator<DHOrbitalThrusterComponent>();
        while (thrusters.MoveNext(out var uid, out var thruster))
        {
            if (_station.GetOwningStation(uid) != orbit.Owner)
                continue;
            total++;
            if (Transform(uid).Anchored && (!TryComp<ApcPowerReceiverComponent>(uid, out var power) || power.Powered))
                working++;
        }

        TimeSpan? impactIn = o.ImpactAt is { } at ? at - _timing.CurTime : null;
        _ui.SetUiState(ent.Owner, DHOrbitUiKey.Key, new DHOrbitConsoleState(Loc.GetString(o.Body), o.Altitude, o.MaxAltitude, o.LastRatePerMinute,
            o.TerrinumAltitude, o.ExAltitude, o.Lost, impactIn, working, total, o.Burning, o.Throttle,
            o.LastMolesPerSecond));
    }

    #endregion

    /// <summary>Высота орбиты станции (для админов, событий и других систем).</summary>
    public void SetAltitude(EntityUid station, float altitude)
    {
        if (TryComp<DHStationOrbitComponent>(station, out var orbit))
            orbit.Altitude = altitude;
    }
}
