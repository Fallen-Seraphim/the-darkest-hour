using System.Numerics;
using Content.Server._DarkestHour.Assault;
using Content.Server._DarkestHour.Clothing.ModularArmor;
using Content.Shared._DarkestHour.Clothing.ModularArmor;
using Content.Shared._DarkestHour.Overwatch;
using Content.Shared._DarkestHour.Recon;
using Content.Shared._DarkestHour.ThirdParty;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.MedicalScanner;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Speech;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.ThirdParty;

/// <summary>
/// Способности ИИ брони M.A.R.S. 1.5 (план осады п. 28, PR 3). M.A.R.S. — бестелесный «голос в броне» на десантнике:
/// видит шире, размечает цели (десантник по метке стреляет точнее), сканирует округу (но писк выдаёт десантника),
/// смотрит тактическую сеть стороны цели, держит детектор движения, взламывает двери, глушит радио, колет
/// автоаптечку и, пока десантник без сознания, ненадолго уводит экзоскелет. Снята броня — ИИ отключён: ни способностей,
/// ни голоса.
/// </summary>
public sealed partial class DHMarsSystem : EntitySystem
{
    [Dependency] private Content.Shared.Actions.SharedActionsSystem _actions = default!;
    [Dependency] private DHModularArmorDamageSystem _armorDamage = default!;
    [Dependency] private DHModularArmorSystem _armor = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContentEyeSystem _contentEye = default!;
    [Dependency] private SharedDoorSystem _doors = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private Content.Shared.Medical.HealthAnalyzer.HealthAnalyzerSystem _healthAnalyzer = default!;

    private static readonly EntProtoId[] ActionProtos =
    {
        "DHActionMarsMark", "DHActionMarsScan", "DHActionMarsTacMap", "DHActionMarsMotion", "DHActionMarsHack",
        "DHActionMarsJam", "DHActionMarsInject", "DHActionMarsHealth", "DHActionMarsCarry",
    };

    private static readonly EntProtoId DetectorProto = "DHMarsMotionDetector";
    private const string ZgaChannel = "DHZGA";

    private const float WideZoom = 1.4f;
    private static readonly TimeSpan MarkTime = TimeSpan.FromSeconds(60);
    private const float ScanRadius = 12f;
    private static readonly TimeSpan ScanMarkTime = TimeSpan.FromSeconds(8);
    private const float ScanRevealRadius = 10f;
    private const float JamRange = 9f;
    private static readonly TimeSpan JamTime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HackTime = TimeSpan.FromSeconds(6);
    private const float HackRange = 2.5f;
    private const float CarryDistance = 8f;
    private const float CarrySpeed = 2.5f;
    private static readonly TimeSpan CarryTime = TimeSpan.FromSeconds(6);

