using System.Linq;
using System.Numerics;
using Content.Server._DarkestHour.Assault;
using Content.Server._DarkestHour.Atmos.Fire;
using Content.Server._DarkestHour.Shuttles.Rift;
using Content.Server.Decals;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Parallax;
using Content.Server.Shuttles.Systems;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Atmos.Fire;
using Content.Shared._DarkestHour.HeavyGuns;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Buckle.Components;
using Content.Shared.Camera;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.HeavyGuns;

/// <summary>
/// Тяжёлые орудия: сборка (казённик + ствол), заряжание, своя консоль наведения у каждой пушки (оператор сидит),
/// полёт снаряда,
/// перехват ПВО, попадание и следы взрывов. Корабельная артиллерия бьёт по точке поверхности тела, над которым
/// висит корабль; ПКО — по кораблю на орбите над телом, на поверхности которого стоит. Попадание ПКО по авианосцу
/// «Штурма» ещё и снимает его прочность.
/// </summary>
public sealed partial class DHHeavyGunSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private DHAssaultRuleSystem _assault = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private DHCelestialSystem _celestial = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private DHFireSystem _fire = default!;
    [Dependency] private Content.Server._DarkestHour.Hearing.DHHearingSystem _hearing = default!;
    [Dependency] private DHLandingSystem _landing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly SoundSpecifier FireSound = new SoundPathSpecifier("/Audio/_Mono/Weapons/Guns/Gunshots/cannon.ogg");
    private static readonly SoundSpecifier IncomingSound = new SoundPathSpecifier("/Audio/_DarkestHour/HeavyGuns/gun_orbital_travel.ogg");
    private static readonly SoundSpecifier ShipAlarmSound = new SoundPathSpecifier("/Audio/_DarkestHour/HeavyGuns/ob_alert.ogg");
    private static readonly SoundSpecifier MissileSound = new SoundPathSpecifier("/Audio/_DarkestHour/HeavyGuns/rocketpod_fire.ogg");
    private static readonly SoundSpecifier AutocannonSound = new SoundPathSpecifier("/Audio/_Mono/Weapons/Guns/Gunshots/flak_fire.ogg");
    private static readonly SoundSpecifier InterceptSound = new SoundPathSpecifier("/Audio/Effects/explosionfar.ogg");
    private static readonly SoundSpecifier LoadSound = new SoundPathSpecifier("/Audio/_DarkestHour/HeavyGuns/gun_mortar_travel.ogg");

    private static readonly string[] Scorch = { "burnt1", "burnt2", "burnt3", "burnt4" };
    private static readonly Robust.Shared.Prototypes.ProtoId<DHFlammableReagentPrototype> IncendiaryFuel = "Napalm";

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan WarningTime = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan InterceptTime = TimeSpan.FromSeconds(2.5);

    private readonly List<Strike> _strikes = new();
    private TimeSpan _nextUpdate;

    private sealed class Strike
    {
        public DHGunRole Role;
        public DHWarhead Warhead;
        public int Caliber;
        public float HullDamage;
        public float HitChance;
        public EntityUid? TargetGrid;
        public MapCoordinates Target;
        public TimeSpan ImpactAt;
        public bool Warned;
        public bool InterceptChecked;
        public bool Intercepted;

        /// <summary>Мина миномёта: свой свист на подлёте.</summary>
        public bool Mortar;

        /// <summary>Чей выстрел: своя ПВО его не сбивает.</summary>
        public DHAssaultFaction? Faction;

        /// <summary>Консоль, с которой стреляли: ей — доклад о результате (перехват, промах, попадание).</summary>
        public EntityUid? Console;
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _strikes.Clear());

        SubscribeLocalEvent<DHHeavyGunBreechComponent, ComponentInit>(OnBreechInit);
        SubscribeLocalEvent<DHHeavyGunBreechComponent, InteractUsingEvent>(OnBreechInteractUsing);
        SubscribeLocalEvent<DHHeavyGunBreechComponent, DHHeavyGunLoadDoAfterEvent>(OnBreechLoaded);
        SubscribeLocalEvent<DHHeavyGunBreechComponent, ExaminedEvent>(OnBreechExamined);

        SubscribeLocalEvent<DHInterceptorComponent, InteractUsingEvent>(OnInterceptorInteractUsing);
        SubscribeLocalEvent<DHInterceptorComponent, DHInterceptorLoadDoAfterEvent>(OnInterceptorLoaded);
        SubscribeLocalEvent<DHInterceptorComponent, ExaminedEvent>(OnInterceptorExamined);

        Subs.BuiEvents<DHHeavyGunConsoleComponent>(DHHeavyGunUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<DHHeavyGunSelectBodyMessage>(OnSelectBody);
            subs.Event<DHHeavyGunFireMessage>(OnFire);
        });

        InitializeMortar();
        InitializeFieldGun();
        InitializeRadar();
        InitializeRecon();
    }

    #region Сборка и заряжание

    private void OnBreechInit(Entity<DHHeavyGunBreechComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.Container);
    }

    /// <summary>Ствол того же калибра стоит на соседнем тайле перед казёнником и повёрнут так же.</summary>
    public bool IsAssembled(Entity<DHHeavyGunBreechComponent> breech)
    {
        var xform = Transform(breech);
        if (!xform.Anchored || xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var forward = xform.LocalRotation.RotateVec(new Vector2(0f, 1f));
        var offset = new Vector2i((int) MathF.Round(forward.X), (int) MathF.Round(forward.Y));
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates) + offset;

        var anchored = _map.GetAnchoredEntities(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (TryComp<DHHeavyGunBarrelComponent>(uid, out var barrel) &&
                barrel.Caliber == breech.Comp.Caliber &&
                (Transform(uid.Value).LocalRotation - xform.LocalRotation).Reduced().EqualsApprox(Angle.Zero, 0.1))
            {
                return true;
            }
        }

        return false;
    }

    private EntityUid? LoadedShell(Entity<DHHeavyGunBreechComponent> breech)
    {
        return _container.TryGetContainer(breech, breech.Comp.Container, out var container) &&
               container.ContainedEntities.Count > 0
            ? container.ContainedEntities[0]
            : null;
    }

    private void OnBreechInteractUsing(Entity<DHHeavyGunBreechComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<DHHeavyShellComponent>(args.Used, out var shell))
            return;

        args.Handled = true;
        if (shell.Caliber != ent.Comp.Caliber)
        {
            _popup.PopupEntity(Loc.GetString("dh-heavy-gun-wrong-caliber", ("caliber", ent.Comp.Caliber)), ent, args.User);
            return;
        }

        if (LoadedShell(ent) != null)
        {
            _popup.PopupEntity(Loc.GetString("dh-heavy-gun-already-loaded"), ent, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.LoadTime,
            new DHHeavyGunLoadDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnBreechLoaded(Entity<DHHeavyGunBreechComponent> ent, ref DHHeavyGunLoadDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } shell || LoadedShell(ent) != null ||
            !_container.TryGetContainer(ent, ent.Comp.Container, out var container))
        {
            return;
        }

        args.Handled = true;
        if (_container.Insert(shell, container))
        {
            _audio.PlayPvs(LoadSound, ent);
            UpdateAppearance(ent);
        }
    }

    private void OnBreechExamined(Entity<DHHeavyGunBreechComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(IsAssembled(ent) ? "dh-heavy-gun-examine-assembled" : "dh-heavy-gun-examine-not-assembled"));
        if (LoadedShell(ent) is { } shell && TryComp<DHHeavyShellComponent>(shell, out var shellComp))
            args.PushMarkup(Loc.GetString("dh-heavy-gun-examine-loaded", ("warhead", WarheadName(shellComp.Warhead))));
        else
            args.PushMarkup(Loc.GetString("dh-heavy-gun-examine-empty"));

        var now = _timing.CurTime;
        if (now < ent.Comp.NextShot)
            args.PushMarkup(Loc.GetString("dh-heavy-gun-examine-reloading", ("seconds", (int) (ent.Comp.NextShot - now).TotalSeconds)));
    }

    private void UpdateAppearance(Entity<DHHeavyGunBreechComponent> ent)
    {
        _appearance.SetData(ent, DHHeavyGunVisuals.Assembled, IsAssembled(ent));
        _appearance.SetData(ent, DHHeavyGunVisuals.Loaded, LoadedShell(ent) != null);
    }

    #endregion

    #region ПВО

    private void OnInterceptorInteractUsing(Entity<DHInterceptorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<DHInterceptorAmmoComponent>(args.Used, out var ammo))
            return;

        args.Handled = true;
        if (ammo.Kind != ent.Comp.Kind)
        {
            _popup.PopupEntity(Loc.GetString("dh-interceptor-wrong-ammo"), ent, args.User);
            return;
        }

        if (ent.Comp.Ammo >= ent.Comp.MaxAmmo)
        {
            _popup.PopupEntity(Loc.GetString("dh-interceptor-full"), ent, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(3),
            new DHInterceptorLoadDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnInterceptorLoaded(Entity<DHInterceptorComponent> ent, ref DHInterceptorLoadDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used ||
            !TryComp<DHInterceptorAmmoComponent>(used, out var ammo) || ent.Comp.Ammo >= ent.Comp.MaxAmmo)
        {
            return;
        }

        args.Handled = true;
        ent.Comp.Ammo = Math.Min(ent.Comp.MaxAmmo, ent.Comp.Ammo + ammo.Amount);
        Del(used);
        _audio.PlayPvs(LoadSound, ent);
    }

    private void OnInterceptorExamined(Entity<DHInterceptorComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("dh-interceptor-examine", ("ammo", ent.Comp.Ammo), ("max", ent.Comp.MaxAmmo)));
    }

    /// <summary>ПВО, запитанная от зоны захвата (АЭС), молчит, пока зона не у своей стороны.</summary>
    private bool Unpowered(EntityUid uid)
    {
        return TryComp<Content.Shared._DarkestHour.ControlPoints.DHControlPointPoweredComponent>(uid, out var powered) &&
               powered.Disabled;
    }

    /// <summary>ПВО, прикрывающие удар: на гриде-цели (корабль) или в радиусе от точки удара (поверхность).</summary>
    /// <summary>ПВО, запитанная от зоны захвата (АЭС), молчит, пока зона не у своей стороны.</summary>
    private bool Unpowered(EntityUid uid)
    {
        return TryComp<Content.Shared._DarkestHour.ControlPoints.DHControlPointPoweredComponent>(uid, out var powered) &&
               powered.Disabled;
    }

    private List<Entity<DHInterceptorComponent>> Defenders(Strike strike)
    {
        var result = new List<Entity<DHInterceptorComponent>>();
        var query = EntityQueryEnumerator<DHInterceptorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!xform.Anchored || comp.Ammo <= 0 || Unpowered(uid))
                continue;

            if (strike.Faction != null && FactionOf(uid) == strike.Faction)
                continue;

            if (strike.TargetGrid is { } grid)
            {
                if (xform.GridUid == grid)
                    result.Add((uid, comp));
            }
            else if (xform.MapID == strike.Target.MapId &&
                     Vector2.Distance(_transform.GetWorldPosition(xform), strike.Target.Position) <= comp.Radius)
            {
                result.Add((uid, comp));
            }
        }

        _random.Shuffle(result);
        return result;
    }

    private bool TryIntercept(Strike strike)
    {
        var now = _timing.CurTime;
        foreach (var defender in Defenders(strike))
        {
            if (now < defender.Comp.NextReady)
                continue;

            defender.Comp.Ammo--;
            defender.Comp.NextReady = now + defender.Comp.Cooldown;
            _audio.PlayPvs(defender.Comp.Kind == DHInterceptorKind.Missile ? MissileSound : AutocannonSound, defender,
                AudioParams.Default.WithMaxDistance(40f));

            if (_random.Prob(defender.Comp.Chance * RadarFactor(defender)))
                return true;
        }

        return false;
    }

    #endregion

    #region Консоль

    private void OnUiOpened(Entity<DHHeavyGunConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnSelectBody(Entity<DHHeavyGunConsoleComponent> ent, ref DHHeavyGunSelectBodyMessage args)
    {
        ent.Comp.SelectedBody = args.Body;
        UpdateUi(ent);
    }

    private static bool IsSeated(EntityUid user, IEntityManager entMan)
    {
        return entMan.TryGetComponent<BuckleComponent>(user, out var buckle) && buckle.Buckled;
    }

    private bool AnySeated(EntityUid console)
    {
        foreach (var actor in _ui.GetActors(console, DHHeavyGunUiKey.Key))
        {
            if (IsSeated(actor, EntityManager))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Орудие консоли (под каждую пушку своя консоль): привязанный казённик, а если его нет — ближайший казённик той
    /// же роли на том же гриде в радиусе привязки, к которому не привязана другая консоль.
    /// </summary>
    private List<Entity<DHHeavyGunBreechComponent>> Guns(Entity<DHHeavyGunConsoleComponent> console)
    {
        var result = new List<Entity<DHHeavyGunBreechComponent>>();
        var xform = Transform(console);
        if (xform.GridUid is not { } grid)
            return result;

        if (console.Comp.Gun is { } linked &&
            TryComp<DHHeavyGunBreechComponent>(linked, out var linkedComp) &&
            Transform(linked).GridUid == grid &&
            linkedComp.Role == console.Comp.Role)
        {
            result.Add((linked, linkedComp));
            return result;
        }

        console.Comp.Gun = null;
        var taken = new HashSet<EntityUid>();
        var consoles = EntityQueryEnumerator<DHHeavyGunConsoleComponent>();
        while (consoles.MoveNext(out var other, out var otherComp))
        {
            if (other != console.Owner && otherComp.Gun is { } otherGun)
                taken.Add(otherGun);
        }

        var position = _transform.GetWorldPosition(xform);
        Entity<DHHeavyGunBreechComponent>? best = null;
        var bestDistance = console.Comp.LinkRange;
        var query = EntityQueryEnumerator<DHHeavyGunBreechComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gunXform))
        {
            if (gunXform.GridUid != grid || comp.Role != console.Comp.Role || taken.Contains(uid))
                continue;

            var distance = Vector2.Distance(_transform.GetWorldPosition(gunXform), position);
            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            best = (uid, comp);
        }

        if (best is { } found)
        {
            console.Comp.Gun = found.Owner;
            result.Add(found);
        }

        return result;
    }

    private bool GunReady(Entity<DHHeavyGunBreechComponent> gun, out string status)
    {
        if (!IsAssembled(gun))
        {
            status = Loc.GetString("dh-heavy-gun-status-not-assembled");
            return false;
        }

        var now = _timing.CurTime;
        if (now < gun.Comp.NextShot)
        {
            status = Loc.GetString("dh-heavy-gun-status-reloading", ("seconds", (int) (gun.Comp.NextShot - now).TotalSeconds));
            return false;
        }

        if (LoadedShell(gun) is not { } shell || !TryComp<DHHeavyShellComponent>(shell, out var shellComp))
        {
            status = Loc.GetString("dh-heavy-gun-status-empty");
            return false;
        }

        status = Loc.GetString("dh-heavy-gun-status-ready", ("warhead", WarheadName(shellComp.Warhead)));
        return true;
    }

    /// <summary>ПКО: тело, на поверхности которого стоит консоль, и карта его системы.</summary>
    private bool TryGetSurfaceBody(EntityUid console, out DHStarSystemPrototype system, out DHCelestialBody body,
        out EntityUid systemMap, out Vector2 origin)
    {
        system = default!;
        body = default!;
        systemMap = default;
        origin = default;

        if (!TryComp<DHPlanetSurfaceMapComponent>(Transform(console).MapUid, out var surface) ||
            _celestial.GetSystemMap(surface.System) is not { } map ||
            !_celestial.TryGetSystem(map, out system, out origin))
        {
            return false;
        }

        var found = system.Bodies.Find(b => b.Id == surface.Body);
        if (found == null)
            return false;

        body = found;
        systemMap = map;
        return true;
    }

    /// <summary>ПКО: корабль на орбите тела не выше дальности или в атмосфере тела.</summary>
    private bool InReach(EntityUid target, DHStarSystemPrototype system, DHCelestialBody body, EntityUid systemMap,
        Vector2 origin, float range)
    {
        var map = Transform(target).MapUid;
        if (map != null && map == _landing.GetAtmosphere(system.ID, body.Id))
            return true;

        return map == systemMap &&
               Vector2.Distance(_transform.GetWorldPosition(target) - origin, body.Position) - body.Radius <= range;
    }

    /// <summary>Артиллерия: тела под кораблём — с орбиты в пределах дальности или тело, в атмосфере которого он висит.</summary>
    private List<(DHStarSystemPrototype System, DHCelestialBody Body, float Altitude)> ArtilleryBodies(
        Entity<DHHeavyGunConsoleComponent> ent)
    {
        var result = new List<(DHStarSystemPrototype, DHCelestialBody, float)>();
        var xform = Transform(ent);
        if (xform.GridUid is not { } grid)
            return result;

        if (TryComp<DHPlanetAtmosphereMapComponent>(xform.MapUid, out var atmosphere) &&
            _celestial.GetSystemMap(atmosphere.System) is { } atmosphereSystemMap &&
            _celestial.TryGetSystem(atmosphereSystemMap, out var atmosphereSystem, out _) &&
            atmosphereSystem.Bodies.Find(b => b.Id == atmosphere.Body) is { } below &&
            _landing.GetSurface(atmosphereSystem.ID, below.Id) != null)
        {
            result.Add((atmosphereSystem, below, DHLandingSystem.HoverAltitude));
            return result;
        }

        if (!_celestial.TryGetSystem(xform.MapUid, out var system, out var origin))
            return result;

        var position = _transform.GetWorldPosition(grid) - origin;
        foreach (var body in system.Bodies)
        {
            if (_landing.GetSurface(system.ID, body.Id) == null)
                continue;

            var altitude = Vector2.Distance(position, body.Position) - body.Radius;
            if (altitude <= ent.Comp.ArtilleryRange)
                result.Add((system, body, altitude));
        }

        return result;
    }

    private void UpdateUi(Entity<DHHeavyGunConsoleComponent> ent)
    {
        var guns = new List<DHGunEntry>();
        foreach (var gun in Guns(ent))
        {
            var ready = GunReady(gun, out var status);
            guns.Add(new DHGunEntry(GetNetEntity(gun), Name(gun), gun.Comp.Caliber, status, ready));
        }

        var targets = new List<DHGunTargetEntry>();
        var bodies = new List<DHGunBodyEntry>();
        string status2;

        if (ent.Comp.Role == DHGunRole.AntiOrbital)
        {
            if (TryGetSurfaceBody(ent, out var surfaceSystem, out var body, out var systemMap, out var origin))
            {
                // Корабли на орбите видно только РЛС; висящие в атмосфере — и так.
                var radar = RadarCovers(ent);
                var query = EntityQueryEnumerator<ShuttleComponent, TransformComponent>();
                while (query.MoveNext(out var uid, out _, out var xform))
                {
                    if (!radar || xform.MapUid != systemMap || !HasComp<MapGridComponent>(uid))
                        continue;

                    var altitude = Vector2.Distance(_transform.GetWorldPosition(xform) - origin, body.Position) - body.Radius;
                    if (altitude > ent.Comp.OrbitalRange)
                        continue;

                    var label = _shuttle.GetIFFLabel(uid) ?? Name(uid);
                    targets.Add(new DHGunTargetEntry(GetNetEntity(uid), label, altitude));
                }

                // Корабли в атмосфере над этим телом.
                if (_landing.GetAtmosphere(surfaceSystem.ID, body.Id) is { } atmosphereMap)
                {
                    var hovering = EntityQueryEnumerator<ShuttleComponent, TransformComponent>();
                    while (hovering.MoveNext(out var uid, out _, out var xform))
                    {
                        if (xform.MapUid != atmosphereMap || !HasComp<MapGridComponent>(uid))
                            continue;

                        var label = _shuttle.GetIFFLabel(uid) ?? Name(uid);
                        targets.Add(new DHGunTargetEntry(GetNetEntity(uid), label, DHLandingSystem.HoverAltitude));
                    }
                }

                targets.Sort((a, b) => a.Altitude.CompareTo(b.Altitude));
                status2 = targets.Count == 0
                    ? Loc.GetString("dh-heavy-gun-no-targets")
                    : Loc.GetString("dh-heavy-gun-targets-found", ("count", targets.Count));
            }
            else
            {
                status2 = Loc.GetString("dh-heavy-gun-not-on-surface");
            }
        }
        else
        {
            var reachable = ArtilleryBodies(ent);
            if (reachable.Count > 0 || TryComp<DHPlanetAtmosphereMapComponent>(Transform(ent).MapUid, out _) ||
                _celestial.TryGetSystem(Transform(ent).MapUid, out _, out _))
            {
                foreach (var (_, body, altitude) in reachable)
                {
                    bodies.Add(new DHGunBodyEntry(body.Id, Loc.GetString(body.Name), altitude));
                }

                if (ent.Comp.SelectedBody == null || bodies.Find(b => b.Id == ent.Comp.SelectedBody) == null)
                    ent.Comp.SelectedBody = bodies.Count > 0 ? bodies[0].Id : null;

                status2 = bodies.Count == 0
                    ? Loc.GetString("dh-heavy-gun-no-bodies", ("range", DHCelestial.FormatDistance(ent.Comp.ArtilleryRange)))
                    : Loc.GetString("dh-heavy-gun-bodies-found");
            }
            else
            {
                status2 = Loc.GetString("dh-heavy-gun-not-in-orbit");
            }
        }

        var defenses = new List<DHGunDefenseEntry>();
        var gridUid = Transform(ent).GridUid;
        var interceptors = EntityQueryEnumerator<DHInterceptorComponent, TransformComponent>();
        while (interceptors.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.GridUid == gridUid)
                defenses.Add(new DHGunDefenseEntry(Name(uid), comp.Ammo, comp.MaxAmmo));
        }

        _ui.SetUiState(ent.Owner, DHHeavyGunUiKey.Key, new DHHeavyGunConsoleState(ent.Comp.Role, AnySeated(ent), guns,
            targets, bodies, ent.Comp.SelectedBody, ent.Comp.TargetZone, defenses, ArtilleryMarks(ent), status2));
    }

    private void OnFire(Entity<DHHeavyGunConsoleComponent> ent, ref DHHeavyGunFireMessage args)
    {
        if (!IsSeated(args.Actor, EntityManager))
        {
            _popup.PopupEntity(Loc.GetString("dh-heavy-gun-need-seat"), ent, args.Actor);
            return;
        }

        var guns = Guns(ent);
        var selected = new HashSet<EntityUid>();
        foreach (var net in args.Guns)
        {
            if (TryGetEntity(net, out var uid))
                selected.Add(uid.Value);
        }

        Strike? template = null;
        if (ent.Comp.Role == DHGunRole.AntiOrbital)
        {
            if (args.Target is not { } targetNet || !TryGetEntity(targetNet, out var target) ||
                !TryGetSurfaceBody(ent, out var targetSystem, out var body, out var systemMap, out var origin) ||
                !InReach(target.Value, targetSystem, body, systemMap, origin, ent.Comp.OrbitalRange) ||
                Transform(target.Value).MapUid == systemMap && !RadarCovers(ent))
            {
                _popup.PopupEntity(Loc.GetString("dh-heavy-gun-bad-target"), ent, args.Actor);
                return;
            }

            template = new Strike { Role = DHGunRole.AntiOrbital, TargetGrid = target.Value };
        }
        else
        {
            var bodyId = args.Body;
            var reachable = ArtilleryBodies(ent).Find(r => r.Body.Id == bodyId);
            if (bodyId == null || reachable.Body == null ||
                _landing.GetSurface(reachable.System.ID, reachable.Body.Id) is not { } surface ||
                !float.IsFinite(args.Position.X) || !float.IsFinite(args.Position.Y) ||
                MathF.Abs(args.Position.X) > ent.Comp.TargetZone || MathF.Abs(args.Position.Y) > ent.Comp.TargetZone)
            {
                _popup.PopupEntity(Loc.GetString("dh-heavy-gun-bad-target"), ent, args.Actor);
                return;
            }

            template = new Strike
            {
                Role = DHGunRole.Artillery,
                Target = new MapCoordinates(args.Position, Transform(surface).MapID),
            };
        }

        var fired = 0;
        foreach (var gun in guns)
        {
            if (!selected.Contains(gun.Owner) || !GunReady(gun, out _) ||
                LoadedShell(gun) is not { } shell || !TryComp<DHHeavyShellComponent>(shell, out var shellComp))
            {
                continue;
            }

            var strike = new Strike
            {
                Role = template.Role,
                Warhead = shellComp.Warhead,
                Caliber = gun.Comp.Caliber,
                HullDamage = gun.Comp.HullDamage,
                HitChance = gun.Comp.HitChance,
                TargetGrid = template.TargetGrid,
                Target = template.Target,
                Faction = FactionOf(ent),
                Console = ent.Owner,
                ImpactAt = _timing.CurTime + gun.Comp.FlightTime + TimeSpan.FromSeconds(fired * 1.5),
            };

            if (strike.Role == DHGunRole.Artillery)
            {
                var spread = _random.NextAngle().ToVec() * _random.NextFloat(0f, gun.Comp.Spread);
                strike.Target = new MapCoordinates(strike.Target.Position + spread, strike.Target.MapId);
            }

            Del(shell);
            gun.Comp.NextShot = _timing.CurTime + gun.Comp.ReloadTime;
            Muzzle(gun);
            UpdateAppearance(gun);
            _strikes.Add(strike);
            fired++;
        }

        _popup.PopupEntity(fired > 0
            ? Loc.GetString("dh-heavy-gun-fired", ("count", fired))
            : Loc.GetString("dh-heavy-gun-nothing-fired"), ent, args.Actor);
        UpdateUi(ent);
    }

    /// <summary>Выстрел: звук далеко, тряска камеры у всех рядом.</summary>
    private void Muzzle(EntityUid gun)
    {
        _audio.PlayPvs(FireSound, gun, AudioParams.Default.WithMaxDistance(60f).WithVolume(4f));

        var center = _transform.GetMapCoordinates(gun);
        _hearing.Blast(center, 90f, 14f);
        foreach (var actor in _lookup.GetEntitiesInRange<ActorComponent>(center, 12f))
        {
            var kick = _random.NextAngle().ToVec() * 0.6f;
            _recoil.KickCamera(actor, kick);
        }
    }

    #endregion

    #region Полёт и попадание

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        for (var i = _strikes.Count - 1; i >= 0; i--)
        {
            var strike = _strikes[i];

            if (!strike.Warned && now >= strike.ImpactAt - WarningTime)
            {
                strike.Warned = true;
                Warn(strike);
            }

            if (!strike.InterceptChecked && now >= strike.ImpactAt - InterceptTime)
            {
                strike.InterceptChecked = true;
                strike.Intercepted = TryIntercept(strike);
                if (strike.Intercepted)
                {
                    InterceptedEffect(strike);
                    Report(strike, "dh-heavy-gun-report-intercepted");
                    _strikes.RemoveAt(i);
                    continue;
                }
            }

            if (now < strike.ImpactAt)
                continue;

            _strikes.RemoveAt(i);
            Impact(strike);
        }

        UpdateFieldShells();

        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;

        var breeches = EntityQueryEnumerator<DHHeavyGunBreechComponent>();
        while (breeches.MoveNext(out var uid, out var comp))
        {
            UpdateAppearance((uid, comp));
        }

        UpdateAirDefence(now);

        var consoles = EntityQueryEnumerator<DHHeavyGunConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var comp))
        {
            if (_ui.IsUiOpen(uid, DHHeavyGunUiKey.Key))
                UpdateUi((uid, comp));
        }

        var fieldGuns = EntityQueryEnumerator<DHFieldGunComponent>();
        while (fieldGuns.MoveNext(out var uid, out var comp))
        {
            if (_ui.IsUiOpen(uid, DHFieldGunUiKey.Key))
                UpdateFieldGunUi((uid, comp));
        }

        var scans = EntityQueryEnumerator<Content.Shared._DarkestHour.Recon.DHOrbitalScanConsoleComponent>();
        while (scans.MoveNext(out var uid, out var comp))
        {
            if (_ui.IsUiOpen(uid, Content.Shared._DarkestHour.Recon.DHOrbitalScanUiKey.Key))
                UpdateScanUi((uid, comp));
        }

        var mortars = EntityQueryEnumerator<DHMortarComponent>();
        while (mortars.MoveNext(out var uid, out var comp))
        {
            if (_ui.IsUiOpen(uid, DHMortarUiKey.Key))
                UpdateMortarUi((uid, comp));
        }
    }

    /// <summary>ПВО на поверхности бьёт по авианосцу, висящему в атмосфере над ней в пределах дальности.</summary>
    private void UpdateAirDefence(TimeSpan now)
    {
        var interceptors = EntityQueryEnumerator<DHInterceptorComponent, TransformComponent>();
        while (interceptors.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.AirRange <= 0f || comp.Ammo <= 0 || now < comp.NextReady || !xform.Anchored || Unpowered(uid) ||
                !TryComp<DHPlanetSurfaceMapComponent>(xform.MapUid, out var surface) ||
                _landing.GetAtmosphere(surface.System, surface.Body) is not { } sky)
            {
                continue;
            }

            var position = _transform.GetWorldPosition(xform);
            EntityUid? target = null;
            var carriers = EntityQueryEnumerator<DHAssaultCarrierComponent, TransformComponent>();
            while (carriers.MoveNext(out var carrier, out var carrierComp, out var carrierXform))
            {
                if (carrierComp.Downed || carrierXform.MapUid != sky ||
                    Vector2.Distance(_transform.GetWorldPosition(carrierXform), position) > comp.AirRange)
                {
                    continue;
                }

                target = carrier;
                break;
            }

            // Челноки десанта на подлёте к ЗП.
            if (target == null)
            {
                var dropships = EntityQueryEnumerator<DHDropshipComponent, TransformComponent>();
                while (dropships.MoveNext(out var ship, out var shipComp, out var shipXform))
                {
                    if (shipComp.State != DHDropshipState.InFlight || shipXform.MapUid != sky ||
                        Vector2.Distance(_transform.GetWorldPosition(shipXform), position) > comp.AirRange)
                    {
                        continue;
                    }

                    target = ship;
                    break;
                }
            }

            if (target is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
                continue;

            comp.Ammo--;
            comp.NextReady = now + comp.Cooldown;
            _audio.PlayPvs(comp.Kind == DHInterceptorKind.Missile ? MissileSound : AutocannonSound, uid,
                AudioParams.Default.WithMaxDistance(40f));

            if (!_random.Prob(comp.AirChance * RadarFactor(uid)))
                continue;

            var tiles = _map.GetAllTiles(grid, gridComp).ToList();
            if (tiles.Count > 0)
            {
                var tile = _random.Pick(tiles);
                var hit = _map.GridTileToWorld(grid, gridComp, tile.GridIndices);
                _explosion.QueueExplosion(hit, "Default", comp.Kind == DHInterceptorKind.Missile ? 80f : 30f, 4f, 10f,
                    null);
            }

            if (HasComp<DHAssaultCarrierComponent>(grid))
                _assault.DamageCarrier(grid, comp.AirDamage);
        }
    }

    private void Warn(Strike strike)
    {
        if (strike.TargetGrid is { } grid)
        {
            if (Exists(grid))
                _audio.PlayGlobal(ShipAlarmSound, Filter.Empty().AddInGrid(grid), true);
            return;
        }

        if (!_map.TryGetMap(strike.Target.MapId, out var map))
            return;

        var coordinates = new EntityCoordinates(map.Value, strike.Target.Position);
        _audio.PlayPvs(strike.Mortar ? MortarIncomingSound : IncomingSound, coordinates,
            AudioParams.Default.WithMaxDistance(30f));
        Spawn("DHStrikeMarker", strike.Target);
    }

    private void InterceptedEffect(Strike strike)
    {
        var coordinates = strike.TargetGrid is { } grid && Exists(grid)
            ? _transform.GetMapCoordinates(grid)
            : strike.Target;

        if (_map.TryGetMap(coordinates.MapId, out var map))
        {
            _audio.PlayPvs(InterceptSound, new EntityCoordinates(map.Value, coordinates.Position),
                AudioParams.Default.WithMaxDistance(60f));
        }
    }

    private void Impact(Strike strike)
    {
        if (strike.Role == DHGunRole.Artillery)
        {
            Detonate(strike.Warhead, strike.Caliber, strike.Target, 1f, false);
            Report(strike, "dh-heavy-gun-report-impact");
            return;
        }

        // ПКО: корабль мог уйти — промах.
        if (strike.TargetGrid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp) ||
            !_random.Prob(strike.HitChance))
        {
            Report(strike, "dh-heavy-gun-report-miss");
            return;
        }

        var tiles = _map.GetAllTiles(grid, gridComp).ToList();
        if (tiles.Count == 0)
            return;

        var tile = _random.Pick(tiles);
        var target = _map.GridTileToWorld(grid, gridComp, tile.GridIndices);
        Detonate(strike.Warhead, strike.Caliber, target, 0.6f, true);

        var damage = strike.HullDamage * strike.Warhead switch
        {
            DHWarhead.ArmorPiercing => 1.5f,
            DHWarhead.Incendiary => 0.7f,
            DHWarhead.Cluster => 0.8f,
            _ => 1f,
        };
        _assault.DamageCarrier(grid, damage);
        Report(strike, "dh-heavy-gun-report-hit");
    }

    /// <summary>Доклад стрелку у консоли: чем кончился выстрел.</summary>
    private void Report(Strike strike, string loc)
    {
        if (strike.Console is not { } console || !Exists(console))
            return;

        var x = (int) MathF.Round(strike.Target.Position.X);
        var y = (int) MathF.Round(strike.Target.Position.Y);
        var text = Loc.GetString(loc, ("x", x), ("y", y));
        foreach (var actor in _ui.GetActors(console, DHHeavyGunUiKey.Key))
        {
            _popup.PopupEntity(text, console, actor, PopupType.Medium);
        }
    }

    /// <summary>
    /// Грунт планеты подгружается только вокруг живых игроков (призраки его не грузят): там, где никого нет, тайлов
    /// ещё нет и взрыву нечего ломать. Перед разрывом ставим грунт биома в зоне поражения (как под базой).
    /// </summary>
    private void PrepareGround(MapCoordinates target, float radius)
    {
        if (!_map.TryGetMap(target.MapId, out var map) || !TryComp<BiomeComponent>(map, out var biome) ||
            !TryComp<MapGridComponent>(map, out var grid))
        {
            return;
        }

        _biome.ReserveTiles(map.Value, Box2.CenteredAround(target.Position, new Vector2(radius * 2f)),
            new List<(Vector2i Index, Tile Tile)>(), biome, grid);
    }

    /// <summary>Зажигательный: огонь на тайлах вокруг разрыва (своя система огня, горит и без кислорода).</summary>
    private void Ignite(MapCoordinates center, float radius, float duration)
    {
        if (!_protoManager.TryIndex(IncendiaryFuel, out var fuel))
            return;

        var r = (int) MathF.Ceiling(radius);
        for (var x = -r; x <= r; x++)
        {
            for (var y = -r; y <= r; y++)
            {
                if (x * x + y * y > radius * radius || !_random.Prob(0.8f))
                    continue;

                var point = new MapCoordinates(center.Position + new Vector2(x, y), center.MapId);
                if (!_map.TryFindGridAt(point, out var gridUid, out var grid))
                    continue;

                var tile = _map.TileIndicesFor(gridUid, grid, point);
                _fire.SpawnFire(gridUid, tile, fuel, duration * _random.NextFloat(0.7f, 1.2f));
            }
        }
    }

    /// <summary>Подрыв боеголовки и следы на земле. <paramref name="vacuum"/> — можно пробить корпус в космос.</summary>
    private void Detonate(DHWarhead warhead, int caliber, MapCoordinates target, float scale, bool vacuum)
    {
        var intensity = caliber * 1.2f * scale;
        var radius = 2f + caliber / 150f;

        PrepareGround(target, Math.Min(4f + caliber / 40f, 20f));

        // Разрыв глушит всех вокруг (осветительная мина — тихая).
        if (warhead != DHWarhead.Flare)
            _hearing.Blast(target, caliber * 0.8f * scale, 6f + caliber / 25f, 0.4f + caliber / 500f);

        switch (warhead)
        {
            case DHWarhead.Flare:
                Flare(target);
                return;
            case DHWarhead.ArmorPiercing:
                _explosion.QueueExplosion(target, "Default", intensity * 0.6f, 10f, 60f, null, canCreateVacuum: vacuum);
                radius *= 0.6f;
                break;
            case DHWarhead.Incendiary:
                _explosion.QueueExplosion(target, "FireBomb", intensity * 0.5f, 3f, 18f, null, canCreateVacuum: vacuum);
                Ignite(target, Math.Min(1.5f + caliber * scale / 60f, 8f), Math.Min(15f + caliber / 10f, 45f));
                break;
            case DHWarhead.Cluster:
                var bomblets = 6 + caliber / 100;
                var spread = 3f + caliber / 100f;
                for (var i = 0; i < bomblets; i++)
                {
                    var offset = _random.NextAngle().ToVec() * _random.NextFloat(0f, spread);
                    var point = new MapCoordinates(target.Position + offset, target.MapId);
                    _explosion.QueueExplosion(point, "Default", intensity * 0.15f, 4f, 10f, null, canCreateVacuum: vacuum);
                    ScorchAt(point, 1.5f);
                }

                radius = spread;
                break;
            default:
                _explosion.QueueExplosion(target, "Default", intensity, 3f, 25f, null, canCreateVacuum: vacuum);
                break;
        }

        ScorchAt(target, radius);
    }

    /// <summary>Следы взрыва: подпалины на тайлах вокруг.</summary>
    private void ScorchAt(MapCoordinates center, float radius)
    {
        var r = (int) MathF.Ceiling(radius);
        for (var x = -r; x <= r; x++)
        {
            for (var y = -r; y <= r; y++)
            {
                if (x * x + y * y > radius * radius || !_random.Prob(0.65f))
                    continue;

                var point = new MapCoordinates(center.Position + new Vector2(x, y), center.MapId);
                if (!_map.TryFindGridAt(point, out var gridUid, out var grid))
                    continue;

                var tile = _map.TileIndicesFor(gridUid, grid, point);
                var coordinates = _map.GridTileToLocal(gridUid, grid, tile).Offset(new Vector2(-0.5f, -0.5f));
                _decals.TryAddDecal(_random.Pick(Scorch), coordinates, out _, rotation: _random.NextAngle(),
                    cleanable: true);
            }
        }
    }

    #endregion

    private string WarheadName(DHWarhead warhead)
    {
        return Loc.GetString(warhead switch
        {
            DHWarhead.ArmorPiercing => "dh-warhead-armor-piercing",
            DHWarhead.Incendiary => "dh-warhead-incendiary",
            DHWarhead.Cluster => "dh-warhead-cluster",
            DHWarhead.Flare => "dh-warhead-flare",
            _ => "dh-warhead-high-explosive",
        });
    }
}
