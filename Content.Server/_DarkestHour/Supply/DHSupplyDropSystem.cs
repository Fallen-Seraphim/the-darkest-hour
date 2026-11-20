using System.Linq;
using System.Numerics;
using Content.Server._DarkestHour.Assault;
using Content.Server._DarkestHour.Carrier;
using Content.Server._DarkestHour.ControlPoints;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Carrier;
using Content.Shared._DarkestHour.HeavyGuns;
using Content.Shared._DarkestHour.Paradrop;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared._DarkestHour.Supply;
using Content.Shared.Buckle.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Supply;

/// <summary>
/// Сброс грузов на маяк (план осады п. 4, решения автора 2026-11-19): всё, что стоит на площадке сброса авианосца,
/// по команде с консоли улетает к выбранному включённому маяку на поверхности и через ~20 с опускается рядом с ним на
/// парашютах (разброс 1–4 клетки); поддон распаковывается в технику. Работает всегда, перезарядка 2 минуты.
/// Включённый маяк дымит; пока к нему летит груз, Республика видит его на тактической карте. Маяк можно разбить.
/// </summary>
public sealed partial class DHSupplyDropSystem : EntitySystem
{
    [Dependency] private DHAssaultRuleSystem _assault = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DHCarrierDamageSystem _carrierDamage = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DHTacticalMapSystem _tacmap = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const string SmokeProto = "DHDropBeaconSmoke";
    private const string UsedParachute = "DHParachuteUsed";
    private static readonly SoundSpecifier LandSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");
    private static readonly TimeSpan SmokeInterval = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan UiInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextUpdate;
    private TimeSpan _nextUi;
    private int _beaconCounter;

    private readonly List<Drop> _drops = new();

