using System.Numerics;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.ControlPoints;
using Content.Server._DarkestHour.City;
using Content.Shared._DarkestHour.City;
using Content.Shared._DarkestHour.Supply;
using Content.Shared.GameTicking;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Supply;

/// <summary>
/// Снабжение стороны (см. <see cref="DHSupplyCatalogPrototype"/>): общий на сторону запас ящиков, заказ с компьютера,
/// подъём заказанного грузовым лифтом и пополнение запаса, пока сторона держит больше двух точек.
/// </summary>
public sealed partial class DHSupplySystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private DHBankSystem _bank = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    /// <summary>Запас по каталогам: каталог → позиция → сколько осталось.</summary>
    private readonly Dictionary<string, Dictionary<string, int>> _stock = new();

    /// <summary>Когда каталог пополнится в следующий раз.</summary>
    private readonly Dictionary<string, TimeSpan> _nextRefill = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<DHSupplyConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<DHSupplyConsoleComponent, DHSupplyOrderMessage>(OnOrder);
        SubscribeLocalEvent<DHSupplyConsoleComponent, DHSupplyToggleElevatorMessage>(OnToggleElevator);
        SubscribeLocalEvent<DHSupplyElevatorComponent, MapInitEvent>(OnElevatorMapInit);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _stock.Clear();
        _nextRefill.Clear();
    }

    #region Запас

    private Dictionary<string, int> Stock(DHSupplyCatalogPrototype catalog)
    {
        if (_stock.TryGetValue(catalog.ID, out var stock))
            return stock;

        stock = new Dictionary<string, int>();
        foreach (var entry in catalog.Entries)
        {
            stock[entry.Id] = entry.Initial;
        }

        _stock[catalog.ID] = stock;
        _nextRefill[catalog.ID] = _timing.CurTime + catalog.RefillInterval;
        return stock;
    }

    /// <summary>Сколько точек держит сторона.</summary>
    public int HeldPoints(DHAssaultFaction faction)
    {
        var count = 0;
        var query = EntityQueryEnumerator<DHControlPointComponent>();
        while (query.MoveNext(out _, out var point))
        {
            if (point.Holder == faction)
                count++;
        }

        return count;
    }

    private void TickRefill(TimeSpan now)
    {
        foreach (var (catalogId, next) in new List<KeyValuePair<string, TimeSpan>>(_nextRefill))
        {
            if (now < next || !_proto.TryIndex<DHSupplyCatalogPrototype>(catalogId, out var catalog) ||
                catalog.Bank != DHBankOrg.None)
            {
                continue;
            }

            _nextRefill[catalogId] = now + catalog.RefillInterval;
            if (HeldPoints(catalog.Faction) <= catalog.RefillMinPoints)
                continue;

            var stock = Stock(catalog);
            foreach (var entry in catalog.Entries)
            {
                stock[entry.Id] = Math.Min(entry.Max, stock.GetValueOrDefault(entry.Id) + entry.Refill);
            }

            UpdateConsoles(catalogId);
        }
    }

    #endregion

    #region Компьютер

    private void OnUiOpened(Entity<DHSupplyConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnOrder(Entity<DHSupplyConsoleComponent> ent, ref DHSupplyOrderMessage args)
    {
        if (!_proto.TryIndex(ent.Comp.Catalog, out var catalog))
            return;

        var id = args.Id;
        var entry = catalog.Entries.Find(e => e.Id == id);
        if (entry == null)
            return;

        if (FindElevator(ent) is not { } elevator)
        {
            _popup.PopupEntity(Loc.GetString("dh-supply-no-elevator"), ent, args.Actor);
            return;
        }

        if (catalog.Bank != DHBankOrg.None)
        {
            // За деньги со счёта организации, без запаса.
            var account = _bank.OrgAccount(catalog.Bank);
            if (!_bank.Withdraw(account, entry.Price, Loc.GetString("dh-supply-log-order", ("item", _proto.Index(entry.Proto).Name))))
            {
                _popup.PopupEntity(Loc.GetString("dh-supply-no-money", ("price", entry.Price)), ent, args.Actor);
                return;
            }
        }
        else
        {
            var stock = Stock(catalog);
            if (stock.GetValueOrDefault(entry.Id) <= 0)
            {
                _popup.PopupEntity(Loc.GetString("dh-supply-out-of-stock"), ent, args.Actor);
                return;
            }

            stock[entry.Id]--;
        }

        elevator.Comp.Orders.Add(entry.Proto);
        _popup.PopupEntity(Loc.GetString("dh-supply-ordered", ("item", _proto.Index(entry.Proto).Name)),
            ent, args.Actor);
        UpdateConsoles(catalog.ID);
    }

    private void OnToggleElevator(Entity<DHSupplyConsoleComponent> ent, ref DHSupplyToggleElevatorMessage args)
    {
        if (FindElevator(ent) is not { } elevator)
            return;

        switch (elevator.Comp.Mode)
        {
            case DHSupplyElevatorMode.Lowered:
                StartMove(elevator, DHSupplyElevatorMode.Raising);
                break;
            case DHSupplyElevatorMode.Raised:
                // На опускающуюся платформу людей не берём.
                if (MobsOnPlatform(elevator))
                {
                    _popup.PopupEntity(Loc.GetString("dh-supply-people-on-platform"), ent, args.Actor);
                    return;
                }

                StartMove(elevator, DHSupplyElevatorMode.Lowering);
                break;
            default:
                return;
        }

        UpdateUi(ent);
    }

    private Entity<DHSupplyElevatorComponent>? FindElevator(Entity<DHSupplyConsoleComponent> console)
    {
        var xform = Transform(console);
        var position = _transform.GetWorldPosition(xform);
        Entity<DHSupplyElevatorComponent>? best = null;
        var bestDistance = float.MaxValue;

        var query = EntityQueryEnumerator<DHSupplyElevatorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var elevator, out var elevatorXform))
        {
            if (elevatorXform.GridUid != xform.GridUid)
                continue;

            var distance = (_transform.GetWorldPosition(elevatorXform) - position).Length();
            if (distance > console.Comp.ElevatorRange || distance >= bestDistance)
                continue;

            best = (uid, elevator);
            bestDistance = distance;
        }

        return best;
    }

    private void UpdateConsoles(string catalogId)
    {
        var query = EntityQueryEnumerator<DHSupplyConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (console.Catalog == catalogId)
                UpdateUi((uid, console));
        }
    }

    private void UpdateUi(Entity<DHSupplyConsoleComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, DHSupplyUiKey.Key) || !_proto.TryIndex(ent.Comp.Catalog, out var catalog))
            return;

        var stock = Stock(catalog);
        var rows = new List<DHSupplyStockRow>();
        foreach (var entry in catalog.Entries)
        {
            rows.Add(new DHSupplyStockRow(entry.Id, entry.Proto, entry.Category, stock.GetValueOrDefault(entry.Id),
                entry.Price));
        }

        var elevator = FindElevator(ent);
        var state = new DHSupplyBuiState(rows, elevator != null,
            elevator?.Comp.Mode ?? DHSupplyElevatorMode.Lowered, elevator?.Comp.Orders.Count ?? 0,
            HeldPoints(catalog.Faction), catalog.RefillMinPoints, (int) catalog.RefillInterval.TotalMinutes);
        if (catalog.Bank != DHBankOrg.None)
        {
            var account = _bank.OrgAccount(catalog.Bank);
            state.Paid = true;
            state.Account = account.Number;
            state.Balance = account.Balance;
        }

        _ui.SetUiState(ent.Owner, DHSupplyUiKey.Key, state);
    }

    #endregion

    #region Лифт

    private void OnElevatorMapInit(Entity<DHSupplyElevatorComponent> ent, ref MapInitEvent args)
    {
        SetMode(ent, ent.Comp.Mode);
    }

    private void StartMove(Entity<DHSupplyElevatorComponent> ent, DHSupplyElevatorMode mode)
    {
        SetMode(ent, mode);
        var raising = mode == DHSupplyElevatorMode.Raising;
        ent.Comp.DoneAt = _timing.CurTime + (raising ? ent.Comp.RaiseTime : ent.Comp.LowerTime);
        _audio.PlayPvs(raising ? ent.Comp.RaisingSound : ent.Comp.LoweringSound, ent);
    }

    private void SetMode(Entity<DHSupplyElevatorComponent> ent, DHSupplyElevatorMode mode)
    {
        ent.Comp.Mode = mode;
        Dirty(ent);
        _appearance.SetData(ent, DHSupplyElevatorVisuals.Mode, mode);

        // Пока платформа не наверху, в яму не войти.
        if (_fixtures.GetFixtureOrNull(ent, ent.Comp.PitFixture) is { } pit)
            _physics.SetHard(ent, pit, mode != DHSupplyElevatorMode.Raised);
    }

    private IEnumerable<EntityUid> OnPlatform(Entity<DHSupplyElevatorComponent> ent)
    {
        var xform = Transform(ent);
        var center = _transform.GetWorldPosition(xform);
        var half = ent.Comp.Radius + 0.45f;
        var box = new Box2(center - new Vector2(half, half), center + new Vector2(half, half));
        foreach (var uid in _lookup.GetEntitiesIntersecting(xform.MapID, box, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (uid != ent.Owner && !Transform(uid).Anchored)
                yield return uid;
        }
    }

    private bool MobsOnPlatform(Entity<DHSupplyElevatorComponent> ent)
    {
        foreach (var uid in OnPlatform(ent))
        {
            if (HasComp<MobStateComponent>(uid))
                return true;
        }

        return false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        TickRefill(now);

        var query = EntityQueryEnumerator<DHSupplyElevatorComponent>();
        while (query.MoveNext(out var uid, out var elevator))
        {
            if (elevator.DoneAt is not { } done || now < done)
                continue;

            elevator.DoneAt = null;
            var ent = new Entity<DHSupplyElevatorComponent>(uid, elevator);
            if (elevator.Mode == DHSupplyElevatorMode.Raising)
            {
                SetMode(ent, DHSupplyElevatorMode.Raised);
                Deliver(ent);
            }
            else if (elevator.Mode == DHSupplyElevatorMode.Lowering)
            {
                // Что осталось на платформе (пустые ящики, мусор), уходит в трюм.
                foreach (var thing in OnPlatform(ent))
                {
                    if (HasComp<ItemComponent>(thing) || !HasComp<MobStateComponent>(thing))
                        QueueDel(thing);
                }

                SetMode(ent, DHSupplyElevatorMode.Lowered);
            }

            UpdateAllConsoles();
        }
    }

    /// <summary>Заказанное появляется на платформе, по клеткам слева направо и сверху вниз.</summary>
    private void Deliver(Entity<DHSupplyElevatorComponent> ent)
    {
        var xform = Transform(ent);
        var size = ent.Comp.Radius * 2 + 1;
        var i = 0;
        foreach (var proto in ent.Comp.Orders)
        {
            var cell = i % (size * size);
            var offset = new Vector2(cell % size - ent.Comp.Radius, ent.Comp.Radius - cell / size);
            Spawn(proto, xform.Coordinates.Offset(offset));
            i++;
        }

        ent.Comp.Orders.Clear();
    }

    private void UpdateAllConsoles()
    {
        var query = EntityQueryEnumerator<DHSupplyConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            UpdateUi((uid, console));
        }
    }

    #endregion
}
