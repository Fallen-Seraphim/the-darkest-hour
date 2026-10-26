using System.Numerics;
using Content.Server._DarkestHour.Assault;
using Content.Server.Station.Systems;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.HeavyGuns;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Spawners;

namespace Content.Server._DarkestHour.HeavyGuns;

/// <summary>
/// Миномёт (разворачивается из свёрнутого, наводится по координатам или по метке ЛЦУ, мину опускают в ствол — выстрел)
/// и лазерный целеуказатель (метка с номером в прямой видимости). Мины летят как снаряды артиллерии: предупреждение,
/// перехват ПВО, подрыв, подпалины.
/// </summary>
public sealed partial class DHHeavyGunSystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private ServerStationSystem _station = default!;

    private static readonly SoundSpecifier MortarFireSound = new SoundPathSpecifier("/Audio/_DarkestHour/HeavyGuns/gun_mortar_fire.ogg");
    private static readonly SoundSpecifier MortarIncomingSound = new SoundPathSpecifier("/Audio/_DarkestHour/HeavyGuns/gun_mortar_travel.ogg");
    private static readonly SoundSpecifier LaserSound = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg");

    private const string LaserMarkProto = "DHLaserMark";
    private const string FlareProto = "DHMortarFlare";

    private void InitializeMortar()
    {
        SubscribeLocalEvent<DHLaserDesignatorComponent, AfterInteractEvent>(OnDesignatorAfterInteract);
        SubscribeLocalEvent<DHLaserDesignatorComponent, DHLaserDoAfterEvent>(OnLaserDoAfter);

        SubscribeLocalEvent<DHMortarFoldedComponent, UseInHandEvent>(OnFoldedUse);
        SubscribeLocalEvent<DHMortarFoldedComponent, DHMortarDeployDoAfterEvent>(OnDeployDoAfter);
        SubscribeLocalEvent<DHMortarComponent, GetVerbsEvent<AlternativeVerb>>(OnMortarVerbs);
        SubscribeLocalEvent<DHMortarComponent, DHMortarFoldDoAfterEvent>(OnFoldDoAfter);
        SubscribeLocalEvent<DHMortarComponent, InteractUsingEvent>(OnMortarInteractUsing);
        SubscribeLocalEvent<DHMortarComponent, DHMortarLoadDoAfterEvent>(OnMortarLoaded);
        SubscribeLocalEvent<DHMortarComponent, ExaminedEvent>(OnMortarExamined);

        Subs.BuiEvents<DHMortarComponent>(DHMortarUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnMortarUiOpened);
            subs.Event<DHMortarAimMessage>(OnMortarAim);
        });
    }

    #region Сторона

    /// <summary>Сторона бойца, а для приборов — сторона станции, к которой относится их грид.</summary>
    private DHAssaultFaction? FactionOf(EntityUid uid)
    {
        if (TryComp<DHAssaultFighterComponent>(uid, out var fighter))
            return fighter.Faction;

        if (_station.GetOwningStation(uid) is { } station &&
            TryComp<DHAssaultFactionStationComponent>(station, out var factionStation))
        {
            return factionStation.Faction;
        }

        return null;
    }

    private static bool SameSide(DHAssaultFaction? a, DHAssaultFaction? b)
    {
        return a == null || b == null || a == b;
    }

    #endregion

    #region ЛЦУ

    private void OnDesignatorAfterInteract(Entity<DHLaserDesignatorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.User;
        if (HasComp<DHUndergroundMapComponent>(Transform(user).MapUid))
        {
            _popup.PopupEntity(Loc.GetString("dh-laser-underground"), user, user);
            return;
        }

        var target = _transform.ToMapCoordinates(args.ClickLocation);
        if (!_examine.InRangeUnOccluded(user, target, ent.Comp.Range))
        {
            _popup.PopupEntity(Loc.GetString("dh-laser-no-line", ("range", (int) ent.Comp.Range)), user, user);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.LaseTime,
            new DHLaserDoAfterEvent(GetNetCoordinates(args.ClickLocation)), ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
        _popup.PopupEntity(Loc.GetString("dh-laser-lasing"), user, user);
    }

    private void OnLaserDoAfter(Entity<DHLaserDesignatorComponent> ent, ref DHLaserDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var coordinates = GetCoordinates(args.Target);
        if (!coordinates.IsValid(EntityManager))
            return;

        if (ent.Comp.Mark is { } old && Exists(old))
            QueueDel(old);

        var map = _transform.ToMapCoordinates(coordinates);
        var mark = Spawn(LaserMarkProto, map);
        var markComp = EnsureComp<DHLaserMarkComponent>(mark);
        markComp.Code = NewLaserCode();
        markComp.Faction = FactionOf(args.User);
        markComp.Designator = ent;
        EnsureComp<TimedDespawnComponent>(mark).Lifetime = (float) ent.Comp.MarkLifetime.TotalSeconds;
        ent.Comp.Mark = mark;

        _audio.PlayPvs(LaserSound, ent);
        var x = (int) MathF.Round(map.Position.X);
        var y = (int) MathF.Round(map.Position.Y);
        _popup.PopupEntity(Loc.GetString("dh-laser-marked", ("code", markComp.Code), ("x", x), ("y", y)), args.User,
            args.User, PopupType.Medium);

        // Стороне — по радио: миномётчики и артиллеристы узнают номер и координаты.
        if (markComp.Faction is { } faction)
        {
            _assault.AnnounceToFaction(faction, Loc.GetString("dh-laser-sender", ("name", Name(args.User))),
                Loc.GetString("dh-laser-announce", ("code", markComp.Code), ("x", x), ("y", y)));
        }
    }

    private int NewLaserCode()
    {
        var used = new HashSet<int>();
        var query = EntityQueryEnumerator<DHLaserMarkComponent>();
        while (query.MoveNext(out _, out var mark))
        {
            used.Add(mark.Code);
        }

        for (var i = 0; i < 50; i++)
        {
            var code = _random.Next(1000, 10000);
            if (!used.Contains(code))
                return code;
        }

        return _random.Next(1000, 10000);
    }

    /// <summary>Метки своей стороны на карте <paramref name="map"/>; расстояние — от <paramref name="from"/>.</summary>
    private void CollectMarks(EntityUid map, DHAssaultFaction? side, Vector2 from, string? body, float maxDistance,
        List<DHLaserMarkEntry> into)
    {
        var query = EntityQueryEnumerator<DHLaserMarkComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mark, out var xform))
        {
            if (xform.MapUid != map || !SameSide(side, mark.Faction))
                continue;

            var position = _transform.GetWorldPosition(xform);
            var distance = Vector2.Distance(position, from);
            if (distance > maxDistance)
                continue;

            into.Add(new DHLaserMarkEntry(mark.Code, position, body, distance));
        }

        into.Sort((a, b) => a.Distance.CompareTo(b.Distance));
    }

    /// <summary>Метки ЛЦУ для консоли артиллерии: на поверхностях тел, до которых она достаёт.</summary>
    private List<DHLaserMarkEntry> ArtilleryMarks(Entity<DHHeavyGunConsoleComponent> console)
    {
        var marks = new List<DHLaserMarkEntry>();
        if (console.Comp.Role != DHGunRole.Artillery)
            return marks;

        var side = FactionOf(console);
        foreach (var (system, body, _) in ArtilleryBodies(console))
        {
            if (_landing.GetSurface(system.ID, body.Id) is not { } surface)
                continue;

            CollectMarks(surface, side, Vector2.Zero, body.Id, float.MaxValue, marks);
        }

        return marks;
    }

    #endregion

    #region Миномёт: развернуть и свернуть

    private void OnFoldedUse(Entity<DHMortarFoldedComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (Transform(args.User).GridUid == null)
        {
            _popup.PopupEntity(Loc.GetString("dh-mortar-no-ground"), args.User, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.DeployTime,
            new DHMortarDeployDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnDeployDoAfter(Entity<DHMortarFoldedComponent> ent, ref DHMortarDeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var userXform = Transform(args.User);
        if (userXform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var tile = _map.TileIndicesFor(grid, gridComp, userXform.Coordinates);
        var mortar = Spawn(ent.Comp.Mortar, _map.GridTileToLocal(grid, gridComp, tile));
        _transform.AnchorEntity(mortar, Transform(mortar));
        _popup.PopupEntity(Loc.GetString("dh-mortar-deployed", ("user", args.User)), mortar, PopupType.Small);
        QueueDel(ent);
    }

    private void OnMortarVerbs(Entity<DHMortarComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dh-mortar-fold-verb"),
            Act = () => _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.FoldTime,
                new DHMortarFoldDoAfterEvent(), ent, target: ent)
            {
                BreakOnMove = true,
                NeedHand = true,
            }),
        });
    }

    private void OnFoldDoAfter(Entity<DHMortarComponent> ent, ref DHMortarFoldDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var folded = Spawn(ent.Comp.Folded, _transform.GetMapCoordinates(ent));
        _hands.PickupOrDrop(args.User, folded);
        QueueDel(ent);
    }

    #endregion

    #region Миномёт: наведение и выстрел

    private bool MortarUnderground(EntityUid mortar)
    {
        return HasComp<DHUndergroundMapComponent>(Transform(mortar).MapUid);
    }

    private void OnMortarUiOpened(Entity<DHMortarComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateMortarUi(ent);
    }

    private void OnMortarAim(Entity<DHMortarComponent> ent, ref DHMortarAimMessage args)
    {
        if (!float.IsFinite(args.Target.X) || !float.IsFinite(args.Target.Y))
            return;

        var distance = Vector2.Distance(_transform.GetWorldPosition(ent), args.Target);
        if (distance < ent.Comp.MinRange || distance > ent.Comp.MaxRange)
        {
            _popup.PopupEntity(Loc.GetString("dh-mortar-out-of-range", ("min", (int) ent.Comp.MinRange),
                ("max", (int) ent.Comp.MaxRange)), ent, args.Actor);
            return;
        }

        ent.Comp.Target = args.Target;
        _popup.PopupEntity(Loc.GetString("dh-mortar-aimed", ("x", (int) MathF.Round(args.Target.X)),
            ("y", (int) MathF.Round(args.Target.Y))), ent, args.Actor);
        UpdateMortarUi(ent);
    }

    private void UpdateMortarUi(Entity<DHMortarComponent> ent)
    {
        var xform = Transform(ent);
        var position = _transform.GetWorldPosition(xform);
        var marks = new List<DHLaserMarkEntry>();
        if (xform.MapUid is { } map)
            CollectMarks(map, FactionOf(ent), position, null, ent.Comp.MaxRange, marks);

        string status;
        if (MortarUnderground(ent))
            status = Loc.GetString("dh-mortar-underground");
        else if (ent.Comp.Target == null)
            status = Loc.GetString("dh-mortar-status-not-aimed");
        else if (_timing.CurTime < ent.Comp.NextShot)
            status = Loc.GetString("dh-mortar-status-reloading");
        else
            status = Loc.GetString("dh-mortar-status-ready");

        _ui.SetUiState(ent.Owner, DHMortarUiKey.Key, new DHMortarState(position, ent.Comp.Target, ent.Comp.MinRange,
            ent.Comp.MaxRange, marks, status));
    }

    private void OnMortarInteractUsing(Entity<DHMortarComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<DHMortarShellComponent>(args.Used))
            return;

        args.Handled = true;
        if (!CanMortarFire(ent, args.User))
            return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.LoadTime,
            new DHMortarLoadDoAfterEvent(), ent, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private bool CanMortarFire(Entity<DHMortarComponent> ent, EntityUid user)
    {
        string? problem = null;
        if (MortarUnderground(ent))
            problem = "dh-mortar-underground";
        else if (ent.Comp.Target == null)
            problem = "dh-mortar-status-not-aimed";
        else if (_timing.CurTime < ent.Comp.NextShot)
            problem = "dh-mortar-status-reloading";

        if (problem == null)
            return true;

        _popup.PopupEntity(Loc.GetString(problem), ent, user);
        return false;
    }

    private void OnMortarLoaded(Entity<DHMortarComponent> ent, ref DHMortarLoadDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } shell ||
            !TryComp<DHMortarShellComponent>(shell, out var shellComp) || !CanMortarFire(ent, args.User) ||
            ent.Comp.Target is not { } target)
        {
            return;
        }

        args.Handled = true;
        var xform = Transform(ent);
        var origin = _transform.GetWorldPosition(xform);
        var distance = Vector2.Distance(origin, target);
        var spread = ent.Comp.Spread * Math.Clamp(distance / ent.Comp.MaxRange, 0.2f, 1f);
        var impact = target + _random.NextAngle().ToVec() * _random.NextFloat(0f, spread);

        _strikes.Add(new Strike
        {
            Role = DHGunRole.Artillery,
            Mortar = true,
            Faction = FactionOf(args.User),
            Warhead = shellComp.Warhead,
            Caliber = ent.Comp.Caliber,
            Target = new MapCoordinates(impact, xform.MapID),
            ImpactAt = _timing.CurTime + ent.Comp.FlightTime,
        });

        QueueDel(shell);
        ent.Comp.NextShot = _timing.CurTime + ent.Comp.ReloadTime;
        _audio.PlayPvs(MortarFireSound, ent, AudioParams.Default.WithMaxDistance(40f));
        foreach (var actor in _lookup.GetEntitiesInRange<Robust.Shared.Player.ActorComponent>(
                     _transform.GetMapCoordinates(ent), 4f))
        {
            _recoil.KickCamera(actor, _random.NextAngle().ToVec() * 0.3f);
        }

        UpdateMortarUi(ent);
    }

    private void OnMortarExamined(Entity<DHMortarComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(ent.Comp.Target is { } target
            ? Loc.GetString("dh-mortar-examine-aimed", ("x", (int) MathF.Round(target.X)), ("y", (int) MathF.Round(target.Y)))
            : Loc.GetString("dh-mortar-examine-not-aimed"));
    }

    /// <summary>Осветительная мина: над точкой падения повисает яркий огонь.</summary>
    private void Flare(MapCoordinates target)
    {
        Spawn(FlareProto, target);
    }

    #endregion
}
