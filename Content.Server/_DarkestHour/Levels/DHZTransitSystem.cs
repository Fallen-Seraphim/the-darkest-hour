using System.Numerics;
using Content.Server._DarkestHour.Medical.Trauma;
using Content.Shared._DarkestHour.Levels;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Item;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Levels;

/// <summary>
/// Z-уровни на сервере: лестничные пролёты (шагнул по ступеням — уже этажом выше/ниже), падение с края (нет пола —
/// летишь до первого пола внизу, урон за каждый этаж), взгляд на соседние уровни (кнопки «Посмотреть вверх/вниз»:
/// камера на том уровне в той же точке) и пролом крыши ударом с орбиты.
/// </summary>
public sealed partial class DHZTransitSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private Content.Server.Atmos.EntitySystems.AtmosphereSystem _atmos = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedRoofSystem _roof = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DHTraumaSystem _trauma = default!;
    [Dependency] private ViewSubscriberSystem _view = default!;
    [Dependency] private DHZLevelSystem _z = default!;

    private const string CameraProto = "DHZLevelCamera";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly string[] FallParts = { "LegLeft", "LegRight", "FootLeft", "FootRight" };
    private static readonly SoundSpecifier StairsSound = new SoundCollectionSpecifier("FootstepCatwalk");
    private static readonly SoundSpecifier FallSound = new SoundPathSpecifier("/Audio/Effects/hit_kick.ogg");

    /// <summary>Урон за каждый пролетевший этаж.</summary>
    private const float FallDamagePerLevel = 20f;

    /// <summary>Насколько близко к лестнице/люку/проёму надо стоять, чтобы посмотреть через него.</summary>
    private const float PassageLookRange = 1.6f;

    /// <summary>Отошёл от места, откуда смотрел, дальше этого — взгляд сбрасывается.</summary>
    private const float LookLeaveRange = 1.25f;

    private static readonly TimeSpan StairsInterval = TimeSpan.FromSeconds(0.1);
    private static readonly TimeSpan FallInterval = TimeSpan.FromSeconds(0.2);
    private static readonly TimeSpan ViewInterval = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan StairsCooldown = TimeSpan.FromSeconds(0.7);

    /// <summary>Как близко центр идущего к центру пролёта (0,45 от края его тела, как было).</summary>
    private const float StairsReach = 0.8f;

    private TimeSpan _nextStairs;
    private TimeSpan _nextFall;
    private TimeSpan _nextView;
    private readonly Dictionary<EntityUid, TimeSpan> _stairsCooldown = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHZViewerComponent, MapInitEvent>(OnViewerMapInit);
        SubscribeLocalEvent<DHZViewerComponent, ComponentShutdown>(OnViewerShutdown);
        SubscribeLocalEvent<DHZViewerComponent, MobStateChangedEvent>(OnViewerMobState);
        SubscribeLocalEvent<DHZViewerComponent, DHLookUpActionEvent>(OnLookUp);
        SubscribeLocalEvent<DHZViewerComponent, DHLookDownActionEvent>(OnLookDown);
        SubscribeLocalEvent<DHZCameraComponent, ComponentShutdown>(OnCameraShutdown);

        SubscribeLocalEvent<DHZGhostMoverComponent, MapInitEvent>(OnGhostMapInit);
        SubscribeLocalEvent<DHZGhostMoverComponent, DHGhostLevelUpActionEvent>(OnGhostUp);
        SubscribeLocalEvent<DHZGhostMoverComponent, DHGhostLevelDownActionEvent>(OnGhostDown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now >= _nextStairs)
        {
            _nextStairs = now + StairsInterval;
            UpdateStairs(now);
        }

        if (now >= _nextFall)
        {
            _nextFall = now + FallInterval;
            UpdateFalling();
        }

        if (now >= _nextView)
        {
            _nextView = now + ViewInterval;
            UpdateViewers();
        }
    }

    #region Перемещение между уровнями

    /// <summary>Переносит сущность (и того, кого она тащит) в точку на другом уровне.</summary>
    public void MoveTo(EntityUid uid, MapCoordinates destination)
    {
        EntityUid? pulled = TryComp<PullerComponent>(uid, out var puller) ? puller.Pulling : null;

        _transform.SetMapCoordinates(uid, destination);
        if (pulled != null && Exists(pulled))
            _transform.SetMapCoordinates(pulled.Value, destination);
    }

    /// <summary>Можно ли встать в точке: есть пол (на земле — всегда).</summary>
    public bool CanStand(MapCoordinates coords)
    {
        return _z.TryGetLevel(coords.MapId, out var level) && level.Level == 0 || _z.HasFloor(coords);
    }

    #endregion

    #region Лестничные пролёты

    /// <summary>
    /// Лестницы: перебираются не все пролёты (на карте их сотни), а живые на Z-уровнях, которые идут, — и пролёт
    /// под каждым ищется рядом с ним (оптимизация, решение автора 2026-11-06).
    /// </summary>
    private void UpdateStairs(TimeSpan now)
    {
        var moves = new List<(EntityUid Uid, MapCoordinates To, EntityUid Stairs)>();
        var query = EntityQueryEnumerator<MobStateComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var mob, out var mobState, out var physics, out var mobXform))
        {
            // Дёшево отсеять: стоит на месте, не на Z-уровне, без сознания, недавно шагнул.
            if (physics.LinearVelocity.LengthSquared() < 0.04f || !_z.TryGetLevel(mobXform.MapUid, out var level) ||
                _mobState.IsIncapacitated(mob, mobState) ||
                _stairsCooldown.TryGetValue(mob, out var until) && now < until)
            {
                continue;
            }

            var mobCoords = _transform.GetMapCoordinates(mobXform);
            Entity<DHZStairsComponent>? best = null;
            var bestDistance = float.MaxValue;
            foreach (var stairs in _lookup.GetEntitiesInRange<DHZStairsComponent>(mobCoords, StairsReach,
                         LookupFlags.Static | LookupFlags.Sundries | LookupFlags.Approximate))
            {
                var distance = Vector2.Distance(_transform.GetWorldPosition(stairs), mobCoords.Position);
                if (distance < bestDistance)
                {
                    best = stairs;
                    bestDistance = distance;
                }
            }

            if (best is not { } found)
                continue;

            var xform = Transform(found);
            var facing = _transform.GetWorldRotation(xform).ToWorldVec();

            // По ступеням идут вдоль пролёта; поперёк — просто стоишь на площадке.
            if (Vector2.Dot(physics.LinearVelocity, facing) < 0.2f)
                continue;

            var targetLevel = level.Level + (found.Comp.Up ? 1 : -1);
            if (_z.LevelMap(level.Stack, targetLevel) is not { } targetMap)
                continue;

            var destination = new MapCoordinates(_transform.GetWorldPosition(xform) + facing * found.Comp.Exit,
                Transform(targetMap).MapID);
            moves.Add((mob, destination, found.Owner));
        }

        foreach (var (uid, to, stairs) in moves)
        {
            _stairsCooldown[uid] = now + StairsCooldown;
            if (!CanStand(to))
            {
                _popup.PopupEntity(Loc.GetString("dh-zlevel-stairs-blocked"), uid, uid);
                continue;
            }

            _audio.PlayPvs(StairsSound, stairs);
            MoveTo(uid, to);
            _audio.PlayPvs(StairsSound, uid);
        }

        // Старые записи — прочь.
        if (_stairsCooldown.Count > 64)
        {
            foreach (var (uid, until) in new Dictionary<EntityUid, TimeSpan>(_stairsCooldown))
            {
                if (now >= until)
                    _stairsCooldown.Remove(uid);
            }
        }
    }

    #endregion

    #region Падение

    /// <summary>
    /// На этаже без пола под ногами (шагнул с края крыши, пол проломан) — падаешь до первого пола внизу.
    /// Вне гридов на картах этажей — пустота: всё, что прямо на карте, падает.
    /// </summary>
    private void UpdateFalling()
    {
        var falling = new List<(EntityUid Uid, int Level, int Stack)>();
        var levels = EntityQueryEnumerator<DHZLevelComponent, TransformComponent>();
        while (levels.MoveNext(out var mapUid, out var level, out var mapXform))
        {
            if (level.Level <= 0)
                continue;

            var children = mapXform.ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (ShouldFall(child))
                    falling.Add((child, level.Level, level.Stack));
            }
        }

        foreach (var (uid, level, stack) in falling)
        {
            Fall(uid, level, stack);
        }
    }

    private bool ShouldFall(EntityUid uid)
    {
        if (HasComp<MapGridComponent>(uid) || HasComp<DHZCameraComponent>(uid) || HasComp<ProjectileComponent>(uid) ||
            HasComp<ThrownItemComponent>(uid))
        {
            return false;
        }

        if (!HasComp<MobStateComponent>(uid) && !HasComp<ItemComponent>(uid))
            return false;

        return !Transform(uid).Anchored;
    }

    private void Fall(EntityUid uid, int fromLevel, int stack)
    {
        var position = _transform.GetWorldPosition(uid);
        for (var level = fromLevel - 1; level >= 0; level--)
        {
            if (_z.LevelMap(stack, level) is not { } map)
                continue;

            var point = new MapCoordinates(position, Transform(map).MapID);
            if (level != 0 && !_z.HasFloor(point))
                continue;

            _transform.SetMapCoordinates(uid, point);
            Landed(uid, fromLevel - level);
            return;
        }
    }

    private void Landed(EntityUid uid, int levels)
    {
        _audio.PlayPvs(FallSound, uid);
        if (!HasComp<MobStateComponent>(uid))
            return;

        var perPart = new DamageSpecifier(_proto.Index(Blunt), FallDamagePerLevel * levels / FallParts.Length);
        foreach (var part in FallParts)
        {
            _trauma.SetPendingHitPart(uid, part);
            _damageable.TryChangeDamage(uid, perPart, true);
        }

        if (levels >= 2)
            _trauma.BreakBone(uid, "LegLeft");
        if (levels >= 4)
            _trauma.BreakBone(uid, "LegRight");

        _stun.TryKnockdown(uid, TimeSpan.FromSeconds(Math.Min(2 + levels, 8)), true);
        _popup.PopupEntity(Loc.GetString("dh-zlevel-fell", ("levels", levels)), uid, uid, PopupType.LargeCaution);
    }

    #endregion

    #region Взгляд на другой уровень

    private void OnViewerMapInit(Entity<DHZViewerComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.LookUpActionEntity, ent.Comp.LookUpAction);
        _actions.AddAction(ent.Owner, ref ent.Comp.LookDownActionEntity, ent.Comp.LookDownAction);
    }

    private void OnViewerShutdown(Entity<DHZViewerComponent> ent, ref ComponentShutdown args)
    {
        StopViewing(ent, false);
    }

    private void OnViewerMobState(Entity<DHZViewerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            StopViewing(ent, false);
    }

    private void OnCameraShutdown(Entity<DHZCameraComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<DHZViewerComponent>(ent.Comp.Viewer, out var viewer) && viewer.Camera == ent.Owner)
            StopViewing((ent.Comp.Viewer, viewer), false);
    }

    private void OnLookUp(Entity<DHZViewerComponent> ent, ref DHLookUpActionEvent args)
    {
        args.Handled = true;
        Look(ent, 1);
    }

    private void OnLookDown(Entity<DHZViewerComponent> ent, ref DHLookDownActionEvent args)
    {
        args.Handled = true;
        Look(ent, -1);
    }

    /// <summary>Шаг взгляда на уровень выше (+1) или ниже (−1); обратно к своему уровню — перестаёшь смотреть.</summary>
    private void Look(Entity<DHZViewerComponent> ent, int step)
    {
        var mapCoords = _transform.GetMapCoordinates(ent);
        if (!_z.TryGetLevel(mapCoords.MapId, out var own))
        {
            _popup.PopupEntity(Loc.GetString("dh-zlevel-look-nothing"), ent, ent);
            return;
        }

        var current = ent.Comp.Camera != null ? ent.Comp.ViewLevel : own.Level;
        var next = current + step;
        if (next == own.Level)
        {
            StopViewing(ent, true);
            return;
        }

        if (next < DHZLevelComponent.MinLevel || next > DHZLevelComponent.MaxLevel ||
            _z.LevelMap(own.Stack, next) is not { } targetMap)
        {
            _popup.PopupEntity(Loc.GetString(step > 0 ? "dh-zlevel-look-nothing-up" : "dh-zlevel-look-nothing-down"),
                ent, ent);
            return;
        }

        var depth = Math.Abs(next - own.Level);
        var up = next > own.Level;
        if (!CanLook(ent, mapCoords, own, up, depth, out var reason))
        {
            _popup.PopupEntity(Loc.GetString(reason), ent, ent);
            return;
        }

        StartViewing(ent, targetMap, next, mapCoords.Position);
    }

    /// <summary>
    /// Через лестницу, люк, проём или лифт рядом — на соседний уровень. С края (рядом нет пола) — вниз, под открытым
    /// небом — вверх, на 1 уровень, а с прицелом в двух руках — на глубину прицела.
    /// </summary>
    private bool CanLook(EntityUid user, MapCoordinates at, DHZLevelComponent own, bool up, int depth, out string reason)
    {
        reason = "dh-zlevel-look-too-far";
        if (depth == 1 && NearPassage(at, up))
            return true;

        var open = up ? !_z.IsCovered(at) : NearEdge(at);
        if (!open)
        {
            reason = up ? "dh-zlevel-look-covered" : "dh-zlevel-look-no-edge";
            return false;
        }

        return depth <= _z.SightDepth(user);
    }

    private bool NearPassage(MapCoordinates at, bool up)
    {
        foreach (var uid in _lookup.GetEntitiesInRange(at, PassageLookRange, LookupFlags.Static | LookupFlags.Sundries))
        {
            if (TryComp<DHLadderComponent>(uid, out var ladder) && ladder.Down != up ||
                TryComp<DHZStairsComponent>(uid, out var stairs) && stairs.Up == up ||
                HasComp<DHLiftComponent>(uid))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Рядом (в клетке от бойца) нет пола — край крыши, пролом, провал.</summary>
    private bool NearEdge(MapCoordinates at)
    {
        if (!_z.TryGetLevel(at.MapId, out var level) || level.Level <= 0 && !_z.HasFloor(at))
            return false;

        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                if (!_z.HasFloor(new MapCoordinates(at.Position + new Vector2(x, y), at.MapId)))
                    return true;
            }
        }

        return false;
    }

    private void StartViewing(Entity<DHZViewerComponent> ent, EntityUid targetMap, int level, Vector2 position)
    {
        StopViewing(ent, false);

        var camera = Spawn(CameraProto, new MapCoordinates(position, Transform(targetMap).MapID));
        EnsureComp<DHZCameraComponent>(camera).Viewer = ent;

        ent.Comp.Camera = camera;
        ent.Comp.ViewLevel = level;
        ent.Comp.Anchor = position;
        Dirty(ent);

        _eye.SetTarget(ent, camera);
        if (TryComp<ActorComponent>(ent, out var actor))
            _view.AddViewSubscriber(camera, actor.PlayerSession);

        _popup.PopupEntity(Loc.GetString("dh-zlevel-look-start", ("level", LevelName(level))), ent, ent);
    }

    public void StopViewing(Entity<DHZViewerComponent> ent, bool notify)
    {
        if (ent.Comp.Camera is not { } camera)
            return;

        ent.Comp.Camera = null;
        if (!TerminatingOrDeleted(ent))
        {
            if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
                Dirty(ent);

            _eye.SetTarget(ent, null);
        }

        if (Exists(camera))
        {
            if (TryComp<ActorComponent>(ent, out var actor))
                _view.RemoveViewSubscriber(camera, actor.PlayerSession);

            if (!TerminatingOrDeleted(camera))
                QueueDel(camera);
        }

        if (notify)
            _popup.PopupEntity(Loc.GetString("dh-zlevel-look-stop"), ent, ent);
    }

    private void UpdateViewers()
    {
        var stop = new List<Entity<DHZViewerComponent>>();
        var query = EntityQueryEnumerator<DHZViewerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var viewer, out var xform))
        {
            if (viewer.Camera == null)
                continue;

            // Отошёл, ушёл на другой уровень, опустил прицел — взгляд сбрасывается.
            if (!_z.TryGetLevel(xform.MapUid, out var own) || own.Level == viewer.ViewLevel ||
                _z.LevelMap(own.Stack, viewer.ViewLevel) != Transform(viewer.Camera.Value).MapUid ||
                Vector2.Distance(_transform.GetWorldPosition(xform), viewer.Anchor) > LookLeaveRange ||
                Math.Abs(viewer.ViewLevel - own.Level) > Math.Max(1, _z.SightDepth(uid)))
            {
                stop.Add((uid, viewer));
            }
        }

        foreach (var ent in stop)
        {
            StopViewing(ent, true);
        }
    }

    public static string LevelName(int level)
    {
        return level switch
        {
            0 => Robust.Shared.Localization.Loc.GetString("dh-zlevel-name-ground"),
            > 0 => Robust.Shared.Localization.Loc.GetString("dh-zlevel-name-up", ("level", level)),
            _ => Robust.Shared.Localization.Loc.GetString("dh-zlevel-name-down", ("level", -level)),
        };
    }

    #endregion

    #region Призраки

    private void OnGhostMapInit(Entity<DHZGhostMoverComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.UpActionEntity, ent.Comp.UpAction);
        _actions.AddAction(ent.Owner, ref ent.Comp.DownActionEntity, ent.Comp.DownAction);
    }

    private void OnGhostUp(Entity<DHZGhostMoverComponent> ent, ref DHGhostLevelUpActionEvent args)
    {
        args.Handled = true;
        StepLevel(ent, 1);
    }

    private void OnGhostDown(Entity<DHZGhostMoverComponent> ent, ref DHGhostLevelDownActionEvent args)
    {
        args.Handled = true;
        StepLevel(ent, -1);
    }

    /// <summary>
    /// Сразу на ближайший существующий уровень выше/ниже в той же точке (призраки, админы, маппинг).
    /// </summary>
    public bool StepLevel(EntityUid uid, int step)
    {
        var at = _transform.GetMapCoordinates(uid);
        if (!_z.TryGetLevel(at.MapId, out var own))
        {
            _popup.PopupEntity(Loc.GetString("dh-zlevel-look-nothing"), uid, uid);
            return false;
        }

        for (var level = own.Level + step;
             level >= DHZLevelComponent.MinLevel && level <= DHZLevelComponent.MaxLevel;
             level += step)
        {
            if (_z.LevelMap(own.Stack, level) is not { } map)
                continue;

            _transform.SetMapCoordinates(uid, new MapCoordinates(at.Position, Transform(map).MapID));
            _popup.PopupEntity(Loc.GetString("dh-zlevel-moved", ("level", LevelName(level))), uid, uid);
            return true;
        }

        _popup.PopupEntity(Loc.GetString(step > 0 ? "dh-zlevel-look-nothing-up" : "dh-zlevel-look-nothing-down"),
            uid, uid);
        return false;
    }

    #endregion

    #region Крыши

    /// <summary>
    /// Флаги крыши под этажами: каждый тайл пола на уровне выше земли накрывает тайлы всех уровней ниже (до земли),
    /// там нет солнца и погоды. Ставится при загрузке уровней и командой маппинга.
    /// </summary>
    public int SyncRoofs(int stack)
    {
        var levels = _z.Levels(stack);
        var set = 0;
        foreach (var (upperMap, upperLevel) in levels)
        {
            if (upperLevel <= 0)
                continue;

            var grids = new List<Entity<MapGridComponent>>();
            var query = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
            while (query.MoveNext(out var gridUid, out var grid, out var xform))
            {
                if (xform.MapUid == upperMap)
                    grids.Add((gridUid, grid));
            }

            foreach (var grid in grids)
            {
                foreach (var tile in _map.GetAllTiles(grid.Owner, grid.Comp))
                {
                    var center = _map.GridTileToWorldPos(grid.Owner, grid.Comp, tile.GridIndices);
                    foreach (var (lowerMap, lowerLevel) in levels)
                    {
                        if (lowerLevel < 0 || lowerLevel >= upperLevel)
                            continue;

                        var point = new MapCoordinates(center, Transform(lowerMap).MapID);
                        if (!_map.TryFindGridAt(point, out var lowerGrid, out var lowerComp))
                            continue;

                        var roof = EnsureComp<RoofComponent>(lowerGrid);
                        _roof.SetRoof((lowerGrid, lowerComp, roof), _map.TileIndicesFor(lowerGrid, lowerComp, point), true);
                        set++;
                    }
                }
            }
        }

        return set;
    }

    #endregion

    #region Новые уровни

    /// <summary>
    /// Пустой уровень над/под картой <paramref name="anyLevelMap"/> (для маппинга: построить этаж и сохранить грид).
    /// Карта без стопки становится уровнем 0 новой стопки. Тяжесть, воздух и свет — как у земли; под землёй темно.
    /// </summary>
    public EntityUid? CreateEmptyLevel(EntityUid anyLevelMap, int level, out string? error)
    {
        error = null;
        if (level < DHZLevelComponent.MinLevel || level > DHZLevelComponent.MaxLevel || level == 0)
        {
            error = "range";
            return null;
        }

        if (!_z.TryGetLevel(anyLevelMap, out var here))
        {
            here = EnsureComp<DHZLevelComponent>(anyLevelMap);
            here.Stack = _z.NewStack();
            here.Level = 0;
            Dirty(anyLevelMap, here);
        }

        if (_z.LevelMap(here.Stack, level) != null)
        {
            error = "exists";
            return null;
        }

        var ground = _z.LevelMap(here.Stack, 0) ?? anyLevelMap;
        var map = _map.CreateMap(out _);
        _meta.SetEntityName(map, level > 0
            ? Loc.GetString("dh-zlevel-map-up", ("level", level))
            : Loc.GetString("dh-zlevel-map-down", ("level", -level)));

        var comp = EnsureComp<DHZLevelComponent>(map);
        comp.Stack = here.Stack;
        comp.Level = level;
        Dirty(map, comp);

        var gravity = EnsureComp<Content.Shared.Gravity.GravityComponent>(map);
        gravity.Enabled = true;
        gravity.Inherent = true;
        Dirty(map, gravity);

        if (TryComp<Content.Shared.Atmos.Components.MapAtmosphereComponent>(ground, out var air))
            _atmos.SetMapAtmosphere(map, air.Space, new Content.Shared.Atmos.GasMixture(air.Mixture));

        var light = EnsureComp<MapLightComponent>(map);
        light.AmbientLightColor = level < 0
            ? Color.FromHex("#0d0c0b")
            : TryComp<MapLightComponent>(ground, out var groundLight) ? groundLight.AmbientLightColor : Color.White;
        Dirty(map, light);

        if (level < 0)
            EnsureComp<Content.Shared._DarkestHour.HeavyGuns.DHUndergroundMapComponent>(map);

        return map;
    }

    #endregion

    #region Пролом крыши

    /// <summary>
    /// Удар с орбиты пришёлся в крышу: пол верхнего уровня в радиусе проломлен (всё незакреплённое на нём падает вниз,
    /// закреплённое рушится), флаги крыши под проломом снимаются на всех уровнях ниже.
    /// </summary>
    public void BreakRoof(MapCoordinates center, float radius)
    {
        var box = Box2.CenteredAround(center.Position, new Vector2(radius * 2f));
        var circle = new Circle(center.Position, radius);

        if (_z.TryGetLevel(center.MapId, out var level) && level.Level > 0)
        {
            // Сначала — всё, что стоит на проломе.
            var onRoof = new List<EntityUid>();
            foreach (var uid in _lookup.GetEntitiesIntersecting(center.MapId, box, LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries))
            {
                if (HasComp<MapGridComponent>(uid) || Vector2.Distance(_transform.GetWorldPosition(uid), center.Position) > radius)
                    continue;

                onRoof.Add(uid);
            }

            var grids = new List<Entity<MapGridComponent>>();
            _map.FindGridsIntersecting(center.MapId, box, ref grids);
            foreach (var grid in grids)
            {
                var tiles = new List<(Vector2i, Robust.Shared.Map.Tile)>();
                foreach (var tile in _map.GetTilesIntersecting(grid.Owner, grid.Comp, circle, ignoreEmpty: true))
                {
                    tiles.Add((tile.GridIndices, Robust.Shared.Map.Tile.Empty));
                }

                _map.SetTiles(grid.Owner, grid.Comp, tiles);
            }

            foreach (var uid in onRoof)
            {
                if (TerminatingOrDeleted(uid))
                    continue;

                if (Transform(uid).Anchored)
                    QueueDel(uid);
                else
                    Fall(uid, level.Level, level.Stack);
            }

            foreach (var (map, lvl) in _z.Levels(level.Stack))
            {
                if (lvl < level.Level)
                    ClearRoofFlags(new MapCoordinates(center.Position, Transform(map).MapID), radius);
            }

            return;
        }

        ClearRoofFlags(center, radius);
    }

    /// <summary>Снимает флаг крыши с тайлов грида в радиусе.</summary>
    public void ClearRoofFlags(MapCoordinates center, float radius)
    {
        var box = Box2.CenteredAround(center.Position, new Vector2(radius * 2f));
        var grids = new List<Entity<MapGridComponent>>();
        _map.FindGridsIntersecting(center.MapId, box, ref grids);
        foreach (var grid in grids)
        {
            if (!TryComp<RoofComponent>(grid, out var roof))
                continue;

            foreach (var tile in _map.GetTilesIntersecting(grid.Owner, grid.Comp, new Circle(center.Position, radius), ignoreEmpty: false))
            {
                _roof.SetRoof((grid.Owner, grid.Comp, roof), tile.GridIndices, false);
            }
        }
    }

    #endregion
}
