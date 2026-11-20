using System.Numerics;
using Content.Server._DarkestHour.ControlPoints;
using Content.Server._DarkestHour.Vehicles;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.Buckle.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Spawners.EntitySystems;
using Content.Shared.Station.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Assault;

/// <summary>
/// Въезд подкреплений стороны на краю карты (план осады п. 2): здесь появляется грузовик с подкреплением.
/// </summary>
[RegisterComponent]
public sealed partial class DHReinforcementEntryComponent : Component
{
    [DataField]
    public DHAssaultFaction Faction = DHAssaultFaction.Republic;

    /// <summary>Название въезда в сообщениях («восточный въезд»).</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>Точки захвата на пути от въезда к базе: пока любую держит противник, въезд закрыт.</summary>
    [DataField]
    public List<string> ClosedBy = new();

    [DataField]
    public EntProtoId Truck = "DHVehicleTruckRepublic";

    /// <summary>Места для грузовиков, в клетках относительно метки (нос машины — к −Y): если первое занято — следующее.</summary>
    [DataField]
    public List<Vector2> Slots = new() { Vector2.Zero, new Vector2(0f, -4f), new Vector2(0f, -8f) };
}

/// <summary>Грузовик с подкреплением, который ещё стоит у въезда.</summary>
[RegisterComponent]
public sealed partial class DHReinforcementTruckComponent : Component
{
    [DataField]
    public EntityUid Entry;

    [DataField]
    public DHAssaultFaction Faction;

    /// <summary>Где грузовик появился (уехал дальше <c>LeftEntryRange</c> — обычный грузовик).</summary>
    [ViewVariables]
    public MapCoordinates Origin;

    /// <summary>Сколько бойцов уже появилось в кузове.</summary>
    [DataField]
    public int Passengers;

    /// <summary>До этого времени в грузовик ещё сажают опоздавших (отсчёт — от первого бойца).</summary>
    [DataField]
    public TimeSpan? BoardingEnds;

    /// <summary>С какого времени грузовик у въезда стоит пустым.</summary>
    [DataField]
    public TimeSpan? EmptySince;
}

