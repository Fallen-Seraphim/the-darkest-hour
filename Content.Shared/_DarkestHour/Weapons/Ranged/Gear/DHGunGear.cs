using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Network;

namespace Content.Shared._DarkestHour.Weapons.Ranged.Gear;

// Особенности стволов Земной Генеральной Ассоциации: два магазина с переключением (TIT/MG-10) и
// электромагнитная ступень, расходующая батарею на каждый выстрел (PRO/AMR-16).

/// <summary>
/// Второй (запасной) магазин рядом с основным: альт-действие меняет их местами за секунду.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHDualMagazineComponent : Component
{
    [DataField]
    public string MainSlot = "gun_magazine";

    [DataField]
    public string StandbySlot = "gun_magazine_2";

    [DataField]
    public SoundSpecifier SwitchSound = new SoundPathSpecifier("/Audio/Weapons/Guns/MagIn/batrifle_magin.ogg");
}

/// <summary>
/// Каждый выстрел дополнительно тратит заряд батареи из слота питания (гибрид порох + электромагнитный разгон).
/// Без заряда оружие не стреляет.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHGunChargePerShotComponent : Component
{
    [DataField]
    public float Charge = 200f;
}

public sealed partial class DHGunGearSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHDualMagazineComponent, GetVerbsEvent<AlternativeVerb>>(OnDualVerbs);
        SubscribeLocalEvent<DHDualMagazineComponent, ExaminedEvent>(OnDualExamined);
        SubscribeLocalEvent<DHGunChargePerShotComponent, AttemptShootEvent>(OnChargeAttempt);
        SubscribeLocalEvent<DHGunChargePerShotComponent, GunShotEvent>(OnChargeShot);
    }

    #region Два магазина

    private void OnDualVerbs(Entity<DHDualMagazineComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dh-dual-magazine-verb"),
            Priority = 2,
            Act = () => SwitchMagazines(ent, user),
        });
    }

    public void SwitchMagazines(Entity<DHDualMagazineComponent> ent, EntityUid user)
    {
        if (!_itemSlots.TryGetSlot(ent.Owner, ent.Comp.MainSlot, out var main) || main.ContainerSlot == null ||
            !_itemSlots.TryGetSlot(ent.Owner, ent.Comp.StandbySlot, out var standby) || standby.ContainerSlot == null)
        {
            return;
        }

        if (standby.Item is not { } next)
        {
            _popup.PopupClient(Loc.GetString("dh-dual-magazine-no-standby"), ent, user);
            return;
        }

        _audio.PlayPredicted(ent.Comp.SwitchSound, ent, user);

        // перестановка — только на сервере (контейнеры), клиент получит состояние
        if (_net.IsClient)
            return;

        var current = main.Item;
        if (current != null)
            _container.Remove(current.Value, main.ContainerSlot, force: true);
        _container.Remove(next, standby.ContainerSlot, force: true);
        _container.Insert(next, main.ContainerSlot, force: true);
        if (current != null)
            _container.Insert(current.Value, standby.ContainerSlot, force: true);

        _popup.PopupEntity(Loc.GetString("dh-dual-magazine-switched"), ent, user);
    }

    private void OnDualExamined(Entity<DHDualMagazineComponent> ent, ref ExaminedEvent args)
    {
        if (!_itemSlots.TryGetSlot(ent.Owner, ent.Comp.StandbySlot, out var standby))
            return;

        args.PushMarkup(Loc.GetString(standby.Item != null ? "dh-dual-magazine-examine-loaded" : "dh-dual-magazine-examine-empty"));
    }

    #endregion

    #region Заряд на выстрел

    private void OnChargeAttempt(Entity<DHGunChargePerShotComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled || _powerCell.HasCharge(ent.Owner, ent.Comp.Charge))
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString("dh-gun-charge-empty");
    }

    private void OnChargeShot(Entity<DHGunChargePerShotComponent> ent, ref GunShotEvent args)
    {
        _powerCell.TryUseCharge(ent.Owner, ent.Comp.Charge);
    }

    #endregion
}