    private static readonly SoundSpecifier ScanSound = new SoundPathSpecifier("/Audio/Machines/high_tech_confirm.ogg");
    private static readonly SoundSpecifier HackSound = new SoundPathSpecifier("/Audio/Machines/airlock_emergencyon.ogg");
    private static readonly SoundSpecifier JamSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHMarsComponent, DHMarsMarkActionEvent>(OnMark);
        SubscribeLocalEvent<DHMarsComponent, DHMarsScanActionEvent>(OnScan);
        SubscribeLocalEvent<DHMarsComponent, DHMarsTacMapActionEvent>(OnTacMap);
        SubscribeLocalEvent<DHMarsComponent, DHMarsMotionActionEvent>(OnMotion);
        SubscribeLocalEvent<DHMarsComponent, DHMarsHackActionEvent>(OnHack);
        SubscribeLocalEvent<DHMarsComponent, DHMarsJamActionEvent>(OnJam);
        SubscribeLocalEvent<DHMarsComponent, DHMarsInjectActionEvent>(OnInject);
        SubscribeLocalEvent<DHMarsComponent, DHMarsHealthActionEvent>(OnHealth);
        SubscribeLocalEvent<DHMarsComponent, DHMarsCarryActionEvent>(OnCarry);
        SubscribeLocalEvent<DHMarsComponent, SpeakAttemptEvent>(OnSpeakAttempt);
        SubscribeLocalEvent<RadioSendAttemptEvent>(OnRadioSend);
        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceive);
    }

    /// <summary>Подключить M.A.R.S. к десантнику: способности, широкий обзор, детектор, тактическая сеть стороны цели.</summary>
    public void Setup(Entity<DHMarsComponent> mars, EntityUid host, EntityUid target)
    {
        mars.Comp.Host = host;
        foreach (var proto in ActionProtos)
        {
            EntityUid? action = null;
            if (_actions.AddAction(mars, ref action, proto) && action != null)
                mars.Comp.Actions.Add(action.Value);
        }

        _contentEye.SetZoom(mars, SharedContentEyeSystem.DefaultZoom * WideZoom, ignoreLimits: true);
        mars.Comp.Detector = Spawn(DetectorProto, new EntityCoordinates(mars, Vector2.Zero));

        if (TryComp<DHOverwatchConsoleComponent>(mars, out var console) &&
            TryComp<DHAssaultFighterComponent>(target, out var fighter))
        {
            console.Faction = fighter.Faction;
        }
    }

    /// <summary>ИИ работает: десантник жив и на нём экзоскелет ЭМБ-5.</summary>
    private bool IsOnline(DHMarsComponent comp, out EntityUid host)
    {
        host = comp.Host ?? EntityUid.Invalid;
        return comp.Host is { } h && !TerminatingOrDeleted(h) && !_mobState.IsDead(h) &&
               _inventory.TryGetSlotEntity(h, "outerClothing", out var suit) && HasComp<DHInfantryShieldComponent>(suit);
    }

    private bool CanAct(Entity<DHMarsComponent> ent, out EntityUid host)
    {
        if (IsOnline(ent.Comp, out host))
            return true;

        _popup.PopupEntity(Loc.GetString("dh-mars-offline"), ent, ent, PopupType.SmallCaution);
        return false;
    }

    private void OnSpeakAttempt(Entity<DHMarsComponent> ent, ref SpeakAttemptEvent args)
    {
        if (!ent.Comp.Online)
            args.Cancel();
    }

    #region Метки и скан

    private void OnMark(Entity<DHMarsComponent> ent, ref DHMarsMarkActionEvent args)
    {
        if (args.Handled || !CanAct(ent, out var host) || args.Target == host)
            return;

        args.Handled = true;
        ClearMark(ent);

        var now = _timing.CurTime;
        var mark = EnsureComp<DHMarsMarkComponent>(args.Target);
        mark.Viewers = new List<NetEntity> { GetNetEntity(host), GetNetEntity(ent) };
        mark.Until = now + MarkTime;
        mark.Scan = false;
        mark.Mars = ent;
        Dirty(args.Target, mark);
        ent.Comp.Marked = args.Target;

        var aim = EnsureComp<DHMarsAimComponent>(host);
        aim.Until = mark.Until;
        Dirty(host, aim);
        RefreshGuns(host);

        _popup.PopupEntity(Loc.GetString("dh-mars-marked", ("target", Name(args.Target))), host, host);
    }

    private void ClearMark(Entity<DHMarsComponent> ent)
    {
        if (ent.Comp.Marked is { } old && TryComp<DHMarsMarkComponent>(old, out var oldMark) && oldMark.Mars == ent.Owner)
            RemComp<DHMarsMarkComponent>(old);

        ent.Comp.Marked = null;
        if (ent.Comp.Host is { } host && RemComp<DHMarsAimComponent>(host))
            RefreshGuns(host);
    }

    private void RefreshGuns(EntityUid host)
    {
        foreach (var held in _hands.EnumerateHeld(host))
        {
            if (HasComp<GunComponent>(held))
                _gun.RefreshModifiers(held);
        }
    }

    private void OnScan(Entity<DHMarsComponent> ent, ref DHMarsScanActionEvent args)
    {
        if (args.Handled || !CanAct(ent, out var host))
            return;

        args.Handled = true;
        var now = _timing.CurTime;
        var center = _transform.GetMapCoordinates(host);
        var viewers = new List<NetEntity> { GetNetEntity(host), GetNetEntity(ent) };
        var found = 0;
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(center, ScanRadius))
        {
            if (mob.Owner == host || _mobState.IsDead(mob) || HasComp<DHMarsMarkComponent>(mob))
                continue;

            var mark = AddComp<DHMarsMarkComponent>(mob);
            mark.Viewers = new List<NetEntity>(viewers);
            mark.Until = now + ScanMarkTime;
            mark.Scan = true;
            mark.Mars = ent;
            Dirty(mob, mark);
            found++;
        }

        // Писк скана слышно далеко — он выдаёт десантника.
        _audio.PlayPvs(ScanSound, host, AudioParams.Default.WithVolume(6f).WithMaxDistance(ScanRevealRadius * 2));
        _popup.PopupCoordinates(Loc.GetString("dh-mars-scan-heard"), Transform(host).Coordinates,
            Robust.Shared.Player.Filter.Pvs(host).RemoveWhereAttachedEntity(e => e == host || e == ent.Owner),
            true, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("dh-mars-scan-done", ("count", found)), host, host);
    }

    #endregion

    #region Тактическая сеть, детектор

    private void OnTacMap(Entity<DHMarsComponent> ent, ref DHMarsTacMapActionEvent args)
    {
        if (args.Handled || !CanAct(ent, out _))
            return;

        args.Handled = true;
        _ui.TryToggleUi(ent.Owner, DHOverwatchUiKey.Key, ent.Owner);
    }

    private void OnMotion(Entity<DHMarsComponent> ent, ref DHMarsMotionActionEvent args)
    {
        if (args.Handled || !CanAct(ent, out _) || !TryComp<DHMotionDetectorComponent>(ent.Comp.Detector, out var detector))
            return;

        args.Handled = true;
        detector.Active = !detector.Active;
        _popup.PopupEntity(Loc.GetString(detector.Active ? "dh-motion-detector-on" : "dh-motion-detector-off"), ent, ent);
    }

    #endregion

    #region Двери, радио, аптечка

    private void OnHack(Entity<DHMarsComponent> ent, ref DHMarsHackActionEvent args)
    {
        if (args.Handled || !CanAct(ent, out var host) || !HasComp<DoorComponent>(args.Target))
            return;

        if (!_transform.InRange(Transform(host).Coordinates, Transform(args.Target).Coordinates, HackRange))
        {
            _popup.PopupEntity(Loc.GetString("dh-mars-hack-too-far"), ent, ent);
            return;
        }

        args.Handled = true;
        ent.Comp.HackTarget = args.Target;
        ent.Comp.HackDone = _timing.CurTime + HackTime;
        _popup.PopupEntity(Loc.GetString("dh-mars-hack-start"), host, host);
    }

    private void FinishHack(Entity<DHMarsComponent> ent, EntityUid host)
    {
        var door = ent.Comp.HackTarget!.Value;
        ent.Comp.HackTarget = null;
        if (TerminatingOrDeleted(door) || !TryComp<DoorComponent>(door, out var doorComp) ||
            !_transform.InRange(Transform(host).Coordinates, Transform(door).Coordinates, HackRange + 0.5f))
        {
            _popup.PopupEntity(Loc.GetString("dh-mars-hack-failed"), host, host);
            return;
        }

        if (TryComp<DoorBoltComponent>(door, out var bolts) && bolts.BoltsDown)
            _doors.SetBoltsDown((door, bolts), false);

        _doors.TryToggleDoor(door, doorComp);
        _audio.PlayPvs(HackSound, door);
        _popup.PopupEntity(Loc.GetString("dh-mars-hack-done"), host, host);
    }

    private void OnJam(Entity<DHMarsComponent> ent, ref DHMarsJamActionEvent args)
    {
        if (args.Handled || !CanAct(ent, out var host))
            return;

        args.Handled = true;
        ent.Comp.JamUntil = _timing.CurTime + JamTime;
        _audio.PlayPvs(JamSound, host);
        _popup.PopupEntity(Loc.GetString("dh-mars-jam-on"), host, host);
    }

    private bool Jammed(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return false;

        var now = _timing.CurTime;
        var coords = _transform.GetMapCoordinates(uid);
        var query = EntityQueryEnumerator<DHMarsComponent>();
        while (query.MoveNext(out var comp))
        {
            if (comp.JamUntil <= now || !comp.Online || comp.Host is not { } host || TerminatingOrDeleted(host))
                continue;

            if (_transform.GetMapCoordinates(host).InRange(coords, JamRange))
                return true;
        }

        return false;
    }

    private void OnRadioSend(ref RadioSendAttemptEvent args)
    {
        if (!args.Cancelled && args.Channel.ID != ZgaChannel && Jammed(args.RadioSource))
            args.Cancelled = true;
    }

    private void OnRadioReceive(ref RadioReceiveAttemptEvent args)
    {
        if (!args.Cancelled && args.Channel.ID != ZgaChannel && Jammed(args.RadioReceiver))
            args.Cancelled = true;
    }

    private void OnInject(Entity<DHMarsComponent> ent, ref DHMarsInjectActionEvent args)
    {
        if (args.Handled || !CanAct(ent, out var host))
            return;

        args.Handled = true;
        if (!_inventory.TryGetSlotEntity(host, "outerClothing", out var suit) ||
            _armor.GetModule(suit.Value, DHArmorSlot.Special) is not { } module ||
            !TryComp<DHArmorSpecialComponent>(module, out var special) ||
            !_armorDamage.TryInjectAutoMed((module, special), host))
        {
            _popup.PopupEntity(Loc.GetString("dh-mars-inject-failed"), ent, ent);
        }
    }

    /// <summary>Проверка здоровья: окно анализатора здоровья на носителя, обновляется раз в секунду, пока открыто.</summary>
    private void OnHealth(Entity<DHMarsComponent> ent, ref DHMarsHealthActionEvent args)
    {
        if (args.Handled || !CanAct(ent, out var host))
            return;

        args.Handled = true;
        if (_ui.IsUiOpen(ent.Owner, HealthAnalyzerUiKey.Key, ent.Owner))
        {
            _ui.CloseUi(ent.Owner, HealthAnalyzerUiKey.Key, ent.Owner);
            return;
        }

        _ui.OpenUi(ent.Owner, HealthAnalyzerUiKey.Key, ent.Owner);
        UpdateHealth(ent.Owner, host);
    }

    private void UpdateHealth(EntityUid mars, EntityUid host)
    {
        _ui.SetUiState(mars, HealthAnalyzerUiKey.Key, _healthAnalyzer.GetHealthAnalyzerUiState(host, true));
    }

    #endregion

    #region Аварийный отход

    private void OnCarry(Entity<DHMarsComponent> ent, ref DHMarsCarryActionEvent args)
    {
        if (args.Handled || !CanAct(ent, out var host))
            return;

        if (!_mobState.IsCritical(host))
        {
            _popup.PopupEntity(Loc.GetString("dh-mars-carry-conscious"), ent, ent);
            return;
        }

        var from = _transform.GetWorldPosition(host);
        var to = _transform.ToMapCoordinates(args.Target).Position;
        var dir = to - from;
        if (dir.Length() > CarryDistance)
            to = from + Vector2.Normalize(dir) * CarryDistance;

        args.Handled = true;
        ent.Comp.CarryTo = to;
        ent.Comp.CarryUntil = _timing.CurTime + CarryTime;
    }

    /// <summary>Шаг аварийного отхода: экзоскелет тащит тело по прямой, пока не упрётся в стену.</summary>
    private void StepCarry(Entity<DHMarsComponent> ent, EntityUid host, float frameTime)
    {
        if (ent.Comp.CarryTo is not { } to)
            return;

        if (_timing.CurTime > ent.Comp.CarryUntil || !_mobState.IsCritical(host))
        {
            ent.Comp.CarryTo = null;
            return;
        }

        var from = _transform.GetWorldPosition(host);
        var dir = to - from;
        var dist = dir.Length();
        if (dist < 0.1f)
        {
            ent.Comp.CarryTo = null;
            return;
        }

        var next = from + dir / dist * MathF.Min(dist, CarrySpeed * frameTime);
        var mapCoords = new MapCoordinates(next, _transform.GetMapId(host));
        var coords = _transform.ToCoordinates(mapCoords);
        if (_turf.GetTileRef(coords) is not { } tile || _turf.IsTileBlocked(tile, CollisionGroup.Impassable))
        {
            ent.Comp.CarryTo = null;
            return;
        }

        _transform.SetWorldPosition(host, next);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var check = now >= _nextCheck;
        if (check)
            _nextCheck = now + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<DHMarsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);
            var online = IsOnline(comp, out var host);
            if (online && comp.CarryTo != null)
                StepCarry(ent, host, frameTime);

            if (!check)
                continue;

            if (online != comp.Online)
            {
                comp.Online = online;
                _popup.PopupEntity(Loc.GetString(online ? "dh-mars-online" : "dh-mars-offline"), uid, uid,
                    PopupType.MediumCaution);
                if (!online)
                {
                    comp.CarryTo = null;
                    comp.HackTarget = null;
                    comp.JamUntil = TimeSpan.Zero;
                    if (TryComp<DHMotionDetectorComponent>(comp.Detector, out var detector))
                        detector.Active = false;
                    _ui.CloseUi(uid, DHOverwatchUiKey.Key);
                    _ui.CloseUi(uid, HealthAnalyzerUiKey.Key);
                }
            }

            if (online && _ui.IsUiOpen(uid, HealthAnalyzerUiKey.Key))
                UpdateHealth(uid, host);

            if (comp.HackTarget != null && now >= comp.HackDone && online)
                FinishHack(ent, host);
        }

        if (!check)
            return;

        // Истёкшие метки и наведение.
        var marks = EntityQueryEnumerator<DHMarsMarkComponent>();
        while (marks.MoveNext(out var marked, out var mark))
        {
            if (now < mark.Until)
                continue;

            RemComp<DHMarsMarkComponent>(marked);
            if (mark.Mars is { } mars && TryComp<DHMarsComponent>(mars, out var marsComp) && marsComp.Marked == marked)
                marsComp.Marked = null;
        }

        var aims = EntityQueryEnumerator<DHMarsAimComponent>();
        while (aims.MoveNext(out var aimer, out var aim))
        {
            if (now < aim.Until)
                continue;

            RemComp<DHMarsAimComponent>(aimer);
            RefreshGuns(aimer);
        }
    }
}
