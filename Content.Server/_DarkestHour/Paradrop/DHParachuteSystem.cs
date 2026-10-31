using Content.Server.Explosion.EntitySystems;
using Content.Shared._DarkestHour.HeavyGuns;
using Content.Shared._DarkestHour.Paradrop;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Paradrop;

/// <summary>
/// Парашюты: укладка, крепление к грузу, сброс груза за борт, проверка при приземлении (вызывает DHLandingSystem).
/// </summary>
public sealed partial class DHParachuteSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const string UsedParachute = "DHParachuteUsed";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    /// <summary>Удар о землю без парашюта.</summary>
    private const float CrashDamage = 400f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHParachuteComponent, MapInitEvent>(OnParachuteMapInit);
        SubscribeLocalEvent<DHParachuteComponent, UseInHandEvent>(OnParachuteUseInHand);
        SubscribeLocalEvent<DHParachuteComponent, DHRepackParachuteDoAfterEvent>(OnRepacked);
        SubscribeLocalEvent<DHParachuteComponent, ExaminedEvent>(OnParachuteExamined);

        SubscribeLocalEvent<DHParadroppableComponent, InteractUsingEvent>(OnCargoInteractUsing);
        SubscribeLocalEvent<DHParadroppableComponent, DHAttachParachuteDoAfterEvent>(OnAttached);
        SubscribeLocalEvent<DHParadroppableComponent, GetVerbsEvent<AlternativeVerb>>(OnCargoVerbs);
        SubscribeLocalEvent<DHParadroppableComponent, DHAirdropDoAfterEvent>(OnAirdropDoAfter);
        SubscribeLocalEvent<DHParadroppableComponent, ExaminedEvent>(OnCargoExamined);
    }

    #region Парашют

    private void OnParachuteMapInit(Entity<DHParachuteComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent, DHParachuteVisuals.Packed, ent.Comp.Packed);
    }

    private void OnParachuteUseInHand(Entity<DHParachuteComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Packed)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("dh-parachute-repacking"), ent, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.RepackTime,
            new DHRepackParachuteDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnRepacked(Entity<DHParachuteComponent> ent, ref DHRepackParachuteDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        SetPacked(ent, true);
        _popup.PopupEntity(Loc.GetString("dh-parachute-repacked"), ent, args.User);
    }

    private void OnParachuteExamined(Entity<DHParachuteComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Packed ? "dh-parachute-examine-packed" : "dh-parachute-examine-used"));
    }

    private void SetPacked(Entity<DHParachuteComponent> ent, bool packed)
    {
        ent.Comp.Packed = packed;
        Dirty(ent);
        _appearance.SetData(ent, DHParachuteVisuals.Packed, packed);
    }

    #endregion

    #region Груз

    private void OnCargoInteractUsing(Entity<DHParadroppableComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<DHParachuteComponent>(args.Used, out var parachute))
            return;

        args.Handled = true;
        if (!parachute.Packed)
        {
            _popup.PopupEntity(Loc.GetString("dh-parachute-not-packed"), ent, args.User);
            return;
        }

        if (ent.Comp.Parachutes >= ent.Comp.RequiredParachutes)
        {
            _popup.PopupEntity(Loc.GetString("dh-parachute-cargo-full"), ent, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, parachute.AttachTime,
            new DHAttachParachuteDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnAttached(Entity<DHParadroppableComponent> ent, ref DHAttachParachuteDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used ||
            !TryComp<DHParachuteComponent>(used, out var parachute) || !parachute.Packed ||
            ent.Comp.Parachutes >= ent.Comp.RequiredParachutes)
        {
            return;
        }

        args.Handled = true;
        QueueDel(used);
        ent.Comp.Parachutes++;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("dh-parachute-attached",
            ("count", ent.Comp.Parachutes), ("required", ent.Comp.RequiredParachutes)), ent, args.User);
    }

    private void OnCargoVerbs(Entity<DHParadroppableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        if (ent.Comp.Parachutes > 0)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dh-parachute-detach-verb"),
                Priority = -1,
                Act = () =>
                {
                    if (ent.Comp.Parachutes <= 0)
                        return;

                    ent.Comp.Parachutes--;
                    Dirty(ent);
                    Spawn("DHParachute", Transform(user).Coordinates);
                },
            });
        }

        if (!InAtmosphereShip(ent))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dh-parachute-airdrop-verb"),
            Priority = 1,
            Act = () =>
            {
                _popup.PopupEntity(Loc.GetString("dh-parachute-airdrop-start"), ent, user, PopupType.Medium);
                _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(4),
                    new DHAirdropDoAfterEvent(), ent, ent)
                {
                    BreakOnMove = true,
                    DistanceThreshold = 2f,
                });
            },
        });
    }

    /// <summary>Груз стоит на корабле, висящем в атмосфере над планетой.</summary>
    private bool InAtmosphereShip(EntityUid uid)
    {
        var xform = Transform(uid);
        return xform.GridUid is { } grid && grid != xform.MapUid && HasComp<DHPlanetAtmosphereMapComponent>(xform.MapUid);
    }

    /// <summary>Сброс за борт: груз выходит из корабля в атмосферу и начинает падать.</summary>
    private void OnAirdropDoAfter(Entity<DHParadroppableComponent> ent, ref DHAirdropDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !InAtmosphereShip(ent))
            return;

        args.Handled = true;
        var xform = Transform(ent);
        if (xform.MapUid is not { } map)
            return;

        if (xform.Anchored)
            _transform.Unanchor(ent, xform);

        _transform.SetCoordinates(ent, new EntityCoordinates(map, _transform.GetWorldPosition(xform)));
        _popup.PopupEntity(Loc.GetString("dh-parachute-airdrop-done"), args.User, args.User, PopupType.Medium);
    }

    private void OnCargoExamined(Entity<DHParadroppableComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("dh-parachute-cargo-examine",
            ("count", ent.Comp.Parachutes), ("required", ent.Comp.RequiredParachutes)));
    }

    #endregion

    #region Приземление

    /// <summary>
    /// Боец приземлился: раскрыл сложенный парашют из снаряжения — цел; нет парашюта — разбился.
    /// </summary>
    /// <returns>true — приземлился на парашюте.</returns>
    public bool LandMob(EntityUid uid)
    {
        foreach (var item in _inventory.GetHandOrInventoryEntities(uid))
        {
            if (!TryComp<DHParachuteComponent>(item, out var parachute) || !parachute.Packed)
                continue;

            SetPacked((item, parachute), false);
            return true;
        }

        _damageable.TryChangeDamage(uid, new DamageSpecifier(_proto.Index(Blunt), CrashDamage), true);
        _popup.PopupEntity(Loc.GetString("dh-parachute-crash-mob"), uid, PopupType.LargeCaution);
        return false;
    }

    /// <summary>
    /// Груз приземлился: хватает парашютов — цел (рядом остаются раскрытые парашюты, поддон распаковывается);
    /// не хватает — разбивается.
    /// </summary>
    public void LandCargo(Entity<DHParadroppableComponent> ent)
    {
        var coordinates = Transform(ent).Coordinates;
        if (ent.Comp.Parachutes < ent.Comp.RequiredParachutes)
        {
            _popup.PopupCoordinates(Loc.GetString("dh-parachute-crash-cargo", ("cargo", ent.Owner)), coordinates,
                PopupType.LargeCaution);
            _explosion.QueueExplosion(_transform.GetMapCoordinates(ent), "Default", 30f, 3f, 8f, null);
            QueueDel(ent);
            return;
        }

        for (var i = 0; i < ent.Comp.Parachutes; i++)
        {
            Spawn(UsedParachute, coordinates.Offset(new System.Numerics.Vector2(i - ent.Comp.Parachutes / 2f, -1f)));
        }

        ent.Comp.Parachutes = 0;
        Dirty(ent);

        if (TryComp<DHDropPalletComponent>(ent, out var pallet))
        {
            Spawn(pallet.Spawn, coordinates);
            QueueDel(ent);
        }
    }

    #endregion
}
