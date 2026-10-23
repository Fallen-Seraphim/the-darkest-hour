using System.Numerics;
using Content.Server.Shuttles.Systems;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Shuttles.Rift;

/// <summary>
/// Навигационная консоль разрывных прыжков: карта системы, выбор системы и точки выхода, прыжок.
/// </summary>
public sealed class DHRiftNavSystem : EntitySystem
{
    [Dependency] private DHCelestialSystem _celestial = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private DHRiftSystem _rift = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(2);

    /// <summary>Запас над твёрдой поверхностью тела, ближе которого выйти из прыжка нельзя.</summary>
    private const float SurfaceMargin = 300f;

    /// <summary>Радиация в точке выхода, выше которой прыжок запрещён.</summary>
    private const float MaxExitRadiation = 2f;

    private const int MaxGrids = 64;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<DHRiftNavConsoleComponent>(DHRiftNavUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<DHRiftNavViewSystemMessage>(OnViewSystem);
            subs.Event<DHRiftNavJumpMessage>(OnJump);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;

        var query = EntityQueryEnumerator<DHRiftNavConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_ui.IsUiOpen(uid, DHRiftNavUiKey.Key))
                UpdateUi((uid, comp));
        }
    }

    private void OnUiOpened(Entity<DHRiftNavConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        ent.Comp.ViewedSystem = null;
        UpdateUi(ent);
    }

    private void OnViewSystem(Entity<DHRiftNavConsoleComponent> ent, ref DHRiftNavViewSystemMessage args)
    {
        if (!_proto.HasIndex<DHStarSystemPrototype>(args.System))
            return;

        ent.Comp.ViewedSystem = args.System;
        UpdateUi(ent);
    }

    #region Системы

    /// <summary>Id домашней системы (прототип с home).</summary>
    private string? HomeSystem()
    {
        foreach (var proto in _proto.EnumeratePrototypes<DHStarSystemPrototype>())
        {
            if (proto.Home)
                return proto.ID;
        }

        return null;
    }

    /// <summary>Система, в которой находится карта; карты без компонента — домашняя система.</summary>
    private string? CurrentSystem(EntityUid? map)
    {
        if (map != null && TryComp<DHStarSystemMapComponent>(map, out var comp) && comp.System != null)
            return comp.System.Value.Id;

        if (map != null && TryComp<DHPlanetSurfaceMapComponent>(map, out var surface))
            return surface.System;

        return HomeSystem();
    }

    /// <summary>Карта системы. Для домашней — карта станции (с телами), а не планеты и экспедиции.</summary>
    private EntityUid? MapOf(string system)
    {
        var query = EntityQueryEnumerator<DHStarSystemMapComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.System?.Id == system)
                return uid;
        }

        return null;
    }

    #endregion

    #region Интерфейс

    private void UpdateUi(Entity<DHRiftNavConsoleComponent> ent)
    {
        var shuttle = Transform(ent).GridUid;
        var shuttleMap = shuttle != null ? Transform(shuttle.Value).MapUid : null;
        var current = CurrentSystem(shuttleMap);
        var viewed = ent.Comp.ViewedSystem ?? current ?? string.Empty;

        var systems = new List<DHRiftNavSystemEntry>();
        foreach (var proto in _proto.EnumeratePrototypes<DHStarSystemPrototype>())
        {
            systems.Add(new DHRiftNavSystemEntry(proto.ID, Loc.GetString(proto.Name), proto.ID == current));
        }

        systems.Sort((a, b) => b.Current.CompareTo(a.Current) != 0
            ? b.Current.CompareTo(a.Current)
            : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));

        var grids = new List<DHRiftNavGrid>();
        if (MapOf(viewed) is { } viewedMap && _celestial.TryGetSystem(viewedMap, out _, out var origin))
            CollectGrids(viewedMap, origin, shuttle, grids);

        var rift = viewed != current;
        var canJump = CanJump(ent, shuttle, viewed, rift, out var status);

        _ui.SetUiState(ent.Owner, DHRiftNavUiKey.Key,
            new DHRiftNavState(systems, viewed, grids, status, canJump, rift));
    }

    private void CollectGrids(EntityUid map, Vector2 origin, EntityUid? self, List<DHRiftNavGrid> grids)
    {
        var query = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid != map || grids.Count >= MaxGrids)
                continue;

            var isSelf = uid == self;
            TryComp<IFFComponent>(uid, out var iff);
            var label = _shuttle.GetIFFLabel(uid, isSelf, iff);
            if (label == null && !isSelf)
                continue;

            var position = _transform.GetWorldPosition(xform) - origin;
            grids.Add(new DHRiftNavGrid(label ?? string.Empty, position, _shuttle.GetIFFColor(uid, isSelf, iff), isSelf));
        }
    }

    private bool CanJump(EntityUid console, EntityUid? shuttle, string viewed, bool rift, out string status)
    {
        if (shuttle == null || !TryComp<ShuttleComponent>(shuttle, out var shuttleComp) || !shuttleComp.Enabled)
        {
            status = Loc.GetString("dh-rift-nav-no-shuttle");
            return false;
        }

        if (!_shuttle.CanFTL(shuttle.Value, out var reason))
        {
            status = reason;
            return false;
        }

        if (HasComp<DHPlanetSurfaceMapComponent>(Transform(shuttle.Value).MapUid))
        {
            status = Loc.GetString("dh-landing-use-console");
            return false;
        }

        if (MapOf(viewed) == null)
        {
            status = Loc.GetString("dh-rift-nav-no-map");
            return false;
        }

        if (rift)
        {
            if (!_rift.TryGetRiftGear(shuttle.Value, out _, out _, out reason))
            {
                status = reason;
                return false;
            }

            status = Loc.GetString("dh-rift-nav-ready-rift");
            return true;
        }

        status = Loc.GetString("dh-rift-nav-ready");
        return true;
    }

    #endregion

    #region Прыжок

    private void OnJump(Entity<DHRiftNavConsoleComponent> ent, ref DHRiftNavJumpMessage args)
    {
        var shuttle = Transform(ent).GridUid;
        if (shuttle == null || !TryComp<ShuttleComponent>(shuttle, out var shuttleComp) || !shuttleComp.Enabled)
            return;

        if (!_shuttle.CanFTL(shuttle.Value, out var reason))
        {
            _popup.PopupEntity(reason, ent, PopupType.MediumCaution);
            return;
        }

        if (!float.IsFinite(args.Position.X) || !float.IsFinite(args.Position.Y) ||
            MapOf(args.System) is not { } targetMap ||
            !_celestial.TryGetSystem(targetMap, out var system, out var origin))
        {
            return;
        }

        // Выход из прыжка — не внутри тела и не в смертельной радиации.
        foreach (var body in system.Bodies)
        {
            var distance = Vector2.Distance(args.Position, body.Position);
            if (distance < DHCelestial.SolidRadius(body) + SurfaceMargin)
            {
                _popup.PopupEntity(Loc.GetString("dh-rift-nav-exit-body", ("body", Loc.GetString(body.Name))), ent, PopupType.MediumCaution);
                return;
            }

            if (DHCelestial.RadiationAt(body, distance) > MaxExitRadiation)
            {
                _popup.PopupEntity(Loc.GetString("dh-rift-nav-exit-radiation", ("body", Loc.GetString(body.Name))), ent, PopupType.MediumCaution);
                return;
            }
        }

        var targetMapId = Transform(targetMap).MapID;
        if (!_shuttle.CanFTLTo(shuttle.Value, targetMapId, ent))
        {
            _popup.PopupEntity(Loc.GetString("dh-rift-nav-no-map"), ent, PopupType.MediumCaution);
            return;
        }

        // Разрыв (если система другая) проверяет и заряжает DHRiftSystem.
        var riftEv = new DHConsoleFTLTargetEvent(shuttle.Value, targetMapId, ent.Owner);
        RaiseLocalEvent(shuttle.Value, ref riftEv, true);
        if (riftEv.Cancelled)
            return;

        var coordinates = new EntityCoordinates(targetMap, origin + args.Position);
        var angle = Angle.Zero;
        if (!_shuttle.FTLFree(shuttle.Value, coordinates, angle, null))
        {
            _popup.PopupEntity(Loc.GetString("dh-rift-nav-exit-blocked"), ent, PopupType.MediumCaution);
            return;
        }

        if (TryComp<PhysicsComponent>(shuttle, out var physics))
            coordinates = coordinates.Offset(-physics.LocalCenter);

        _shuttle.FTLToCoordinates(shuttle.Value, shuttleComp, coordinates, angle);
        UpdateUi(ent);
    }

    #endregion
}
