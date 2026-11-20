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
/// подъём заказанного грузовым лифтом. Пополнение раздела покупается за очки командования; позиции, закрытые узлом
/// древа, появляются (со стартовым запасом), когда командование откроет узел (<see cref="DHCommandPointsSystem"/>).
/// </summary>
public sealed partial class DHSupplySystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private DHBankSystem _bank = default!;
    [Dependency] private Content.Server._DarkestHour.Carrier.DHCarrierDamageSystem _carrierDamage = default!;
    [Dependency] private DHCommandPointsSystem _points = default!;
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

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<DHSupplyConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<DHSupplyConsoleComponent, DHSupplyOrderMessage>(OnOrder);
        SubscribeLocalEvent<DHSupplyConsoleComponent, DHSupplyToggleElevatorMessage>(OnToggleElevator);
        SubscribeLocalEvent<DHSupplyConsoleComponent, DHSupplyRestockMessage>(OnRestock);
        SubscribeLocalEvent<DHSupplyElevatorComponent, MapInitEvent>(OnElevatorMapInit);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _stock.Clear();
    }

    #region Запас

    private Dictionary<string, int> Stock(DHSupplyCatalogPrototype catalog)
    {
        if (_stock.TryGetValue(catalog.ID, out var stock))
            return stock;

        stock = new Dictionary<string, int>();
        foreach (var entry in catalog.Entries)
        {
            // Закрытое древом появляется, когда узел откроют.
            stock[entry.Id] = Locked(catalog, entry) ? 0 : entry.Initial;
        }

        _stock[catalog.ID] = stock;
        return stock;
    }

    /// <summary>Каталог стороны в «Осаде» (не за деньги).</summary>
    public DHSupplyCatalogPrototype? CatalogOf(DHAssaultFaction faction)
    {
        foreach (var catalog in _proto.EnumeratePrototypes<DHSupplyCatalogPrototype>())
        {
            if (catalog.Faction == faction && catalog.Bank == DHBankOrg.None)
                return catalog;
        }

        return null;
    }

    private bool Locked(DHSupplyCatalogPrototype catalog, DHSupplyEntry entry)
    {
        return catalog.Bank == DHBankOrg.None && _points.EntryLocked(catalog.Faction, entry.Id);
    }

    /// <summary>Потолок запаса позиции с учётом складов из древа.</summary>
    private int Max(DHSupplyCatalogPrototype catalog, DHSupplyEntry entry)
    {
        var bonus = 0f;
        foreach (var node in _points.OpenedNodes(catalog.Faction))
        {
            bonus += node.MaxBonus;
        }

        return (int) MathF.Ceiling(entry.Max * (1f + bonus));
    }

    /// <summary>Время подъёма и спуска лифта стороны с учётом древа.</summary>
    private float ElevatorFactor(DHAssaultFaction faction)
    {
        var factor = 1f;
        foreach (var node in _points.OpenedNodes(faction))
        {
            factor *= node.ElevatorTime;
        }

        return Math.Clamp(factor, 0.1f, 1f);
    }

    /// <summary>Командование открыло узел: его позиции появляются в каталоге со стартовым запасом.</summary>
    public void NodeOpened(DHSupplyNodePrototype node)
    {
        if (CatalogOf(node.Faction) is not { } catalog)
            return;

        var stock = Stock(catalog);
        foreach (var entry in catalog.Entries)
        {
            if (node.Unlocks.Contains(entry.Id) && !Locked(catalog, entry))
                stock[entry.Id] = Math.Max(stock.GetValueOrDefault(entry.Id), entry.Initial);
        }

        UpdateConsoles(catalog.ID);
    }

    /// <summary>Пополнение раздела за очки: каждой открытой позиции — её пополнение, не выше потолка.</summary>
    private void OnRestock(Entity<DHSupplyConsoleComponent> ent, ref DHSupplyRestockMessage args)
    {
        if (!_proto.TryIndex(ent.Comp.Catalog, out var catalog) || catalog.Bank != DHBankOrg.None)
            return;

        var category = args.Category;
        var restock = catalog.Restocks.Find(r => r.Category.Id == category);
        if (restock == null)
            return;

        var stock = Stock(catalog);
        var anything = false;
        foreach (var entry in catalog.Entries)
        {
            if (entry.Category.Id == category && entry.Refill > 0 && !Locked(catalog, entry) &&
                stock.GetValueOrDefault(entry.Id) < Max(catalog, entry))
            {
                anything = true;
            }
        }

        if (!anything)
        {
            _popup.PopupEntity(Loc.GetString("dh-supply-restock-full"), ent, args.Actor);
            return;
        }

        if (!_points.TrySpend(catalog.Faction, restock.Price))
        {
            _popup.PopupEntity(Loc.GetString("dh-command-points-not-enough", ("cost", restock.Price)), ent,
                args.Actor);
            return;
        }

        foreach (var entry in catalog.Entries)
        {
            if (entry.Category.Id != category || Locked(catalog, entry))
                continue;

            stock[entry.Id] = Math.Min(Max(catalog, entry), stock.GetValueOrDefault(entry.Id) + entry.Refill);
        }

        _popup.PopupEntity(Loc.GetString("dh-supply-restocked", ("category", Loc.GetString(restock.Category))), ent,
            args.Actor);
        UpdateConsoles(catalog.ID);

        // Каргоавтоматы стороны пополняются вместе с разделом (план осады п. 27).
        RaiseLocalEvent(new Content.Server._DarkestHour.Vendors.DHSupplyCategoryRestockedEvent(catalog.Faction, category));
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
            if (Locked(catalog, entry) || stock.GetValueOrDefault(entry.Id) <= 0)
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

        // Ангар авианосца повреждён — лифт стоит, пока пробоину не заварят (живучесть, п. 19).
        if (_carrierDamage.Damaged(Transform(elevator).GridUid,
                Content.Shared._DarkestHour.Carrier.DHCarrierCompartmentKind.Hangar))
        {
            _popup.PopupEntity(Loc.GetString("dh-supply-hangar-damaged"), ent, args.Actor);
            return;
        }

        switch (elevator.Comp.Mode)
        {
            case DHSupplyElevatorMode.Lowered:
                StartMove(elevator, DHSupplyElevatorMode.Raising, Factor(ent));
                break;
            case DHSupplyElevatorMode.Raised:
                // На опускающуюся платформу людей не берём.
                if (MobsOnPlatform(elevator))
                {
                    _popup.PopupEntity(Loc.GetString("dh-supply-people-on-platform"), ent, args.Actor);
                    return;
                }

                StartMove(elevator, DHSupplyElevatorMode.Lowering, Factor(ent));
                break;
            default:
                return;
        }

        UpdateUi(ent);
    }

    private float Factor(Entity<DHSupplyConsoleComponent> console)
    {
        return _proto.TryIndex(console.Comp.Catalog, out var catalog) && catalog.Bank == DHBankOrg.None
            ? ElevatorFactor(catalog.Faction)
            : 1f;
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
            // Закрытое древом не показываем.
            if (Locked(catalog, entry))
                continue;

            rows.Add(new DHSupplyStockRow(entry.Id, entry.Proto, entry.Category, stock.GetValueOrDefault(entry.Id),
                entry.Price) { Max = Max(catalog, entry) });
        }

        var elevator = FindElevator(ent);
        var state = new DHSupplyBuiState(rows, elevator != null,
            elevator?.Comp.Mode ?? DHSupplyElevatorMode.Lowered, elevator?.Comp.Orders.Count ?? 0,
            catalog.Bank == DHBankOrg.None ? _points.Points(catalog.Faction) : 0);
        foreach (var restock in catalog.Restocks)
        {
            state.Restocks.Add(new DHSupplyRestockRow(restock.Category, restock.Price));
        }
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

    private void StartMove(Entity<DHSupplyElevatorComponent> ent, DHSupplyElevatorMode mode, float factor)
    {
        SetMode(ent, mode);
        var raising = mode == DHSupplyElevatorMode.Raising;
        ent.Comp.DoneAt = _timing.CurTime + (raising ? ent.Comp.RaiseTime : ent.Comp.LowerTime) * factor;
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

    public void UpdateAllConsoles()
    {
        var query = EntityQueryEnumerator<DHSupplyConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            UpdateUi((uid, console));
        }
    }

    #endregion
}
