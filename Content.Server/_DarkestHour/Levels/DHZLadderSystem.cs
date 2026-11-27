using Content.Shared._DarkestHour.Levels;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server._DarkestHour.Levels;

/// <summary>
/// Универсальная лестница (решение автора 2026-11-27): вместо лестничных маршей, проёмов, отдельных «лестниц наверх»
/// и «люков вниз» — одна лестница в одной точке на каждом уровне. Куда она ведёт, видно по соседям: лестница в той же
/// точке уровнем выше — можно подняться, уровнем ниже — спуститься. Спрайт (RMC-14) — по соединениям: низ, сквозная,
/// верх с проёмом.
/// </summary>
public sealed partial class DHZLadderSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DHZLevelSystem _z = default!;
    [Dependency] private DHZTransitSystem _zTransit = default!;
    [Dependency] private Robust.Shared.Timing.IGameTiming _timing = default!;

    private static readonly SoundSpecifier ClimbSound = new SoundCollectionSpecifier("FootstepCatwalk");

    /// <summary>Насколько точно лестница на соседнем уровне должна стоять над/под этой.</summary>
    private const float MatchRange = 0.6f;

    /// <summary>
    /// Лестницы на кораблях (авианосец, челнок) переезжают между картами вместе с гридом — их спрайты изредка
    /// пересчитываются по соседям.
    /// </summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(10);

    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHZLadderComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DHZLadderComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<DHZLadderComponent, InteractHandEvent>(OnInteract);
        SubscribeLocalEvent<DHZLadderComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeLocalEvent<DHZLadderComponent, DHZLadderDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<DHZLadderComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextRefresh)
            return;

        _nextRefresh = now + RefreshInterval;
        var query = EntityQueryEnumerator<DHZLadderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            // Только на подвижных гридах (у корабля есть физика); лестницы городов и бункеров не меняются.
            if (xform.GridUid is { } grid && grid != xform.MapUid && HasComp<Content.Shared.Shuttles.Components.ShuttleComponent>(grid) ||
                xform.GridUid is { } deck && HasComp<Content.Server._DarkestHour.Carrier.DHCarrierDeckComponent>(deck))
            {
                UpdateVisuals(uid);
            }
        }
    }

    #region Соединения

    /// <summary>Лестница в той же точке на уровень выше (+1) или ниже (−1).</summary>
    public EntityUid? Neighbour(EntityUid ladder, int step, EntityUid? ignore = null)
    {
        var here = _transform.GetMapCoordinates(ladder);
        if (!_z.TryGetLevel(here.MapId, out var level) || _z.OnLevel(here, level.Level + step) is not { } there)
            return null;

        foreach (var other in _lookup.GetEntitiesInRange<DHZLadderComponent>(there, MatchRange,
                     LookupFlags.Static | LookupFlags.Sundries | LookupFlags.Approximate))
        {
            if (other.Owner != ladder && other.Owner != ignore)
                return other.Owner;
        }

        return null;
    }

    private void UpdateVisuals(EntityUid ladder, EntityUid? ignore = null)
    {
        var up = Neighbour(ladder, 1, ignore) != null;
        var down = Neighbour(ladder, -1, ignore) != null;
        var state = up && down ? DHZLadderConnections.Both
            : up ? DHZLadderConnections.Up
            : down ? DHZLadderConnections.Down
            : DHZLadderConnections.None;
        _appearance.SetData(ladder, DHZLadderVisuals.Connections, state);
    }

    /// <summary>Уровни грузятся по очереди: появившаяся лестница обновляет и себя, и соседей.</summary>
    private void OnMapInit(Entity<DHZLadderComponent> ent, ref MapInitEvent args)
    {
        UpdateVisuals(ent);
        if (Neighbour(ent, 1) is { } above)
            UpdateVisuals(above);
        if (Neighbour(ent, -1) is { } below)
            UpdateVisuals(below);
    }

    private void OnShutdown(Entity<DHZLadderComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(Transform(ent).MapUid))
            return;

        // Сломали лестницу — соседи перестают к ней вести.
        if (Neighbour(ent, 1) is { } above)
            UpdateVisuals(above, ent);
        if (Neighbour(ent, -1) is { } below)
            UpdateVisuals(below, ent);
    }

    /// <summary>Куда встаёт поднявшийся/спустившийся: на лестницу соседнего уровня, если там можно стоять.</summary>
    private MapCoordinates? Destination(EntityUid ladder, int step)
    {
        if (Neighbour(ladder, step) is not { } other)
            return null;

        var to = _transform.GetMapCoordinates(other);
        return _zTransit.CanStand(to) ? to : null;
    }

    #endregion

    #region Подъём и спуск

    private void OnInteract(Entity<DHZLadderComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        // Клик — наверх, если есть куда; иначе вниз. Обе стороны — в альтернативных действиях.
        var step = Neighbour(ent, 1) != null ? 1 : Neighbour(ent, -1) != null ? -1 : 0;
        if (step == 0)
        {
            _popup.PopupEntity(Loc.GetString("dh-ladder-blocked"), ent, args.User);
            return;
        }

        StartClimb(ent, args.User, step);
    }

    public void StartClimb(Entity<DHZLadderComponent> ent, EntityUid user, int step)
    {
        if (Destination(ent, step) == null)
        {
            _popup.PopupEntity(Loc.GetString("dh-ladder-blocked"), ent, user);
            return;
        }

        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.Delay,
            new DHZLadderDoAfterEvent(step), ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
        });

        if (started)
        {
            _popup.PopupEntity(Loc.GetString(step < 0 ? "dh-ladder-start-down" : "dh-ladder-start-up",
                ("user", user)), ent, PopupType.Small);
        }
    }

    private void OnDoAfter(Entity<DHZLadderComponent> ent, ref DHZLadderDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || Destination(ent, args.Step) is not { } destination)
            return;

        args.Handled = true;
        var user = args.User;

        if (TryComp<DHZViewerComponent>(user, out var viewer))
            _zTransit.StopViewing((user, viewer), false);

        // Раненого, которого тащишь, берёшь с собой.
        _audio.PlayPvs(ClimbSound, ent);
        _zTransit.MoveTo(user, destination);
        _audio.PlayPvs(ClimbSound, user);
        _popup.PopupEntity(Loc.GetString(args.Step < 0 ? "dh-ladder-arrived-down" : "dh-ladder-arrived-up",
            ("user", user)), user, PopupType.Small);
    }

    private void OnVerbs(Entity<DHZLadderComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var viewer = CompOrNull<DHZViewerComponent>(user);
        foreach (var step in new[] { 1, -1 })
        {
            if (Neighbour(ent, step) == null)
                continue;

            var s = step;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(s > 0 ? "dh-zladder-verb-up" : "dh-zladder-verb-down"),
                Icon = new SpriteSpecifier.Texture(new ResPath(s > 0
                    ? "/Textures/Interface/VerbIcons/eject.svg.192dpi.png"
                    : "/Textures/Interface/VerbIcons/insert.svg.192dpi.png")),
                Priority = s > 0 ? 3 : 2,
                Act = () => StartClimb(ent, user, s),
            });

            if (viewer == null)
                continue;

            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(s > 0 ? "dh-zladder-verb-look-up" : "dh-zladder-verb-look-down"),
                Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_DarkestHour/Interface/Actions/zlevels.rsi"),
                    s > 0 ? "look_up" : "look_down"),
                Priority = 1,
                Act = () => _zTransit.Look((user, viewer), s),
            });
        }
    }

    private void OnExamined(Entity<DHZLadderComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        foreach (var step in new[] { 1, -1 })
        {
            if (Neighbour(ent, step) is not { } other || !_z.TryGetLevel(Transform(other).MapUid, out var level))
                continue;

            args.PushMarkup(Loc.GetString(step > 0 ? "dh-ladder-examine-up" : "dh-ladder-examine-down",
                ("where", FormattedMessage.EscapeText(DHZTransitSystem.LevelName(level.Level)))));
        }
    }

    #endregion
}
