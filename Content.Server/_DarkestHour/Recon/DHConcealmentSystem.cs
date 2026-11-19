using System.Numerics;
using Content.Server._DarkestHour.Assault;
using Content.Server._DarkestHour.Carrier;
using Content.Server._DarkestHour.HeavyGuns;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Carrier;
using Content.Shared._DarkestHour.HeavyGuns;
using Content.Shared._DarkestHour.Recon;
using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Recon;

/// <summary>Выстрел миномёта или полевого орудия (для контрбатарейной засечки и маскировочных сетей).</summary>
public sealed class DHArtilleryFiredEvent : EntityEventArgs
{
    public readonly EntityUid Gun;
    public readonly DHAssaultFaction? Faction;

    public DHArtilleryFiredEvent(EntityUid gun, DHAssaultFaction? faction)
    {
        Gun = gun;
        Faction = faction;
    }
}

/// <summary>
/// Маскировочные сети и контрбатарейная засечка (план осады п. 9, решения автора 2026-11-19).
/// <list type="bullet">
/// <item>техника и орудия под сетью, пока стоят и не стреляют, не дают сигнатур на РЛС штурмовика и РЛС Республики
/// и не видны пилоту сверху; тронулись или выстрелили — видны ~20 с;</item>
/// <item>огонь миномёта или полевого орудия засекается противником: без сети — с первого выстрела, под сетью — со
/// второго подряд. Отметка — круг радиусом ~5 клеток, где-то в нём орудие; держится 30 с, новые выстрелы её
/// обновляют. Республика засекает через РЛС (пока та работает), Империя — штабом авианосца (пока мостик цел).</item>
/// </list>
/// </summary>
public sealed partial class DHConcealmentSystem : EntitySystem
{
    [Dependency] private DHAssaultRuleSystem _assault = default!;
    [Dependency] private DHCarrierDamageSystem _carrierDamage = default!;
    [Dependency] private DHHeavyGunSystem _guns = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>Радиус круга засечки, клеток.</summary>
    public const float CounterBatteryRadius = 5f;

    private static readonly TimeSpan CounterBatteryLifetime = TimeSpan.FromSeconds(30);

    /// <summary>Выстрелы одного орудия считаются серией, если между ними не больше этого.</summary>
    private static readonly TimeSpan SeriesGap = TimeSpan.FromSeconds(60);

    /// <summary>Сколько цель видна после движения или выстрела, хоть и под сетью.</summary>
    private static readonly TimeSpan RevealTime = TimeSpan.FromSeconds(20);

    /// <summary>Скорость, с которой орудие или машина считаются движущимися, м/с.</summary>
    private const float MovingSpeed = 0.2f;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextUpdate;

    private readonly Dictionary<EntityUid, (int Shots, TimeSpan Last)> _series = new();
    private readonly Dictionary<EntityUid, TimeSpan> _revealedUntil = new();
    private readonly List<CounterBatteryMark> _marks = new();

    public sealed class CounterBatteryMark
    {
        /// <summary>Сторона, которая засекла (ей и показывается).</summary>
        public DHAssaultFaction Detector;

        public EntityUid Gun;
        public EntityUid? Map;
        public Vector2 Center;
        public TimeSpan Until;
    }

