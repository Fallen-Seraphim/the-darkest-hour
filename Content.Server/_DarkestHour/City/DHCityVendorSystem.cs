using Content.Server.Stack;
using Content.Shared._DarkestHour.City;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.City;

/// <summary>
/// Уличные автоматы Лост-Энджелса: берут только республиканские кредиты наличными (на счётчик автомата), выдают товар
/// по цене и сдачу.
/// </summary>
public sealed partial class DHCityVendorSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly SoundSpecifier InsertSound = new SoundPathSpecifier("/Audio/Machines/id_insert.ogg");
    private static readonly SoundSpecifier VendSound = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");
    private static readonly SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHCityVendorComponent, InteractUsingEvent>(OnInteractUsing);
        Subs.BuiEvents<DHCityVendorComponent>(DHCityVendorUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<DHCityVendorBuyMessage>(OnBuy);
            subs.Event<DHCityVendorChangeMessage>(OnChange);
        });
    }

    /// <summary>Наличные республиканские кредиты в руке: сколько, или null, если это не они.</summary>
    public int? RepublicCash(EntityUid used)
    {
        if (!TryComp<StackComponent>(used, out var stack) || stack.StackTypeId != DHCityCurrency.Stack)
            return null;

        return _stack.GetCount((used, stack));
    }

    private void OnInteractUsing(Entity<DHCityVendorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (RepublicCash(args.Used) is not { } amount)
        {
            if (TryComp<StackComponent>(args.Used, out var other) && other.StackTypeId == "Credit")
            {
                _popup.PopupEntity(Loc.GetString("dh-city-vendor-wrong-currency"), ent, args.User);
                _audio.PlayPvs(DenySound, ent);
                args.Handled = true;
            }

            return;
        }

        args.Handled = true;
        ent.Comp.Balance += amount;
        QueueDel(args.Used);
        _audio.PlayPvs(InsertSound, ent);
        _popup.PopupEntity(Loc.GetString("dh-city-vendor-inserted", ("amount", amount), ("balance", ent.Comp.Balance)),
            ent, args.User);
        UpdateUi(ent);
    }

    private void OnOpened(Entity<DHCityVendorComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnBuy(Entity<DHCityVendorComponent> ent, ref DHCityVendorBuyMessage args)
    {
        DHCityVendorItem? item = null;
        foreach (var entry in ent.Comp.Items)
        {
            if (entry.Id.Id == args.Id)
                item = entry;
        }

        if (item == null)
            return;

        if (ent.Comp.Balance < item.Price)
        {
            _popup.PopupEntity(Loc.GetString("dh-city-vendor-not-enough", ("price", item.Price)), ent, args.Actor);
            _audio.PlayPvs(DenySound, ent);
            return;
        }

        ent.Comp.Balance -= item.Price;
        var spawned = Spawn(item.Id, Transform(ent).Coordinates);
        _hands.PickupOrDrop(args.Actor, spawned);
        _audio.PlayPvs(VendSound, ent);
        UpdateUi(ent);
    }

    private void OnChange(Entity<DHCityVendorComponent> ent, ref DHCityVendorChangeMessage args)
    {
        if (ent.Comp.Balance <= 0)
            return;

        var change = ent.Comp.Balance;
        ent.Comp.Balance = 0;
        foreach (var cash in _stack.SpawnMultipleNextToOrDrop(DHCityCurrency.Cash, change, ent))
        {
            _hands.PickupOrDrop(args.Actor, cash);
        }

        UpdateUi(ent);
    }

    private void UpdateUi(Entity<DHCityVendorComponent> ent)
    {
        var items = new List<DHCityVendorEntry>();
        foreach (var item in ent.Comp.Items)
        {
            var name = _proto.TryIndex(item.Id, out var proto) ? proto.Name : item.Id.Id;
            items.Add(new DHCityVendorEntry(item.Id.Id, name, item.Price));
        }

        _ui.SetUiState(ent.Owner, DHCityVendorUiKey.Key, new DHCityVendorState(ent.Comp.Balance, items));
    }
}
