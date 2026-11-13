using System.Numerics;
using Content.Shared._DarkestHour.Aviation;
using Content.Shared._DarkestHour.PowerLoader;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Robust.Server.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server._DarkestHour.PowerLoader;

/// <summary>
/// Погрузчик: пилот пристёгивается, едет на нём (реле ввода) и работает клешнями — виртуальными предметами в обеих
/// руках. Клик клешнёй: пусто — захват груза; с грузом по цели — зарядить туда (событие InteractUsing от пилота с
/// грузом, то есть той же логикой, что и вручную); с грузом по полу — поставить. См. <see cref="DHPowerLoaderComponent"/>.
/// </summary>
public sealed partial class DHPowerLoaderSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHPowerLoaderComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DHPowerLoaderComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<DHPowerLoaderComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<DHPowerLoaderComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DHPowerLoaderComponent, DHPowerLoaderGrabDoAfterEvent>(OnGrabDoAfter);
        SubscribeLocalEvent<DHPowerLoaderComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<DHPowerLoaderClampComponent, BeforeRangedInteractEvent>(OnClampInteract);
    }

    private void OnStartup(Entity<DHPowerLoaderComponent> ent, ref ComponentStartup args)
    {
        // Груз видно в клешнях: содержимое контейнера рисуется поверх погрузчика.
        var slot = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.Container);
        slot.ShowContents = true;
        slot.OccludesLight = false;
    }

    private void OnTerminating(Entity<DHPowerLoaderComponent> ent, ref EntityTerminatingEvent args)
    {
        // Разбитый погрузчик роняет груз, а не уносит его с собой.
        if (Held(ent) is { } held)
            _container.Remove(held, _container.GetContainer(ent, ent.Comp.Container));
    }

    private void OnStrapped(Entity<DHPowerLoaderComponent> ent, ref StrappedEvent args)
    {
        var pilot = args.Buckle.Owner;
        _mover.SetRelay(pilot, ent);

        // Обе руки — клешни.
        if (!TryComp<HandsComponent>(pilot, out var hands))
            return;

        for (var i = 0; i < hands.Count; i++)
        {
            if (!_virtualItem.TrySpawnUnremoveableVirtualItemInHand(ent, pilot, out var clamp, dropOthers: true))
                break;

            EnsureComp<DHPowerLoaderClampComponent>(clamp.Value).Loader = ent;
        }
    }

    private void OnUnstrapped(Entity<DHPowerLoaderComponent> ent, ref UnstrappedEvent args)
    {
        var pilot = args.Buckle.Owner;
        RemComp<RelayInputMoverComponent>(pilot);
        _virtualItem.DeleteInHandsMatching(pilot, ent);
    }

    private void OnExamined(Entity<DHPowerLoaderComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Held(ent) is { } held
            ? Loc.GetString("dh-power-loader-examine-held", ("item", held))
            : Loc.GetString("dh-power-loader-examine-empty"));
    }

    /// <summary>Погрузчик, в котором сидит <paramref name="pilot"/>.</summary>
    public bool TryGetLoader(EntityUid pilot, out Entity<DHPowerLoaderComponent> loader)
    {
        loader = default;
        if (!TryComp<BuckleComponent>(pilot, out var buckle) || buckle.BuckledTo is not { } strap ||
            !TryComp<DHPowerLoaderComponent>(strap, out var comp))
        {
            return false;
        }

        loader = (strap, comp);
        return true;
    }

    /// <summary>
    /// Погрузчик пилота с пустыми клешнями, достающий до <paramref name="target"/> (до штурмовика — дальше). Иначе —
    /// подсказка пилоту и false.
    /// </summary>
    public bool TryGetFreeLoaderNear(EntityUid pilot, EntityUid target, out Entity<DHPowerLoaderComponent> loader,
        bool popup = true)
    {
        if (!TryGetLoader(pilot, out loader))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("dh-power-loader-required"), target, pilot);
            return false;
        }

        if (Held(loader) != null)
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("dh-power-loader-clamp-busy"), loader, pilot);
            return false;
        }

        var reach = HasComp<DHAircraftComponent>(target) ? loader.Comp.BigReach : loader.Comp.Reach;
        if (Distance(loader, target) > reach)
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("dh-power-loader-too-far"), loader, pilot);
            return false;
        }

        return true;
    }

    /// <summary>Положить груз в пустые клешни (снятое с самолёта и т. п.).</summary>
    public bool TryHold(Entity<DHPowerLoaderComponent> loader, EntityUid cargo)
    {
        if (Held(loader) != null || !_container.Insert(cargo, _container.GetContainer(loader, loader.Comp.Container)))
            return false;

        _audio.PlayPvs(loader.Comp.GrabSound, loader);
        return true;
    }

    private EntityUid? Held(Entity<DHPowerLoaderComponent> ent)
    {
        return _container.TryGetContainer(ent, ent.Comp.Container, out var container) &&
               container.ContainedEntities.Count > 0
            ? container.ContainedEntities[0]
            : null;
    }

    /// <summary>Что клешни могут взять: помеченный тяжёлый груз или незакреплённый ящик.</summary>
    private bool CanGrab(EntityUid target)
    {
        if (HasComp<DHPowerLoaderGrabbableComponent>(target))
            return true;

        return HasComp<EntityStorageComponent>(target) && !Transform(target).Anchored &&
               TryComp<PhysicsComponent>(target, out var physics) && physics.BodyType != BodyType.Static;
    }

    private float Distance(EntityUid loader, EntityUid target)
    {
        return Vector2.Distance(_transform.GetWorldPosition(loader), _transform.GetWorldPosition(target));
    }

    private void OnClampInteract(Entity<DHPowerLoaderClampComponent> ent, ref BeforeRangedInteractEvent args)
    {
        // Виртуальный предмет сам гасит взаимодействие; работаем независимо от того, кто успел первым.
        args.Handled = true;

        var loaderUid = ent.Comp.Loader;
        if (!TryComp<DHPowerLoaderComponent>(loaderUid, out var loaderComp))
            return;

        var loader = (loaderUid, loaderComp);
        var user = args.User;
        var held = Held(loader);

        if (args.Target is { } target && target != loaderUid && target != user)
        {
            if (held == null)
            {
                if (!CanGrab(target))
                {
                    _popup.PopupEntity(Loc.GetString("dh-power-loader-cant-grab"), loaderUid, user);
                    return;
                }

                if (Distance(loaderUid, target) > loaderComp.Reach)
                {
                    _popup.PopupEntity(Loc.GetString("dh-power-loader-too-far"), loaderUid, user);
                    return;
                }

                _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, loaderComp.GrabTime,
                    new DHPowerLoaderGrabDoAfterEvent(), loaderUid, target)
                {
                    BreakOnMove = true,
                    DistanceThreshold = loaderComp.Reach + 0.5f,
                });
                return;
            }

            // С грузом — по цели: штурмовик, казённик, ПВО и т. д. заряжаются той же логикой, что и вручную.
            var reach = HasComp<DHAircraftComponent>(target) ? loaderComp.BigReach : loaderComp.Reach;
            if (Distance(loaderUid, target) > reach)
            {
                _popup.PopupEntity(Loc.GetString("dh-power-loader-too-far"), loaderUid, user);
                return;
            }

            var interact = new InteractUsingEvent(user, held.Value, target, args.ClickLocation);
            RaiseLocalEvent(target, interact);
            if (interact.Handled)
                return;
        }

        // Пустой клик или цель, которой груз не нужен: поставить груз на пол.
        if (held is not { } cargo)
            return;

        var drop = _transform.ToMapCoordinates(args.ClickLocation);
        var loaderPos = _transform.GetMapCoordinates(loaderUid);
        if (drop.MapId != loaderPos.MapId || Vector2.Distance(drop.Position, loaderPos.Position) > loaderComp.Reach)
        {
            _popup.PopupEntity(Loc.GetString("dh-power-loader-too-far"), loaderUid, user);
            return;
        }

        if (!_container.Remove(cargo, _container.GetContainer(loaderUid, loaderComp.Container)))
            return;

        _transform.SetMapCoordinates(cargo, drop);
        _audio.PlayPvs(loaderComp.GrabSound, loaderUid);
    }

    private void OnGrabDoAfter(Entity<DHPowerLoaderComponent> ent, ref DHPowerLoaderGrabDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || Held(ent) != null || !CanGrab(target))
            return;

        args.Handled = true;
        if (!_container.Insert(target, _container.GetContainer(ent, ent.Comp.Container)))
        {
            _popup.PopupEntity(Loc.GetString("dh-power-loader-cant-grab"), ent, args.User);
            return;
        }

        _audio.PlayPvs(ent.Comp.GrabSound, ent);
    }
}