/// <summary>
/// Подкрепления на грузовиках (план осады п. 2, решения автора 2026-11-19). Все, кто появляется у стороны после
/// начала раунда, — и зашедшие позже, и вернувшиеся с волной подкреплений, — появляются в кузове грузовика у въезда на
/// краю карты (на «Терминусе» — восток по шоссе и запад через деревню и дамбу) и сами едут к фронту: кто-то садится за
/// руль. В грузовик сажают, пока он не полон и 45 с после первого бойца, дальше — новый грузовик у следующего
/// открытого въезда. Въезд закрыт, пока противник держит точку на пути (на западе — дамбу или стройку). Бойцы,
/// появившиеся со старта раунда, — на базе, как раньше.
/// <para>Грузовик, который уехал от въезда, — обычный грузовик стороны. Брошенный у въезда пустым на 3 минуты —
/// «уезжает обратно» (удаляется).</para>
/// </summary>
public sealed partial class DHReinforcementTruckSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private DHAssaultRuleSystem _assault = default!;
    [Dependency] private DHControlPointSystem _points = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private StationSpawningSystem _spawning = default!;
    [Dependency] private ServerGameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DHVehicleServerSystem _vehicles = default!;

    private static readonly TimeSpan BoardingTime = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan AbandonTime = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

    /// <summary>Дальше этого от места появления грузовик считается уехавшим — дальше он обычный.</summary>
    private const float LeftEntryRange = 12f;

    /// <summary>Свободно ли место под грузовик: другой техники ближе этого нет.</summary>
    private const float SlotClearance = 2.4f;

    /// <summary>Бойцы, которых только что посадили в грузовик: им — сообщение, где они (по завершении появления).</summary>
    private readonly Dictionary<EntityUid, EntityUid> _arrived = new();

    private readonly HashSet<Entity<DHVehicleComponent>> _nearby = new();

    private int _nextEntry;
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning,
            before: new[] { typeof(SpawnPointSystem), typeof(ContainerSpawnPointSystem), typeof(ArrivalsSystem) });
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _arrived.Clear();
        _nextEntry = 0;
    }

    private void OnPlayerSpawning(PlayerSpawningEvent args)
    {
        // Со старта раунда (уровень раунда ещё «лобби») — на базе, как раньше.
        if (args.SpawnResult != null || _ticker.RunLevel != GameRunLevel.InRound || args.Station is not { } station ||
            !TryComp<DHAssaultFactionStationComponent>(station, out var factionStation) || !AssaultActive())
        {
            return;
        }

        if (Boarding(factionStation.Faction) is not { } truck ||
            !TryComp<DHVehicleComponent>(truck, out var vehicle) ||
            SeatFor((truck.Owner, vehicle), truck.Comp.Passengers) is not { } seat)
        {
            return;
        }

        var mob = _spawning.SpawnPlayerMob(seat, args.Job, args.HumanoidCharacterProfile, station);
        args.SpawnResult = mob;
        truck.Comp.Passengers++;
        truck.Comp.BoardingEnds ??= _timing.CurTime + BoardingTime;
        truck.Comp.EmptySince = null;
        _arrived[mob] = truck;
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!_arrived.Remove(ev.Mob, out var truck) || !TryComp<DHReinforcementTruckComponent>(truck, out var comp))
            return;

        _chat.DispatchServerMessage(ev.Player, Loc.GetString("dh-reinforcement-truck-boarded",
            ("entry", EntryName(comp.Entry))));
    }

    private bool AssaultActive()
    {
        var rules = EntityQueryEnumerator<DHAssaultRuleComponent, ActiveGameRuleComponent>();
        return rules.MoveNext(out _, out _);
    }

    /// <summary>Грузовик, в который сейчас сажают, или новый у следующего открытого въезда.</summary>
    private Entity<DHReinforcementTruckComponent>? Boarding(DHAssaultFaction faction)
    {
        var now = _timing.CurTime;
        var trucks = EntityQueryEnumerator<DHReinforcementTruckComponent, DHVehicleComponent>();
        while (trucks.MoveNext(out var uid, out var truck, out var vehicle))
        {
            if (truck.Faction != faction || truck.BoardingEnds is { } ends && now >= ends ||
                vehicle.InteriorGrid == null || Moved(uid, truck) ||
                vehicle.Capacity > 0 && _vehicles.Occupants((uid, vehicle)) >= vehicle.Capacity)
            {
                continue;
            }

            return (uid, truck);
        }

        return SpawnTruck(faction);
    }

    private Entity<DHReinforcementTruckComponent>? SpawnTruck(DHAssaultFaction faction)
    {
        var all = new List<Entity<DHReinforcementEntryComponent>>();
        var entries = EntityQueryEnumerator<DHReinforcementEntryComponent>();
        while (entries.MoveNext(out var uid, out var entry))
        {
            if (entry.Faction == faction)
                all.Add((uid, entry));
        }

        if (all.Count == 0)
            return null;

        all.Sort((a, b) => a.Owner.CompareTo(b.Owner));
        var open = all.FindAll(Open);
        // Все въезды перерезаны — грузовик всё равно приходит (иначе сторона осталась бы без подкреплений).
        var pick = (open.Count > 0 ? open : all)[_nextEntry++ % (open.Count > 0 ? open.Count : all.Count)];

        var xform = Transform(pick);
        var origin = _transform.GetMapCoordinates(pick.Owner, xform);
        var rotation = _transform.GetWorldRotation(xform);
        var position = origin.Position;
        foreach (var slot in pick.Comp.Slots)
        {
            var candidate = origin.Position + rotation.RotateVec(slot);
            _nearby.Clear();
            _lookup.GetEntitiesInRange(origin.MapId, candidate, SlotClearance, _nearby);
            if (_nearby.Count > 0)
                continue;

            position = candidate;
            break;
        }

        var spot = new MapCoordinates(position, origin.MapId);
        var truckUid = Spawn(pick.Comp.Truck, spot, rotation: rotation);
        var truck = EnsureComp<DHReinforcementTruckComponent>(truckUid);
        truck.Entry = pick;
        truck.Faction = faction;
        truck.Origin = spot;
        if (TryComp<DHVehicleComponent>(truckUid, out var vehicle))
            _vehicles.SetEngine((truckUid, vehicle), true);

        _assault.AnnounceToFaction(faction, Loc.GetString("dh-reinforcement-truck-sender"),
            Loc.GetString("dh-reinforcement-truck-announce", ("entry", Loc.GetString(pick.Comp.Name))));
        return (truckUid, truck);
    }

    private bool Open(Entity<DHReinforcementEntryComponent> entry)
    {
        foreach (var point in entry.Comp.ClosedBy)
        {
            if (_points.HolderOf(point) is { } holder && holder != entry.Comp.Faction)
                return false;
        }

        return true;
    }

    /// <summary>Где появится боец: на местах кузова по очереди (кресла водителя и стрелков — не занимаем).</summary>
    private EntityCoordinates? SeatFor(Entity<DHVehicleComponent> truck, int index)
    {
        if (truck.Comp.InteriorGrid is not { } grid)
            return null;

        var seats = new List<EntityUid>();
        var straps = EntityQueryEnumerator<StrapComponent, TransformComponent>();
        while (straps.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == grid && !HasComp<DHVehicleStationComponent>(uid))
                seats.Add(uid);
        }

        if (seats.Count == 0)
            return new EntityCoordinates(grid, truck.Comp.InteriorEntry);

        seats.Sort();
        return Transform(seats[index % seats.Count]).Coordinates;
    }

    private string EntryName(EntityUid entry)
    {
        return TryComp<DHReinforcementEntryComponent>(entry, out var comp)
            ? Loc.GetString(comp.Name)
            : Loc.GetString("dh-reinforcement-entry-unknown");
    }

    private bool Moved(EntityUid uid, DHReinforcementTruckComponent truck)
    {
        var position = _transform.GetMapCoordinates(uid);
        return position.MapId != truck.Origin.MapId ||
               Vector2.Distance(position.Position, truck.Origin.Position) > LeftEntryRange;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;
        var trucks = EntityQueryEnumerator<DHReinforcementTruckComponent, DHVehicleComponent>();
        while (trucks.MoveNext(out var uid, out var truck, out var vehicle))
        {
            // Уехал от въезда — обычный грузовик стороны.
            if (Moved(uid, truck))
            {
                RemCompDeferred<DHReinforcementTruckComponent>(uid);
                continue;
            }

            if (_vehicles.Occupants((uid, vehicle)) > 0 || truck.BoardingEnds is { } ends && now < ends)
            {
                truck.EmptySince = null;
                continue;
            }

            truck.EmptySince ??= now;
            if (now - truck.EmptySince.Value >= AbandonTime)
                QueueDel(uid);
        }
    }
}
