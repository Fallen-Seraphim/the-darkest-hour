using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.RoundEnd;
using Content.Shared._DarkestHour.Republic;
using Content.Shared.Audio;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Explosion;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Paper;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._DarkestHour.Republic;

/// <summary>
/// Тактическая ядерная боеголовка Республиканского Армейского Корпуса.
/// </summary>
[RegisterComponent]
public sealed partial class DHTacticalWarheadComponent : Component
{
    [DataField]
    public bool PanelOpen;

    /// <summary>Кто приложил ключ авторизации (интерфейс открыт только им); сбрасывается при закрытии панели.</summary>
    [ViewVariables]
    public HashSet<EntityUid> Authorized = new();

    [ViewVariables]
    public DHWarheadStatus Status = DHWarheadStatus.AwaitCode;

    [ViewVariables]
    public string EnteredCode = string.Empty;

    /// <summary>Выставленный таймер, с.</summary>
    [DataField]
    public int Timer = 300;

    [DataField]
    public int MinTimer = 180;

    [DataField]
    public int MaxTimer = 600;

    [DataField]
    public int TimerStep = 30;

    [ViewVariables]
    public float Remaining;

    [DataField]
    public float PanelTime = 3f;

    [DataField]
    public float DisarmTime = 30f;

    /// <summary>За сколько секунд до подрыва звучит сирена.</summary>
    [DataField]
    public float AlarmTime = 10f;

    [DataField]
    public ProtoId<ExplosionPrototype> ExplosionType = "Default";

    [DataField]
    public float TotalIntensity = 5000000;

    [DataField]
    public float IntensitySlope = 5;

    [DataField]
    public float MaxIntensity = 100;

    [DataField]
    public SoundSpecifier PanelSound = new SoundPathSpecifier("/Audio/Machines/door_lock_off.ogg");

    [DataField]
    public SoundSpecifier SwipeSound = new SoundPathSpecifier("/Audio/Machines/id_swipe.ogg");

    [DataField]
    public SoundSpecifier KeypadSound = new SoundPathSpecifier("/Audio/Machines/Nuke/general_beep.ogg");

    [DataField]
    public SoundSpecifier AcceptedSound = new SoundPathSpecifier("/Audio/Machines/Nuke/confirm_beep.ogg");

    [DataField]
    public SoundSpecifier DeniedSound = new SoundPathSpecifier("/Audio/Machines/Nuke/angry_beep.ogg");

    [DataField]
    public SoundSpecifier ArmSound = new SoundPathSpecifier("/Audio/Misc/notice1.ogg");

    [DataField]
    public SoundSpecifier DisarmSound = new SoundPathSpecifier("/Audio/Misc/notice2.ogg");

    [DataField]
    public SoundSpecifier AlarmSound = new SoundPathSpecifier("/Audio/Machines/Nuke/nuke_alarm.ogg");

    [DataField]
    public SoundSpecifier ArmMusic = new SoundCollectionSpecifier("NukeMusic");

    [ViewVariables]
    public ResolvedSoundSpecifier? SelectedMusic;

    [ViewVariables]
    public float MusicLength;

    [ViewVariables]
    public bool PlayedMusic;

    [ViewVariables]
    public bool PlayedAlarm;

    [ViewVariables]
    public bool Exploded;
}