    private sealed class Drop
    {
        public List<EntityUid> Cargo = new();
        public EntityUid Beacon;
        public MapCoordinates Target;
        public TimeSpan LandAt;
    }

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<DHSupplyDropConsoleComponent>(DHSupplyDropUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<DHSupplyDropLaunchMessage>(OnLaunch);
        });
    }

    #region Маяки

    /// <summary>Маяк включён и стоит на поверхности района (не в руках, не в сумке).</summary>
    public bool BeaconReady(EntityUid uid, DHDropBeaconComponent beacon)
    {
        if (!beacon.Active || _tacmap.GetSurfaceMap() is not { } surface)
            return false;

        var xform = Transform(uid);
        return xform.MapUid == surface && !_container.IsEntityOrParentInContainer(uid);
    }

    public string BeaconName(EntityUid uid, DHDropBeaconComponent beacon)
    {
        if (beacon.Number == 0)
            beacon.Number = ++_beaconCounter;

        return Loc.GetString("dh-supply-drop-beacon-name", ("number", beacon.Number));
    }

    private void UpdateBeacons(TimeSpan now)
    {
        var query = EntityQueryEnumerator<DHDropBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var beacon, out var xform))
        {
            if (beacon.IncomingUntil is { } until && until <= now)
                beacon.IncomingUntil = null;

            if (now < beacon.NextSmoke || !BeaconReady(uid, beacon))
                continue;

            // Цветной дым над включённым маяком: виден издалека и с воздуха.
            beacon.NextSmoke = now + SmokeInterval;
            Spawn(SmokeProto, _transform.GetMapCoordinates(uid, xform)
                .Offset(_random.NextAngle().ToVec() * _random.NextFloat(0f, 0.3f)));
        }
    }

    #endregion

    #region Консоль

    private Entity<DHSupplyDropPadComponent>? FindPad(Entity<DHSupplyDropConsoleComponent> console)
    {
        var xform = Transform(console);
        Entity<DHSupplyDropPadComponent>? best = null;
        var bestDistance = console.Comp.PadRange;
        var query = EntityQueryEnumerator<DHSupplyDropPadComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pad, out var padXform))
        {
            if (padXform.GridUid != xform.GridUid || padXform.GridUid == null)
                continue;

            var distance = (padXform.LocalPosition - xform.LocalPosition).Length();
            if (distance > bestDistance)
                continue;

            best = (uid, pad);
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>Что стоит на площадке и может улететь: ящики, поддоны, орудия — не люди и не погрузчик.</summary>
    private List<EntityUid> CargoOn(Entity<DHSupplyDropPadComponent> pad)
    {
        var result = new List<EntityUid>();
        var xform = Transform(pad);
        var center = _transform.GetWorldPosition(xform);
        var half = pad.Comp.Radius + 0.45f;
        var box = new Box2(center - new Vector2(half, half), center + new Vector2(half, half));
        foreach (var uid in _lookup.GetEntitiesIntersecting(xform.MapID, box,
                     LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Static))
        {
            if (uid == pad.Owner || Transform(uid).ParentUid != xform.ParentUid)
                continue;

            if (HasComp<MobStateComponent>(uid) || HasComp<ActorComponent>(uid) || HasComp<StrapComponent>(uid) ||
                HasComp<DHSupplyDropPadComponent>(uid))
            {
                continue;
            }

            // Закреплённое — только груз для сброса (развёрнутая пушка); остальное закреплённое — часть корабля.
            if (Transform(uid).Anchored && !HasComp<DHParadroppableComponent>(uid))
                continue;

            result.Add(uid);
        }

        return result;
    }

    private string Blocked(EntityUid console)
    {
        if (_carrierDamage.Damaged(Transform(console).GridUid, DHCarrierCompartmentKind.Hangar))
            return Loc.GetString("dh-supply-drop-blocked-hangar");

        return string.Empty;
    }

    private void OnUiOpened(Entity<DHSupplyDropConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<DHSupplyDropConsoleComponent> ent)
    {
        var state = new DHSupplyDropState
        {
            Cooldown = (int) Math.Ceiling(Math.Max(0, (ent.Comp.NextDrop - _timing.CurTime).TotalSeconds)),
            Blocked = Blocked(ent),
        };

        if (FindPad(ent) is { } pad)
        {
            state.HasPad = true;
            state.Cargo = CargoOn(pad).Select(uid => Name(uid)).ToList();
        }

        var query = EntityQueryEnumerator<DHDropBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var beacon, out var xform))
        {
            if (!BeaconReady(uid, beacon))
                continue;

            var position = _transform.GetWorldPosition(xform);
            state.Beacons.Add(new DHSupplyDropBeaconRow
            {
                Beacon = GetNetEntity(uid),
                Name = BeaconName(uid, beacon),
                X = (int) MathF.Round(position.X),
                Y = (int) MathF.Round(position.Y),
                Incoming = beacon.IncomingUntil != null,
            });
        }

        state.Beacons.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        _ui.SetUiState(ent.Owner, DHSupplyDropUiKey.Key, state);
    }

    private void OnLaunch(Entity<DHSupplyDropConsoleComponent> ent, ref DHSupplyDropLaunchMessage args)
    {
        var now = _timing.CurTime;
        var user = args.Actor;
        if (Blocked(ent) is { Length: > 0 } blocked)
        {
            _popup.PopupEntity(blocked, ent, user);
            return;
        }

        if (now < ent.Comp.NextDrop)
        {
            _popup.PopupEntity(Loc.GetString("dh-supply-drop-cooldown"), ent, user);
            return;
        }

        if (!TryGetEntity(args.Beacon, out var beaconUid) ||
            !TryComp<DHDropBeaconComponent>(beaconUid, out var beacon) || !BeaconReady(beaconUid.Value, beacon))
        {
            _popup.PopupEntity(Loc.GetString("dh-supply-drop-no-beacon"), ent, user);
            UpdateUi(ent);
            return;
        }

        if (FindPad(ent) is not { } pad || CargoOn(pad) is not { Count: > 0 } cargo)
        {
            _popup.PopupEntity(Loc.GetString("dh-supply-drop-empty"), ent, user);
            return;
        }

        var drop = new Drop
        {
            Beacon = beaconUid.Value,
            Target = _transform.GetMapCoordinates(beaconUid.Value),
            LandAt = now + ent.Comp.FlightTime,
        };

        // Груз уходит с площадки «в капсулу» — вне карты до приземления.
        foreach (var uid in cargo)
        {
            var xform = Transform(uid);
            if (xform.Anchored)
                _transform.Unanchor(uid, xform);

            _transform.DetachEntity(uid, xform);
            drop.Cargo.Add(uid);
        }

        _drops.Add(drop);
        beacon.IncomingUntil = drop.LandAt;
        ent.Comp.NextDrop = now + ent.Comp.Cooldown;

        var name = BeaconName(beaconUid.Value, beacon);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_RMC14/Machines/asrs_raising.ogg"), pad);
        _assault.AnnounceToFaction(DHAssaultFaction.Empire, Loc.GetString("dh-supply-drop-sender"),
            Loc.GetString("dh-supply-drop-launched", ("beacon", name), ("count", cargo.Count),
                ("seconds", (int) ent.Comp.FlightTime.TotalSeconds)));
        UpdateUi(ent);
    }

    #endregion

    #region Приземление

    private void Land(Drop drop)
    {
        // Маяк могли переставить — груз идёт к нему; разбили или выключили — к последней точке.
        var target = drop.Target;
        if (TryComp<DHDropBeaconComponent>(drop.Beacon, out var beacon) && BeaconReady(drop.Beacon, beacon))
            target = _transform.GetMapCoordinates(drop.Beacon);

        if (_tacmap.GetSurfaceMap() is not { } surface)
            return;

        foreach (var uid in drop.Cargo)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            var position = target.Position + _random.NextAngle().ToVec() * _random.NextFloat(1f, 4f);
            var coordinates = new EntityCoordinates(surface, position);
            _transform.SetCoordinates(uid, coordinates);
            _transform.AttachToGridOrMap(uid);
            Spawn(UsedParachute, coordinates.Offset(new Vector2(0f, -1f)));
            _audio.PlayPvs(LandSound, uid);

            // Поддон распаковывается в технику.
            if (TryComp<DHDropPalletComponent>(uid, out var pallet))
            {
                Spawn(pallet.Spawn, Transform(uid).Coordinates);
                QueueDel(uid);
            }
        }

        _popup.PopupCoordinates(Loc.GetString("dh-supply-drop-landed"), new EntityCoordinates(surface, target.Position),
            PopupType.Medium);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + Interval;

        for (var i = _drops.Count - 1; i >= 0; i--)
        {
            if (now < _drops[i].LandAt)
                continue;

            var drop = _drops[i];
            _drops.RemoveAt(i);
            Land(drop);
        }

        UpdateBeacons(now);

        if (now < _nextUi)
            return;

        _nextUi = now + UiInterval;
        var consoles = EntityQueryEnumerator<DHSupplyDropConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            if (_ui.IsUiOpen(uid, DHSupplyDropUiKey.Key))
                UpdateUi((uid, console));
        }
    }
}
