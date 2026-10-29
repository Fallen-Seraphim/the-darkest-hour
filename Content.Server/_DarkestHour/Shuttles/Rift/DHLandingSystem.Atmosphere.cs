using System.Numerics;
using Content.Shared._DarkestHour.HeavyGuns;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Atmos;
using Content.Shared.Camera;
using Content.Shared.Examine;
using Content.Shared.Ghost.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Parallax;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Shuttles.Rift;

/// <summary>Корабль в пути между орбитой, атмосферой и поверхностью: где окажется и когда.</summary>
[RegisterComponent]
public sealed partial class DHAtmosphereTransitComponent : Component
{
    [DataField]
    public EntityCoordinates Target;

    [DataField]
    public TimeSpan At;

    /// <summary>Строка статуса консоли посадки на время перехода.</summary>
    [DataField]
    public string Status = string.Empty;

    [DataField]
    public TimeSpan NextShake;
}

/// <summary>Парашютист: выпрыгнул из корабля в атмосфере, приземлится в <see cref="LandAt"/>.</summary>
[RegisterComponent]
public sealed partial class DHFallingComponent : Component
{
    [DataField]
    public TimeSpan LandAt;
}

public sealed partial class DHLandingSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    /// <summary>Высота слоя атмосферы над поверхностью (для ПКО и подписей), м.</summary>
    public const float HoverAltitude = 2000f;

    public const string DefaultSky = "DHSky";

    private static readonly TimeSpan DescentTime = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan ClimbTime = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan LandTime = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan TakeOffTime = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan FallTime = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan FallCheckInterval = TimeSpan.FromSeconds(0.25);

    private static readonly SoundSpecifier TransitSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_begin.ogg");
    private static readonly SoundSpecifier JumpSound = new SoundCollectionSpecifier("MetalThud");
    private static readonly SoundSpecifier LandSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    private readonly Dictionary<(string System, string Body), EntityUid> _atmospheres = new();
    private TimeSpan _nextFallCheck;

    private void InitializeAtmosphere()
    {
        SubscribeLocalEvent<DHDropBeaconComponent, UseInHandEvent>(OnBeaconUse);
        SubscribeLocalEvent<DHDropBeaconComponent, ExaminedEvent>(OnBeaconExamined);
    }

    #region Слой атмосферы

    /// <summary>Карта слоя атмосферы тела (если уже есть).</summary>
    public EntityUid? GetAtmosphere(string system, string body)
    {
        return _atmospheres.TryGetValue((system, body), out var map) && !TerminatingOrDeleted(map) ? map : null;
    }

    /// <summary>Карта слоя атмосферы тела по id системы и тела (создаётся при необходимости).</summary>
    public EntityUid? EnsureAtmosphere(string system, string body)
    {
        return TryGetBody(system, body, out var proto, out var found) ? GetOrCreateAtmosphere(proto, found) : null;
    }

    /// <summary>Тряска у всех на борту грида.</summary>
    public void Shake(EntityUid grid, float strength)
    {
        ShakeGrid(grid, strength);
    }

    /// <summary>Карта слоя атмосферы тела: небо, воздух тела, без тяготения (вне корабля — свободное падение).</summary>
    private EntityUid? GetOrCreateAtmosphere(DHStarSystemPrototype system, DHCelestialBody body)
    {
        if (GetAtmosphere(system.ID, body.Id) is { } existing)
            return existing;

        var mapUid = _map.CreateMap(out _);
        _meta.SetEntityName(mapUid, Loc.GetString("dh-landing-atmosphere-map", ("body", Loc.GetString(body.Name))));

        var parallax = EnsureComp<ParallaxComponent>(mapUid);
        parallax.Parallax = body.Sky ?? DefaultSky;
        Dirty(mapUid, parallax);

        if (body.SurfaceGases.Count > 0)
        {
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            foreach (var (gas, amount) in body.SurfaceGases)
            {
                moles[(int) gas] = amount;
            }

            _atmos.SetMapAtmosphere(mapUid, false, new GasMixture(moles, body.SurfaceTemperature));
        }
        else
        {
            _atmos.SetMapAtmosphere(mapUid, true, GasMixture.SpaceGas);
        }

        var atmosphere = EnsureComp<DHPlanetAtmosphereMapComponent>(mapUid);
        atmosphere.System = system.ID;
        atmosphere.Body = body.Id;
        Dirty(mapUid, atmosphere);

        _atmospheres[(system.ID, body.Id)] = mapUid;
        return mapUid;
    }

    #endregion

    #region Переходы орбита — атмосфера — поверхность

    /// <summary>Мировая точка центра масс корабля.</summary>
    private Vector2 ShuttleCenter(EntityUid shuttle)
    {
        var xform = Transform(shuttle);
        var position = _transform.GetWorldPosition(xform);
        if (TryComp<PhysicsComponent>(shuttle, out var physics))
            position += _transform.GetWorldRotation(xform).RotateVec(physics.LocalCenter);
        return position;
    }

    /// <summary>Начать переход: через <paramref name="delay"/> центр корабля окажется в <paramref name="center"/>.</summary>
    private void StartTransit(EntityUid shuttle, EntityCoordinates center, TimeSpan delay, string status)
    {
        var transit = EnsureComp<DHAtmosphereTransitComponent>(shuttle);
        transit.Target = center;
        transit.At = _timing.CurTime + delay;
        transit.Status = status;
        transit.NextShake = _timing.CurTime;

        _audio.PlayGlobal(TransitSound, Filter.Empty().AddInGrid(shuttle), true);
    }

    /// <summary>
    /// Переставить корабль центром в точку (на любую карту) и погасить его скорость. Без FTL: вместо гиперпространства
    /// экипаж видит небо атмосферы.
    /// </summary>
    /// <summary>
    /// Поставить объект на грунт поверхности (грид самой карты планеты) и прикрепить к тайлу: если тайла ещё нет
    /// (биом не подгружен), сначала ставится грунт биома.
    /// </summary>
    public EntityUid SpawnOnGround(EntProtoId proto, EntityUid map, Vector2 position)
    {
        if (!TryComp<MapGridComponent>(map, out var grid))
            return Spawn(proto, new EntityCoordinates(map, position));

        var tile = _map.WorldToTile(map, grid, position);
        if (!_map.TryGetTileRef(map, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
        {
            _biome.ReserveTiles(map, Box2.CenteredAround(_map.GridTileToWorldPos(map, grid, tile), new Vector2(0.5f)),
                new List<(Vector2i Index, Tile Tile)>());
        }

        var uid = Spawn(proto, _map.GridTileToLocal(map, grid, tile));
        var xform = Transform(uid);
        if (!xform.Anchored)
            _transform.AnchorEntity((uid, xform), (map, grid), tile);

        return uid;
    }

    public void PlaceShuttle(EntityUid shuttle, EntityCoordinates center)
    {
        var xform = Transform(shuttle);
        var target = center;
        if (TryComp<PhysicsComponent>(shuttle, out var physics))
            target = center.Offset(-_transform.GetWorldRotation(xform).RotateVec(physics.LocalCenter));

        _transform.SetCoordinates(shuttle, xform, target);
        if (physics != null)
        {
            _physics.SetLinearVelocity(shuttle, Vector2.Zero, body: physics);
            _physics.SetAngularVelocity(shuttle, 0f, body: physics);
        }
    }

    private void UpdateTransits(TimeSpan now)
    {
        var query = EntityQueryEnumerator<DHAtmosphereTransitComponent>();
        var arrived = new List<(EntityUid, EntityCoordinates)>();
        while (query.MoveNext(out var uid, out var transit))
        {
            if (now >= transit.At)
            {
                arrived.Add((uid, transit.Target));
                continue;
            }

            // Тряска при входе в атмосферу и посадке.
            if (now >= transit.NextShake)
            {
                transit.NextShake = now + TimeSpan.FromSeconds(1.5);
                ShakeGrid(uid, 0.4f);
            }
        }

        foreach (var (uid, target) in arrived)
        {
            RemComp<DHAtmosphereTransitComponent>(uid);
            if (target.EntityId.IsValid() && !TerminatingOrDeleted(target.EntityId))
                PlaceShuttle(uid, target);
            ShakeGrid(uid, 1f);
        }
    }

    private void ShakeGrid(EntityUid grid, float strength)
    {
        var actors = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actors.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == grid)
                _recoil.KickCamera(uid, _random.NextAngle().ToVec() * strength);
        }
    }

    #endregion

    #region Прыжок с парашютом

    private void UpdateAtmosphere(TimeSpan now)
    {
        UpdateTransits(now);

        if (now < _nextFallCheck)
            return;

        _nextFallCheck = now + FallCheckInterval;

        // Кто оказался вне корабля в слое атмосферы — падает.
        var jumpers = new List<EntityUid>();
        foreach (var map in _atmospheres.Values)
        {
            if (TerminatingOrDeleted(map))
                continue;

            var children = Transform(map).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (HasComp<MapGridComponent>(child) || HasComp<DHFallingComponent>(child) ||
                    HasComp<GhostComponent>(child) || HasComp<ProjectileComponent>(child) ||
                    !HasComp<MobStateComponent>(child) && !HasComp<ItemComponent>(child) &&
                    !HasComp<DHParadroppableComponent>(child))
                {
                    continue;
                }

                jumpers.Add(child);
            }
        }

        foreach (var jumper in jumpers)
        {
            EnsureComp<DHFallingComponent>(jumper).LandAt = now + FallTime;
            if (HasComp<MobStateComponent>(jumper))
            {
                _popup.PopupEntity(Loc.GetString("dh-paradrop-jump"), jumper, jumper, PopupType.Large);
                _audio.PlayPvs(JumpSound, jumper);
            }
        }

        var falling = EntityQueryEnumerator<DHFallingComponent, TransformComponent>();
        var landing = new List<EntityUid>();
        while (falling.MoveNext(out var uid, out var fall, out var xform))
        {
            // Поймали обратно на корабль.
            if (xform.ParentUid != xform.MapUid)
            {
                landing.Add(uid);
                continue;
            }

            if (now >= fall.LandAt)
                landing.Add(uid);
        }

        foreach (var uid in landing)
        {
            RemComp<DHFallingComponent>(uid);
            var xform = Transform(uid);
            if (xform.ParentUid != xform.MapUid ||
                !TryComp<DHPlanetAtmosphereMapComponent>(xform.MapUid, out var atmosphere) ||
                !TryGetBody(atmosphere.System, atmosphere.Body, out var system, out var body) ||
                GetOrCreateSurface(system, body) is not { } surface)
            {
                continue;
            }

            Land(uid, surface, _transform.GetWorldPosition(xform));
        }
    }

    /// <summary>Приземление: у ближайшего включённого маяка высадки в пределах его досягаемости или под собой.</summary>
    private void Land(EntityUid uid, EntityUid surface, Vector2 above)
    {
        var target = above + _random.NextAngle().ToVec() * _random.NextFloat(0f, 6f);

        var best = float.MaxValue;
        var beacons = EntityQueryEnumerator<DHDropBeaconComponent, TransformComponent>();
        while (beacons.MoveNext(out _, out var beacon, out var xform))
        {
            if (!beacon.Active || xform.MapUid != surface)
                continue;

            var position = _transform.GetWorldPosition(xform);
            var distance = Vector2.Distance(position, above);
            if (distance > beacon.Range || distance >= best)
                continue;

            best = distance;
            target = position + _random.NextAngle().ToVec() * _random.NextFloat(1f, 3f);
        }

        _transform.SetCoordinates(uid, new EntityCoordinates(surface, target));
        _transform.AttachToGridOrMap(uid);

        if (TryComp<PhysicsComponent>(uid, out var physics))
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);

        if (!HasComp<MobStateComponent>(uid))
        {
            // Тяжёлый груз с парашютом (пушка, ящик): встаёт как стоял, со звуком посадки.
            if (HasComp<DHParadroppableComponent>(uid))
                _audio.PlayPvs(LandSound, uid);
            return;
        }

        _audio.PlayPvs(LandSound, uid);
        _stun.TryKnockdown(uid, TimeSpan.FromSeconds(1.5), true);
        _popup.PopupEntity(Loc.GetString("dh-paradrop-landed"), uid, uid, PopupType.Medium);
    }

    #endregion

    #region Маяк высадки

    private void OnBeaconUse(Entity<DHDropBeaconComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Active = !ent.Comp.Active;
        _appearance.SetData(ent, DHDropBeaconVisuals.Active, ent.Comp.Active);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Active ? "dh-drop-beacon-on" : "dh-drop-beacon-off"), ent, args.User);
    }

    private void OnBeaconExamined(Entity<DHDropBeaconComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Active ? "dh-drop-beacon-examine-on" : "dh-drop-beacon-examine-off",
            ("range", (int) ent.Comp.Range)));
    }

    #endregion
}