/// <summary>
/// Тактическая ядерная боеголовка отряда зачистки (код «Деаденд»). Порядок: ключом панели открыть панель ввода →
/// приложить ключ авторизации (пропуск на шее) → в интерфейсе ввести код ядерной аутентификации, выставить таймер,
/// взвести. Боеголовка закрепляется на палубе и ведёт отсчёт; разминировать — тем же путём (панель + ключ
/// авторизации) и 30 с работы. Подрыв — взрыв уровня ядерной бомбы и конец раунда.
/// Код один на раунд; его получает командир усиленного отряда на листе <see cref="DHWarheadCodePaperComponent"/>.
/// </summary>
public sealed partial class DHTacticalWarheadSystem : EntitySystem
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ExplosionSystem _explosions = default!;
    [Dependency] private ServerGameTicker _gameTicker = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private PointLightSystem _pointLight = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private GlobalSoundSystem _sound = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public const int CodeLength = 6;

    private string? _roundCode;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _roundCode = null);

        SubscribeLocalEvent<DHWarheadCodePaperComponent, MapInitEvent>(OnCodePaperInit,
            after: new[] { typeof(PaperSystem) });

        SubscribeLocalEvent<DHTacticalWarheadComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DHTacticalWarheadComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<DHTacticalWarheadComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DHTacticalWarheadComponent, DHWarheadPanelDoAfterEvent>(OnPanelDoAfter);
        SubscribeLocalEvent<DHTacticalWarheadComponent, DHWarheadDisarmDoAfterEvent>(OnDisarmDoAfter);

        SubscribeLocalEvent<DHTacticalWarheadComponent, DHWarheadKeypadMessage>(OnKeypad);
        SubscribeLocalEvent<DHTacticalWarheadComponent, DHWarheadClearMessage>(OnClear);
        SubscribeLocalEvent<DHTacticalWarheadComponent, DHWarheadEnterMessage>(OnEnter);
        SubscribeLocalEvent<DHTacticalWarheadComponent, DHWarheadAdjustTimerMessage>(OnAdjustTimer);
        SubscribeLocalEvent<DHTacticalWarheadComponent, DHWarheadArmMessage>(OnArm);
        SubscribeLocalEvent<DHTacticalWarheadComponent, DHWarheadDisarmMessage>(OnDisarm);
    }

    /// <summary>Код ядерной аутентификации этого раунда.</summary>
    public string RoundCode
    {
        get
        {
            if (_roundCode != null)
                return _roundCode;

            var code = string.Empty;
            for (var i = 0; i < CodeLength; i++)
            {
                code += _random.Next(0, 10).ToString();
            }

            _roundCode = code;
            return code;
        }
    }

    private void OnCodePaperInit(Entity<DHWarheadCodePaperComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<PaperComponent>(ent, out var paper))
            return;

        _paper.SetContent((ent.Owner, paper), Loc.GetString("dh-warhead-code-paper-content", ("code", RoundCode)));
    }

    #region Ключи и панель

    private void OnInteractUsing(Entity<DHTacticalWarheadComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<DHWarheadPanelKeyComponent>(args.Used))
        {
            args.Handled = true;
            StartPanelDoAfter(ent, args.User, args.Used);
            return;
        }

        if (HasComp<DHWarheadAuthKeyComponent>(args.Used))
        {
            args.Handled = true;
            Swipe(ent, args.User);
        }
    }

    /// <summary>Пустой рукой: если ключ авторизации на шее — приложить его.</summary>
    private void OnActivate(Entity<DHTacticalWarheadComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (_inventory.TryGetSlotEntity(args.User, "neck", out var neck) && HasComp<DHWarheadAuthKeyComponent>(neck))
        {
            args.Handled = true;
            Swipe(ent, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString(ent.Comp.PanelOpen ? "dh-warhead-need-auth" : "dh-warhead-panel-closed"),
            ent, args.User);
    }

    private void StartPanelDoAfter(Entity<DHTacticalWarheadComponent> ent, EntityUid user, EntityUid key)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.PanelTime, new DHWarheadPanelDoAfterEvent(), ent,
            target: ent, used: key)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnPanelDoAfter(Entity<DHTacticalWarheadComponent> ent, ref DHWarheadPanelDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        ent.Comp.PanelOpen = !ent.Comp.PanelOpen;
        _audio.PlayPvs(ent.Comp.PanelSound, ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.PanelOpen ? "dh-warhead-panel-opened" : "dh-warhead-panel-closed-now"),
            ent, args.User);

        if (!ent.Comp.PanelOpen)
        {
            // Панель закрыта — авторизация сброшена, набранный код стёрт
            ent.Comp.Authorized.Clear();
            ent.Comp.EnteredCode = string.Empty;
            _ui.CloseUi(ent.Owner, DHWarheadUiKey.Key);
        }

        UpdateAppearance(ent);
    }

    private void Swipe(Entity<DHTacticalWarheadComponent> ent, EntityUid user)
    {
        if (!ent.Comp.PanelOpen)
        {
            _popup.PopupEntity(Loc.GetString("dh-warhead-panel-closed"), ent, user);
            return;
        }

        _audio.PlayPvs(ent.Comp.SwipeSound, ent);
        ent.Comp.Authorized.Add(user);
        _popup.PopupEntity(Loc.GetString("dh-warhead-authorized"), ent, user);
        _ui.OpenUi(ent.Owner, DHWarheadUiKey.Key, user);
        UpdateAppearance(ent);
        UpdateUi(ent);
    }

    #endregion

    #region Интерфейс

    private bool CanUse(Entity<DHTacticalWarheadComponent> ent, EntityUid actor)
    {
        return ent.Comp.PanelOpen && ent.Comp.Authorized.Contains(actor) && !ent.Comp.Exploded;
    }

    private void OnKeypad(Entity<DHTacticalWarheadComponent> ent, ref DHWarheadKeypadMessage args)
    {
        if (!CanUse(ent, args.Actor) || ent.Comp.Status != DHWarheadStatus.AwaitCode)
            return;

        if (args.Digit is < 0 or > 9 || ent.Comp.EnteredCode.Length >= CodeLength)
            return;

        ent.Comp.EnteredCode += args.Digit.ToString();
        _audio.PlayPvs(ent.Comp.KeypadSound, ent, AudioParams.Default.WithPitchScale(1f + args.Digit * 0.06f));
        UpdateUi(ent);
    }

    private void OnClear(Entity<DHTacticalWarheadComponent> ent, ref DHWarheadClearMessage args)
    {
        if (!CanUse(ent, args.Actor) || ent.Comp.Status != DHWarheadStatus.AwaitCode)
            return;

        ent.Comp.EnteredCode = string.Empty;
        _audio.PlayPvs(ent.Comp.KeypadSound, ent);
        UpdateUi(ent);
    }

    private void OnEnter(Entity<DHTacticalWarheadComponent> ent, ref DHWarheadEnterMessage args)
    {
        if (!CanUse(ent, args.Actor) || ent.Comp.Status != DHWarheadStatus.AwaitCode)
            return;

        if (ent.Comp.EnteredCode == RoundCode)
        {
            ent.Comp.Status = DHWarheadStatus.Ready;
            _audio.PlayPvs(ent.Comp.AcceptedSound, ent);
        }
        else
        {
            _audio.PlayPvs(ent.Comp.DeniedSound, ent);
        }

        ent.Comp.EnteredCode = string.Empty;
        UpdateUi(ent);
    }

    private void OnAdjustTimer(Entity<DHTacticalWarheadComponent> ent, ref DHWarheadAdjustTimerMessage args)
    {
        if (!CanUse(ent, args.Actor) || ent.Comp.Status == DHWarheadStatus.Armed)
            return;

        var step = Math.Sign(args.Delta) * ent.Comp.TimerStep;
        ent.Comp.Timer = Math.Clamp(ent.Comp.Timer + step, ent.Comp.MinTimer, ent.Comp.MaxTimer);
        _audio.PlayPvs(ent.Comp.KeypadSound, ent);
        UpdateUi(ent);
    }

    private void OnArm(Entity<DHTacticalWarheadComponent> ent, ref DHWarheadArmMessage args)
    {
        if (!CanUse(ent, args.Actor) || ent.Comp.Status != DHWarheadStatus.Ready)
            return;

        var xform = Transform(ent);
        if (xform.GridUid == null)
        {
            _popup.PopupEntity(Loc.GetString("dh-warhead-need-deck"), ent, args.Actor, PopupType.MediumCaution);
            return;
        }

        if (!xform.Anchored)
            _transform.AnchorEntity(ent.Owner, xform);

        if (!xform.Anchored)
        {
            _popup.PopupEntity(Loc.GetString("dh-warhead-need-deck"), ent, args.Actor, PopupType.MediumCaution);
            return;
        }

        ent.Comp.Status = DHWarheadStatus.Armed;
        ent.Comp.Remaining = ent.Comp.Timer;
        ent.Comp.PlayedAlarm = false;
        ent.Comp.PlayedMusic = false;
        ent.Comp.SelectedMusic = _audio.ResolveSound(ent.Comp.ArmMusic);
        ent.Comp.MusicLength = (float) _audio.GetAudioLength(ent.Comp.SelectedMusic).TotalSeconds;

        var location = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString((ent.Owner, xform)));
        _chat.DispatchStationAnnouncement(ent,
            Loc.GetString("dh-warhead-announcement-armed", ("time", ent.Comp.Timer), ("location", location)),
            Loc.GetString("dh-warhead-announcement-sender"), false, null, Color.FromHex("#7B2FBE"));
        _sound.PlayGlobalOnStation(ent, _audio.ResolveSound(ent.Comp.ArmSound));

        _pointLight.SetEnabled(ent, true);
        _navMap.SetBeaconEnabled(ent.Owner, true);
        UpdateAppearance(ent);
        UpdateUi(ent);
    }

    private void OnDisarm(Entity<DHTacticalWarheadComponent> ent, ref DHWarheadDisarmMessage args)
    {
        if (!CanUse(ent, args.Actor) || ent.Comp.Status != DHWarheadStatus.Armed)
            return;

        var doAfter = new DoAfterArgs(EntityManager, args.Actor, ent.Comp.DisarmTime, new DHWarheadDisarmDoAfterEvent(),
            ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("dh-warhead-disarming"), ent, args.Actor, PopupType.LargeCaution);
    }

    private void OnDisarmDoAfter(Entity<DHTacticalWarheadComponent> ent, ref DHWarheadDisarmDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Status != DHWarheadStatus.Armed || ent.Comp.Exploded)
            return;

        args.Handled = true;
        ent.Comp.Status = DHWarheadStatus.AwaitCode;
        ent.Comp.PlayedAlarm = false;
        ent.Comp.PlayedMusic = false;
        ent.Comp.Remaining = 0;

        _chat.DispatchStationAnnouncement(ent, Loc.GetString("dh-warhead-announcement-disarmed"),
            Loc.GetString("dh-warhead-announcement-sender"), false, null, Color.FromHex("#7B2FBE"));
        _sound.PlayGlobalOnStation(ent, _audio.ResolveSound(ent.Comp.DisarmSound));
        _sound.StopStationEventMusic(ent, StationEventMusicType.Nuke);

        _pointLight.SetEnabled(ent, false);
        _navMap.SetBeaconEnabled(ent.Owner, false);
        UpdateAppearance(ent);
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<DHTacticalWarheadComponent> ent)
    {
        var c = ent.Comp;
        var state = new DHWarheadState(c.Status, c.EnteredCode.Length, CodeLength, c.Timer, c.MinTimer, c.MaxTimer,
            c.Remaining, Transform(ent).Anchored);
        _ui.SetUiState(ent.Owner, DHWarheadUiKey.Key, state);
    }

    #endregion

    private void OnExamined(Entity<DHTacticalWarheadComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Status == DHWarheadStatus.Armed)
            args.PushMarkup(Loc.GetString("dh-warhead-examine-armed", ("time", (int) ent.Comp.Remaining)));

        args.PushMarkup(Loc.GetString(ent.Comp.PanelOpen ? "dh-warhead-examine-panel-open" : "dh-warhead-examine-panel-closed"));
    }

    private void UpdateAppearance(Entity<DHTacticalWarheadComponent> ent)
    {
        var state = ent.Comp.Status == DHWarheadStatus.Armed
            ? ent.Comp.PlayedAlarm ? DHWarheadVisualState.Final : DHWarheadVisualState.Armed
            : ent.Comp.Authorized.Count > 0 ? DHWarheadVisualState.Authorized : DHWarheadVisualState.Idle;

        _appearance.SetData(ent, DHWarheadVisuals.Panel, ent.Comp.PanelOpen);
        _appearance.SetData(ent, DHWarheadVisuals.State, state);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DHTacticalWarheadComponent>();
        while (query.MoveNext(out var uid, out var warhead))
        {
            if (warhead.Status != DHWarheadStatus.Armed || warhead.Exploded)
                continue;

            var ent = (uid, warhead);
            var before = (int) warhead.Remaining;
            warhead.Remaining -= frameTime;

            if (!warhead.PlayedMusic && warhead.SelectedMusic != null &&
                warhead.Remaining <= warhead.MusicLength + warhead.AlarmTime + 1.5f)
            {
                warhead.PlayedMusic = true;
                _sound.DispatchStationEventMusic(uid, warhead.SelectedMusic, StationEventMusicType.Nuke);
            }

            if (!warhead.PlayedAlarm && warhead.Remaining <= warhead.AlarmTime)
            {
                warhead.PlayedAlarm = true;
                _sound.StopStationEventMusic(uid, StationEventMusicType.Nuke);
                _sound.PlayGlobalOnStation(uid, _audio.ResolveSound(warhead.AlarmSound), AudioParams.Default.WithVolume(-5f));
                UpdateAppearance(ent);
            }

            if (warhead.Remaining <= 0)
            {
                Detonate(ent);
                continue;
            }

            if ((int) warhead.Remaining != before)
                UpdateUi(ent);
        }
    }

    /// <summary>Подрыв: ядерный взрыв и конец раунда.</summary>
    public void Detonate(Entity<DHTacticalWarheadComponent> ent)
    {
        if (ent.Comp.Exploded)
            return;

        ent.Comp.Exploded = true;
        _explosions.QueueExplosion(ent.Owner, ent.Comp.ExplosionType, ent.Comp.TotalIntensity, ent.Comp.IntensitySlope,
            ent.Comp.MaxIntensity);

        _sound.StopStationEventMusic(ent, StationEventMusicType.Nuke);

        // Ванильное событие ядерного взрыва не поднимаем: правило Рейда засчитало бы его как победу Империи.
        if (_gameTicker.RunLevel == GameRunLevel.InRound)
            _roundEnd.EndRound(TimeSpan.FromSeconds(30));

        QueueDel(ent);
    }
}
