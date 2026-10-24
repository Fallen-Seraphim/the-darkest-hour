using Content.Server._DarkestHour.Medical.Radiation;
using Content.Server.Lightning;
using Content.Server.Power.EntitySystems;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Events;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Timing;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Shuttles.Rift;

/// <summary>
/// Разрывные прыжки поверх ванильного FTL: звёздные системы (свои карты), проверка оборудования перед прыжком в
/// другую систему, долгая зарядка, остывание, расход хронита, износ, хронитовая вспышка и сбой при износе.
/// </summary>
public sealed partial class DHRiftSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DHDoseSystem _dose = default!;
    [Dependency] private LightningSystem _lightning = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ShuttleConsoleSystem _shuttleConsole = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<ToolQualityPrototype> WeldingQuality = "Welding";

    /// <summary>Снятие износа сваркой за один раз.</summary>
    private const float RepairAmount = 0.3f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<DHConsoleFTLTargetEvent>(OnFTLTarget);
        SubscribeLocalEvent<DHRiftJumpPendingComponent, FTLRequestEvent>(OnFTLRequest);
        SubscribeLocalEvent<DHRiftJumpPendingComponent, FTLStartedEvent>(OnFTLStarted);

        SubscribeLocalEvent<DHRiftGeneratorComponent, ExaminedEvent>(OnGeneratorExamined);
        SubscribeLocalEvent<DHRiftAccumulatorComponent, ExaminedEvent>(OnAccumulatorExamined);
        SubscribeLocalEvent<DHRiftAccumulatorComponent, InteractUsingEvent>(OnAccumulatorInteractUsing);
        SubscribeLocalEvent<DHRiftAccumulatorComponent, DHRiftRepairDoAfterEvent>(OnRepairDoAfter);

        InitializeSpace();
    }

    #region Star systems

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        foreach (var system in _proto.EnumeratePrototypes<DHStarSystemPrototype>())
        {
            // DarkestHour: домашняя система привязывается к карте станции (DHCelestialSystem).
            if (system.Home)
                continue;

            var mapUid = _map.CreateMap(out _);
            _meta.SetEntityName(mapUid, Loc.GetString(system.Name));
            EnsureComp<DHStarSystemMapComponent>(mapUid).System = system.ID;
            EnsureComp<FTLDestinationComponent>(mapUid);
        }

        _shuttleConsole.RefreshShuttleConsoles();
    }

    /// <summary>Звёздная система карты; null — домашняя система станции.</summary>
    public string? SystemOf(EntityUid? map)
    {
        if (map == null || !TryComp<DHStarSystemMapComponent>(map, out var comp) || comp.System == null)
            return null;

        // Домашняя система — та же, что у карт без компонента (планеты, экспедиции).
        return _proto.TryIndex(comp.System.Value, out var proto) && proto.Home ? null : comp.System.Value.Id;
    }

    #endregion

    #region Jump

    private void OnFTLTarget(ref DHConsoleFTLTargetEvent ev)
    {
        RemComp<DHRiftJumpPendingComponent>(ev.Shuttle);

        // С поверхности тела и из его атмосферы уходят только консолью посадки.
        if (HasComp<DHPlanetSurfaceMapComponent>(Transform(ev.Shuttle).MapUid) ||
            HasComp<DHPlanetAtmosphereMapComponent>(Transform(ev.Shuttle).MapUid))
        {
            _popup.PopupEntity(Loc.GetString("dh-landing-use-console"), ev.Console, PopupType.MediumCaution);
            ev.Cancelled = true;
            return;
        }

        var source = SystemOf(Transform(ev.Shuttle).MapUid);
        var target = SystemOf(_map.GetMapOrInvalid(ev.TargetMap));
        if (source == target)
            return;

        // Между системами прыгают только с навигационной консоли разрыва: там карта системы и точка выхода.
        if (!HasComp<DHRiftNavConsoleComponent>(ev.Console))
        {
            _popup.PopupEntity(Loc.GetString("dh-rift-use-nav-console"), ev.Console, PopupType.MediumCaution);
            ev.Cancelled = true;
            return;
        }

        if (!TryGetRiftGear(ev.Shuttle, out var generator, out var accumulator, out var reason))
        {
            _popup.PopupEntity(reason, ev.Console, PopupType.MediumCaution);
            ev.Cancelled = true;
            return;
        }

        var pending = EnsureComp<DHRiftJumpPendingComponent>(ev.Shuttle);
        pending.Generator = generator;
        pending.Accumulator = accumulator;

        _popup.PopupEntity(Loc.GetString("dh-rift-charging"), ev.Console, PopupType.Medium);
    }

    /// <summary>Генератор и подходящий накопитель на корабле; иначе — причина отказа.</summary>
    private List<Entity<MapGridComponent>> _exitGrids = new();

    /// <summary>
    /// Свободна ли точка выхода: в радиусе корабля (плюс запас) нет других сеток. В отличие от ванильного
    /// <c>FTLFree</c> не ограничивает дальность прыжка 256 м — консоли разрыва и посадки прыгают через всю
    /// систему и на поверхность.
    /// </summary>
    public bool ExitFree(EntityUid shuttle, EntityCoordinates coordinates)
    {
        var target = _transform.ToMapCoordinates(coordinates);
        if (target.MapId == MapId.Nullspace)
            return false;

        var radius = _shuttle.GetFTLBufferRange(shuttle) + ShuttleSystem.FTLBufferRange;
        var circle = new PhysShapeCircle(radius, target.Position);

        _exitGrids.Clear();
        _map.FindGridsIntersecting(target.MapId, circle, Robust.Shared.Physics.Transform.Empty, ref _exitGrids,
            includeMap: false);

        foreach (var grid in _exitGrids)
        {
            if (grid.Owner != shuttle)
                return false;
        }

        return true;
    }

    public bool TryGetRiftGear(EntityUid shuttle, out EntityUid generator, out EntityUid accumulator, out string reason)
    {
        generator = EntityUid.Invalid;
        accumulator = EntityUid.Invalid;

        DHRiftGeneratorComponent? genComp = null;
        var generators = EntityQueryEnumerator<DHRiftGeneratorComponent, TransformComponent>();
        while (generators.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.GridUid != shuttle || !xform.Anchored)
                continue;

            generator = uid;
            genComp = comp;
            if (_power.IsPowered(uid))
                break;
        }

        if (genComp == null)
        {
            reason = Loc.GetString("dh-rift-no-generator");
            return false;
        }

        if (!_power.IsPowered(generator))
        {
            reason = Loc.GetString("dh-rift-no-power");
            return false;
        }

        var now = _timing.CurTime;
        if (now < genComp.NextJump)
        {
            reason = Loc.GetString("dh-rift-cooldown", ("seconds", (int) (genComp.NextJump - now).TotalSeconds));
            return false;
        }

        var mass = TryComp<PhysicsComponent>(shuttle, out var physics) ? physics.Mass : 0f;

        DHRiftAccumulatorComponent? best = null;
        var accumulators = EntityQueryEnumerator<DHRiftAccumulatorComponent, TransformComponent>();
        while (accumulators.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.GridUid != shuttle || !xform.Anchored)
                continue;

            if (best == null || comp.MaxMass > best.MaxMass)
            {
                best = comp;
                accumulator = uid;
            }
        }

        if (best == null)
        {
            reason = Loc.GetString("dh-rift-no-accumulator");
            return false;
        }

        if (mass > best.MaxMass)
        {
            reason = Loc.GetString("dh-rift-too-heavy", ("mass", (int) mass), ("max", (int) best.MaxMass));
            return false;
        }

        if (best.Chronite < best.ChronitePerJump)
        {
            reason = Loc.GetString("dh-rift-no-chronite", ("chronite", (int) best.Chronite), ("need", (int) best.ChronitePerJump));
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>Разрыв заряжается дольше обычного FTL.</summary>
    private void OnFTLRequest(Entity<DHRiftJumpPendingComponent> ent, ref FTLRequestEvent args)
    {
        if (!TryComp<DHRiftGeneratorComponent>(ent.Comp.Generator, out var generator) ||
            !TryComp<FTLComponent>(ent, out var ftl))
        {
            return;
        }

        ftl.StartupTime = (float) generator.ChargeTime.TotalSeconds;
        ftl.StateTime = StartEndTime.FromStartDuration(_timing.CurTime, generator.ChargeTime);
        Dirty(ent, ftl);

        _popup.PopupEntity(Loc.GetString("dh-rift-charging-generator"), ent.Comp.Generator, PopupType.LargeCaution);
    }

    /// <summary>Корабль ушёл в разрыв: расход хронита, износ, вспышка, возможный сбой, остывание генератора.</summary>
    private void OnFTLStarted(Entity<DHRiftJumpPendingComponent> ent, ref FTLStartedEvent args)
    {
        var generatorUid = ent.Comp.Generator;
        var accumulatorUid = ent.Comp.Accumulator;
        RemComp<DHRiftJumpPendingComponent>(ent);

        if (!TryComp<DHRiftGeneratorComponent>(generatorUid, out var generator) ||
            !TryComp<DHRiftAccumulatorComponent>(accumulatorUid, out var accumulator))
        {
            return;
        }

        var mass = TryComp<PhysicsComponent>(ent, out var physics) ? physics.Mass : 0f;
        var massRatio = accumulator.MaxMass > 0f ? Math.Clamp(mass / accumulator.MaxMass, 0f, 1f) : 1f;
        var intensity = accumulator.FlashPower * (0.5f + massRatio) * (1f + accumulator.Wear);

        // Сбой: изношенный накопитель выбрасывает корабль мимо точки выхода, вспышка вдвое сильнее.
        var failChance = Math.Clamp((accumulator.Wear - 0.5f) * 1.5f, 0f, 0.75f);
        if (_random.Prob(failChance) && TryComp<FTLComponent>(ent, out var ftl))
        {
            var offset = _random.NextAngle().ToVec() * _random.NextFloat(150f, 400f);
            ftl.TargetCoordinates = ftl.TargetCoordinates.Offset(offset);
            Dirty(ent, ftl);
            intensity *= 2f;
            _popup.PopupEntity(Loc.GetString("dh-rift-misjump"), generatorUid, PopupType.LargeCaution);
        }

        accumulator.Chronite = MathF.Max(0f, accumulator.Chronite - accumulator.ChronitePerJump);
        accumulator.Wear = MathF.Min(1f, accumulator.Wear + accumulator.WearPerJump);
        Dirty(accumulatorUid, accumulator);

        generator.NextJump = _timing.CurTime + generator.Cooldown;

        Flash(ent, generatorUid, generator, intensity);
        EnterRiftSpace(ent);
    }

    /// <summary>
    /// Хронитовая вспышка: молнии вокруг генератора, радиация и хронит всем живым на корабле (урон Radiation
    /// учитывает броню и йодид калия и копится в дозе).
    /// </summary>
    private void Flash(EntityUid shuttle, EntityUid generator, DHRiftGeneratorComponent comp, float intensity)
    {
        var bolts = 2 + (int) MathF.Round(intensity * 3f);
        _lightning.ShootRandomLightnings(generator, comp.FlashRange, bolts);

        var radiation = new DamageSpecifier();
        radiation.DamageDict["Radiation"] = FixedPoint2.New(8f * intensity);
        var chronite = 2f * intensity;

        var query = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != shuttle)
                continue;

            _damageable.TryChangeDamage(uid, radiation);
            _dose.AddChronite(uid, chronite);
            _popup.PopupEntity(Loc.GetString("dh-rift-flash"), uid, uid, PopupType.MediumCaution);
        }
    }

    #endregion

    #region Accumulator

    private void OnAccumulatorInteractUsing(Entity<DHRiftAccumulatorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<DHChroniteCellComponent>(args.Used, out var cell))
        {
            args.Handled = true;
            if (ent.Comp.Chronite >= ent.Comp.ChroniteMax)
            {
                _popup.PopupEntity(Loc.GetString("dh-rift-accumulator-full"), ent, args.User);
                return;
            }

            ent.Comp.Chronite = MathF.Min(ent.Comp.ChroniteMax, ent.Comp.Chronite + cell.Amount);
            Dirty(ent);
            QueueDel(args.Used);
            _popup.PopupEntity(Loc.GetString("dh-rift-accumulator-loaded", ("chronite", (int) ent.Comp.Chronite),
                ("max", (int) ent.Comp.ChroniteMax)), ent, args.User);
            return;
        }

        if (ent.Comp.Wear > 0f && _tool.HasQuality(args.Used, WeldingQuality.Id))
        {
            args.Handled = _tool.UseTool(args.Used, args.User, ent, 8f, WeldingQuality, new DHRiftRepairDoAfterEvent(), fuel: 5f);
        }
    }

    private void OnRepairDoAfter(Entity<DHRiftAccumulatorComponent> ent, ref DHRiftRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Wear = MathF.Max(0f, ent.Comp.Wear - RepairAmount);
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("dh-rift-accumulator-repaired", ("wear", (int) (ent.Comp.Wear * 100f))), ent, args.User);
    }

    private void OnAccumulatorExamined(Entity<DHRiftAccumulatorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("dh-rift-accumulator-examine",
            ("chronite", (int) ent.Comp.Chronite), ("max", (int) ent.Comp.ChroniteMax),
            ("perJump", (int) ent.Comp.ChronitePerJump), ("wear", (int) (ent.Comp.Wear * 100f)),
            ("mass", (int) ent.Comp.MaxMass)));
    }

    private void OnGeneratorExamined(Entity<DHRiftGeneratorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var now = _timing.CurTime;
        if (!_power.IsPowered(ent.Owner))
            args.PushMarkup(Loc.GetString("dh-rift-generator-examine-unpowered"));
        else if (now < ent.Comp.NextJump)
            args.PushMarkup(Loc.GetString("dh-rift-generator-examine-cooldown", ("seconds", (int) (ent.Comp.NextJump - now).TotalSeconds)));
        else
            args.PushMarkup(Loc.GetString("dh-rift-generator-examine-ready"));
    }

    #endregion
}
