using System.Linq;
using System.Numerics;
using Content.Shared._DarkestHour.HeavyGuns;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.HeavyGuns;

/// <summary>
/// Пехотные пушки: наводка двумя углами, заряжание расчётом из 1–2 человек, прямая наводка (снаряд летит снарядом)
/// и навесной огонь (падает в расчётной точке тем же путём, что мины и снаряды артиллерии).
/// </summary>
public sealed partial class DHHeavyGunSystem
{
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private SharedGunSystem _guns = default!;

    private static readonly SoundSpecifier FieldGunFireSound = new SoundPathSpecifier("/Audio/_Mono/Weapons/Guns/Gunshots/cannon.ogg");
    private static readonly SoundSpecifier FieldGunLoadSound = new SoundPathSpecifier("/Audio/Weapons/Guns/MagIn/batrifle_magin.ogg");

    /// <summary>Углы таблицы стрельб в окне наводчика.</summary>
    private static readonly List<int> TableAngles = new() { 0, 5, 10, 15, 20, 30, 45, 60 };

    private void InitializeFieldGun()
    {
        SubscribeLocalEvent<DHFieldGunComponent, InteractUsingEvent>(OnFieldGunInteractUsing);
        SubscribeLocalEvent<DHFieldGunComponent, DHFieldGunLoadDoAfterEvent>(OnFieldGunLoaded);
        SubscribeLocalEvent<DHFieldGunComponent, ExaminedEvent>(OnFieldGunExamined);
        SubscribeLocalEvent<DHFieldShellProjectileComponent, ProjectileHitEvent>(OnFieldShellHit);

        Subs.BuiEvents<DHFieldGunComponent>(DHFieldGunUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnFieldGunUiOpened);
            subs.Event<DHFieldGunAimMessage>(OnFieldGunAim);
            subs.Event<DHFieldGunFireMessage>(OnFieldGunFire);
        });
    }

    #region Заряжание

    private void OnFieldGunInteractUsing(Entity<DHFieldGunComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<DHFieldGunShellComponent>(args.Used, out var shell))
            return;

        args.Handled = true;
        if (shell.Caliber != ent.Comp.Caliber)
        {
            _popup.PopupEntity(Loc.GetString("dh-field-gun-wrong-caliber", ("caliber", ent.Comp.Caliber)), ent, args.User);
            return;
        }

        if (ent.Comp.Loaded != null)
        {
            _popup.PopupEntity(Loc.GetString("dh-field-gun-already-loaded"), ent, args.User);
            return;
        }

        if (_timing.CurTime < ent.Comp.NextLoad)
        {
            _popup.PopupEntity(Loc.GetString("dh-field-gun-recoiling"), ent, args.User);
            return;
        }

        // Расчёт: за прицелом кто-то другой — заряжающий не отвлекается, быстрее.
        var loader = args.User;
        var crew = _ui.GetActors(ent.Owner, DHFieldGunUiKey.Key).Any(a => a != loader);
        var delay = crew ? ent.Comp.LoadTime : ent.Comp.SoloLoadTime;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, delay, new DHFieldGunLoadDoAfterEvent(), ent,
            target: ent, used: args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnFieldGunLoaded(Entity<DHFieldGunComponent> ent, ref DHFieldGunLoadDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } shell || ent.Comp.Loaded != null ||
            MetaData(shell).EntityPrototype is not { } proto)
        {
            return;
        }

        args.Handled = true;
        ent.Comp.Loaded = proto.ID;
        QueueDel(shell);
        _audio.PlayPvs(FieldGunLoadSound, ent);
        UpdateFieldGunUi(ent);
    }

    private void OnFieldGunExamined(Entity<DHFieldGunComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(ent.Comp.Loaded is { } loaded && _protoManager.TryIndex(loaded, out var proto)
            ? Loc.GetString("dh-field-gun-examine-loaded", ("shell", proto.Name))
            : Loc.GetString("dh-field-gun-examine-empty"));
        args.PushMarkup(Loc.GetString("dh-field-gun-examine-aim", ("azimuth", ent.Comp.Azimuth.ToString("0.#")),
            ("elevation", ent.Comp.Elevation.ToString("0.#"))));
    }

    #endregion

    #region Наводка

    private void OnFieldGunUiOpened(Entity<DHFieldGunComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateFieldGunUi(ent);
    }

    private void OnFieldGunAim(Entity<DHFieldGunComponent> ent, ref DHFieldGunAimMessage args)
    {
        if (!float.IsFinite(args.Azimuth) || !float.IsFinite(args.Elevation))
            return;

        ent.Comp.Azimuth = Math.Clamp(args.Azimuth, -ent.Comp.Traverse, ent.Comp.Traverse);
        ent.Comp.Elevation = Math.Clamp(args.Elevation, ent.Comp.MinElevation, ent.Comp.MaxElevation);
        UpdateFieldGunUi(ent);
    }

    /// <summary>Направление ствола в мире: направление лафета плюс горизонтальный угол (вправо — по часовой).</summary>
    private Angle FieldGunDirection(Entity<DHFieldGunComponent> ent)
    {
        return _transform.GetWorldRotation(ent) - Angle.FromDegrees(ent.Comp.Azimuth);
    }

    private DHFieldGunShellComponent? LoadedShellComp(DHFieldGunComponent gun)
    {
        if (gun.Loaded is not { } loaded || !_protoManager.TryIndex(loaded, out var proto) ||
            !proto.TryGetComponent<DHFieldGunShellComponent>(out var shell, EntityManager.ComponentFactory))
        {
            return null;
        }

        return shell;
    }

    private bool FieldGunReady(Entity<DHFieldGunComponent> ent, out string status)
    {
        var xform = Transform(ent);
        if (!xform.Anchored)
            status = Loc.GetString("dh-field-gun-status-not-deployed");
        else if (HasComp<DHUndergroundMapComponent>(xform.MapUid))
            status = Loc.GetString("dh-field-gun-status-underground");
        else if (LoadedShellComp(ent.Comp) is not { } shell)
            status = Loc.GetString("dh-field-gun-status-empty");
        else if (ent.Comp.Elevation > shell.MaxElevation)
            status = Loc.GetString("dh-field-gun-status-too-high", ("max", (int) shell.MaxElevation));
        else
        {
            status = Loc.GetString("dh-field-gun-status-ready");
            return true;
        }

        return false;
    }

    private void UpdateFieldGunUi(Entity<DHFieldGunComponent> ent)
    {
        var ready = FieldGunReady(ent, out var status);
        var shell = LoadedShellComp(ent.Comp);
        string? loadedName = ent.Comp.Loaded is { } loaded && _protoManager.TryIndex(loaded, out var loadedProto)
            ? loadedProto.Name
            : null;
        int? predicted = shell == null
            ? null
            : (int) MathF.Round(DHFieldGunBallistics.Range(shell.MaxRange, ent.Comp.Elevation));

        // Таблица стрельб: все снаряды этого калибра.
        var table = new List<DHFieldGunTableRow>();
        foreach (var proto in _protoManager.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || !proto.TryGetComponent<DHFieldGunShellComponent>(out var row, EntityManager.ComponentFactory) ||
                row.Caliber != ent.Comp.Caliber)
            {
                continue;
            }

            var ranges = TableAngles
                .Select(a => a <= row.MaxElevation ? (int) MathF.Round(DHFieldGunBallistics.Range(row.MaxRange, a)) : -1)
                .ToList();
            table.Add(new DHFieldGunTableRow(proto.Name, row.MaxElevation, ranges));
        }

        table.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));

        // Метки ЛЦУ: дистанция и горизонтальный угол до метки относительно лафета.
        var marks = new List<DHFieldGunMarkEntry>();
        var xform = Transform(ent);
        if (xform.MapUid is { } map)
        {
            var position = _transform.GetWorldPosition(xform);
            var facing = _transform.GetWorldRotation(xform);
            var found = new List<DHLaserMarkEntry>();
            CollectMarks(map, FactionOf(ent), position, null, 200f, found);
            foreach (var mark in found)
            {
                var toMark = mark.Position - position;
                var bearing = Angle.FromWorldVec(toMark);
                var azimuth = -(bearing - facing).Degrees;
                azimuth = ((azimuth + 180) % 360 + 360) % 360 - 180;
                marks.Add(new DHFieldGunMarkEntry(mark.Code, mark.Distance, (float) azimuth));
            }
        }

        _ui.SetUiState(ent.Owner, DHFieldGunUiKey.Key, new DHFieldGunState(ent.Comp.Caliber, ent.Comp.Azimuth,
            ent.Comp.Elevation, ent.Comp.Traverse, ent.Comp.MinElevation, ent.Comp.MaxElevation,
            ent.Comp.DirectFireMaxElevation, loadedName, shell?.MaxRange, shell?.MaxElevation ?? ent.Comp.MaxElevation,
            predicted, xform.Anchored, ready, status, TableAngles, table,
            marks));
    }

    #endregion

    #region Выстрел

    private void OnFieldGunFire(Entity<DHFieldGunComponent> ent, ref DHFieldGunFireMessage args)
    {
        if (!FieldGunReady(ent, out var status) || LoadedShellComp(ent.Comp) is not { } shell)
        {
            _popup.PopupEntity(status, ent, args.Actor);
            return;
        }

        ent.Comp.Loaded = null;
        ent.Comp.NextLoad = _timing.CurTime + ent.Comp.ReloadTime;

        var xform = Transform(ent);
        var origin = _transform.GetWorldPosition(xform);
        var direction = FieldGunDirection(ent) +
                        Angle.FromDegrees(_random.NextFloat(-ent.Comp.AzimuthSpread, ent.Comp.AzimuthSpread));
        var range = DHFieldGunBallistics.Range(shell.MaxRange, ent.Comp.Elevation) *
                    (1f + _random.NextFloat(-ent.Comp.RangeSpread, ent.Comp.RangeSpread));
        var dirVec = direction.ToWorldVec();

        _audio.PlayPvs(FieldGunFireSound, ent, AudioParams.Default.WithMaxDistance(50f).WithVolume(2f));
        foreach (var actor in _lookup.GetEntitiesInRange<ActorComponent>(_transform.GetMapCoordinates(ent), 6f))
        {
            _recoil.KickCamera(actor, -dirVec * 0.5f);
        }

        if (ent.Comp.Elevation <= ent.Comp.DirectFireMaxElevation)
        {
            // Прямая наводка: снаряд летит снарядом и бьёт в первое препятствие.
            var projectile = Spawn(shell.Projectile, new MapCoordinates(origin + dirVec * 0.6f, xform.MapID));
            var flight = EnsureComp<DHFieldShellProjectileComponent>(projectile);
            flight.Warhead = shell.Warhead;
            flight.Caliber = ent.Comp.Caliber;
            flight.Origin = origin;
            flight.MaxDistance = range;

            if (TryComp<ProjectileComponent>(projectile, out var projComp) && ent.Comp.Caliber != 75)
                projComp.Damage *= ent.Comp.Caliber / 75f;

            _guns.ShootProjectile(projectile, dirVec, Vector2.Zero, ent, args.Actor, shell.DirectSpeed);
        }
        else
        {
            // Навесной огонь: падает в расчётной точке, по пути не задевает ничего.
            var impact = origin + dirVec * range;
            _strikes.Add(new Strike
            {
                Role = DHGunRole.Artillery,
                Mortar = true,
                Faction = FactionOf(args.Actor),
                Warhead = shell.Warhead,
                Caliber = ent.Comp.Caliber,
                Target = new MapCoordinates(impact, xform.MapID),
                ImpactAt = _timing.CurTime +
                           TimeSpan.FromSeconds(DHFieldGunBallistics.FlightTime(range, ent.Comp.Elevation)),
            });
        }

        UpdateFieldGunUi(ent);
    }

    private void OnFieldShellHit(Entity<DHFieldShellProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        // Подкалиберный прошивает цели сам (пробитие снаряда), остальные рвутся на первой.
        if (ent.Comp.Warhead == DHWarhead.ArmorPiercing)
            return;

        FieldShellBurst(ent);
    }

    private void FieldShellBurst(Entity<DHFieldShellProjectileComponent> ent)
    {
        if (ent.Comp.Done)
            return;

        ent.Comp.Done = true;
        var at = _transform.GetMapCoordinates(ent);
        if (ent.Comp.Warhead == DHWarhead.ArmorPiercing)
            ScorchAt(at, 0.5f);
        else
            Detonate(ent.Comp.Warhead, ent.Comp.Caliber, at, 0.8f, false);

        QueueDel(ent);
    }

    /// <summary>Снаряды прямой наводки, долетевшие до дальности падения, — рвутся на земле.</summary>
    private void UpdateFieldShells()
    {
        var query = EntityQueryEnumerator<DHFieldShellProjectileComponent, TransformComponent>();
        var landed = new List<Entity<DHFieldShellProjectileComponent>>();
        while (query.MoveNext(out var uid, out var shell, out var xform))
        {
            if (!shell.Done && Vector2.Distance(_transform.GetWorldPosition(xform), shell.Origin) >= shell.MaxDistance)
                landed.Add((uid, shell));
        }

        foreach (var shell in landed)
        {
            FieldShellBurst(shell);
        }
    }

    #endregion
}