    /// <summary>Действующие засечки.</summary>
    public IReadOnlyList<CounterBatteryMark> Marks => _marks;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHArtilleryFiredEvent>(OnArtilleryFired);
    }

    #region Сети

    private List<(MapId Map, Vector2 Position, float Radius)> Nets()
    {
        var nets = new List<(MapId, Vector2, float)>();
        var query = EntityQueryEnumerator<DHCamoNetComponent, TransformComponent>();
        while (query.MoveNext(out _, out var net, out var xform))
        {
            nets.Add((xform.MapID, _transform.GetWorldPosition(xform), net.Radius));
        }

        return nets;
    }

    private static bool UnderNet(List<(MapId Map, Vector2 Position, float Radius)> nets, MapId map, Vector2 position)
    {
        foreach (var (netMap, netPosition, radius) in nets)
        {
            if (netMap == map && Vector2.Distance(netPosition, position) <= radius)
                return true;
        }

        return false;
    }

    /// <summary>Под сетью ли сейчас точка.</summary>
    public bool UnderNet(MapId map, Vector2 position)
    {
        return UnderNet(Nets(), map, position);
    }

    private void UpdateNets(TimeSpan now)
    {
        var nets = Nets();

        var vehicles = EntityQueryEnumerator<DHVehicleComponent, TransformComponent>();
        while (vehicles.MoveNext(out var uid, out var vehicle, out var xform))
        {
            var active = TryComp<DHGridDriveComponent>(uid, out var drive) && drive.IsMoving || Moving(uid);
            foreach (var weapon in vehicle.WeaponEntities.Values)
            {
                if (TryComp<GunComponent>(weapon, out var gun) && gun.NextFire > now - RevealTime)
                    active = true;
            }

            Apply(uid, xform, active, nets, now);
        }

        var guns = EntityQueryEnumerator<DHScanTargetComponent, TransformComponent>();
        while (guns.MoveNext(out var uid, out var target, out var xform))
        {
            if (target.AlwaysVisible || HasComp<DHVehicleComponent>(uid))
                continue;

            Apply(uid, xform, Moving(uid), nets, now);
        }

        var gone = new List<EntityUid>();
        foreach (var (uid, until) in _revealedUntil)
        {
            if (until < now || TerminatingOrDeleted(uid))
                gone.Add(uid);
        }

        foreach (var uid in gone)
        {
            _revealedUntil.Remove(uid);
        }
    }

    private bool Moving(EntityUid uid)
    {
        return TryComp<PhysicsComponent>(uid, out var physics) && physics.LinearVelocity.Length() > MovingSpeed;
    }

    private void Apply(EntityUid uid, TransformComponent xform, bool active, List<(MapId, Vector2, float)> nets,
        TimeSpan now)
    {
        if (active)
            _revealedUntil[uid] = now + RevealTime;

        var hidden = (!_revealedUntil.TryGetValue(uid, out var until) || until <= now) &&
                     UnderNet(nets, xform.MapID, _transform.GetWorldPosition(xform));

        if (hidden)
            EnsureComp<DHCamoHiddenComponent>(uid);
        else
            RemComp<DHCamoHiddenComponent>(uid);
    }

    #endregion

    #region Контрбатарейная засечка

    private void OnArtilleryFired(DHArtilleryFiredEvent ev)
    {
        var now = _timing.CurTime;
        var gun = ev.Gun;
        _revealedUntil[gun] = now + RevealTime;
        RemComp<DHCamoHiddenComponent>(gun);

        if (ev.Faction is not { } shooter)
            return;

        var shots = _series.TryGetValue(gun, out var previous) && now - previous.Last <= SeriesGap
            ? previous.Shots + 1
            : 1;
        _series[gun] = (shots, now);

        var xform = Transform(gun);
        var position = _transform.GetWorldPosition(xform);
        if (shots < (UnderNet(xform.MapID, position) ? 2 : 1))
            return;

        var detector = shooter == DHAssaultFaction.Empire ? DHAssaultFaction.Republic : DHAssaultFaction.Empire;
        if (!CanDetect(detector))
            return;

        foreach (var mark in _marks)
        {
            if (mark.Gun != gun || mark.Detector != detector)
                continue;

            mark.Until = now + CounterBatteryLifetime;
            return;
        }

        // Центр круга — со сдвигом, но так, чтобы орудие оставалось внутри.
        var center = position + _random.NextAngle().ToVec() * _random.NextFloat(0f, CounterBatteryRadius * 0.8f);
        _marks.Add(new CounterBatteryMark
        {
            Detector = detector,
            Gun = gun,
            Map = xform.MapUid,
            Center = center,
            Until = now + CounterBatteryLifetime,
        });

        _assault.AnnounceToFaction(detector, Loc.GetString("dh-counter-battery-sender"),
            Loc.GetString("dh-counter-battery-announce", ("x", (int) MathF.Round(center.X)),
                ("y", (int) MathF.Round(center.Y)), ("radius", (int) CounterBatteryRadius)));
    }

    /// <summary>
    /// Может ли сторона засекать огонь: Республика — пока работает её РЛС (точка РЛС, питание), Империя — пока цел
    /// мостик и штаб авианосца.
    /// </summary>
    public bool CanDetect(DHAssaultFaction faction)
    {
        if (faction == DHAssaultFaction.Republic)
        {
            var radars = EntityQueryEnumerator<DHRadarComponent>();
            while (radars.MoveNext(out var uid, out var radar))
            {
                if (radar.Faction == DHAssaultFaction.Republic && _guns.RadarWorking(uid, radar))
                    return true;
            }

            return false;
        }

        var found = false;
        var carriers = EntityQueryEnumerator<DHCarrierCompartmentsComponent>();
        while (carriers.MoveNext(out var grid, out _))
        {
            found = true;
            if (!_carrierDamage.Damaged(grid, DHCarrierCompartmentKind.Bridge))
                return true;
        }

        return !found;
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + Interval;
        _marks.RemoveAll(m => m.Until <= now);

        var stale = new List<EntityUid>();
        foreach (var (gun, series) in _series)
        {
            if (now - series.Last > SeriesGap || TerminatingOrDeleted(gun))
                stale.Add(gun);
        }

        foreach (var gun in stale)
        {
            _series.Remove(gun);
        }

        UpdateNets(now);
    }
}
