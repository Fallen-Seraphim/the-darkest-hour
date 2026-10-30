using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.ControlPoints;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Server.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.ControlPoints;

/// <summary>
/// Зоны захвата: захват удержанием у терминала, выдача ящиков удерживающей стороне, питание света и ПВО от зоны (АЭС).
/// </summary>
public sealed partial class DHControlPointSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private DHAssaultRuleSystem _assault = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly SoundSpecifier CaptureSound = new SoundPathSpecifier("/Audio/Misc/notice1.ogg");
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHControlPointComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DHControlPointComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<DHControlPointComponent, DHControlPointCaptureDoAfterEvent>(OnCaptured);
        SubscribeLocalEvent<DHControlPointComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DHControlPointPoweredComponent, MapInitEvent>(OnPoweredMapInit);
    }

    private void OnMapInit(Entity<DHControlPointComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextOutput = _timing.CurTime + ent.Comp.Interval;
        UpdateAppearance(ent);
    }

    private void OnPoweredMapInit(Entity<DHControlPointPoweredComponent> ent, ref MapInitEvent args)
    {
        // Зона могла появиться раньше или позже — сверяемся со всеми зонами с этим id.
        var points = EntityQueryEnumerator<DHControlPointComponent>();
        while (points.MoveNext(out var point))
        {
            if (point.PointId == ent.Comp.Point)
            {
                ApplyPower(ent, point.Holder);
                return;
            }
        }

        ApplyPower(ent, null);
    }

    #region Захват

    private void OnInteractHand(Entity<DHControlPointComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.User;
        if (!TryComp<DHAssaultFighterComponent>(user, out var fighter))
        {
            _popup.PopupEntity(Loc.GetString("dh-control-point-not-fighter"), ent, user);
            return;
        }

        if (ent.Comp.Holder == fighter.Faction)
        {
            _popup.PopupEntity(Loc.GetString("dh-control-point-already-ours"), ent, user);
            return;
        }

        if (Contested(ent, fighter.Faction))
        {
            _popup.PopupEntity(Loc.GetString("dh-control-point-contested"), ent, user, PopupType.MediumCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("dh-control-point-capturing", ("point", Loc.GetString(ent.Comp.PointName))),
            ent, user);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.CaptureTime,
            new DHControlPointCaptureDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 2f,
        });
    }

    /// <summary>В зоне живой боец другой стороны.</summary>
    private bool Contested(Entity<DHControlPointComponent> ent, DHAssaultFaction capturer)
    {
        var coordinates = Transform(ent).Coordinates;
        foreach (var other in _lookup.GetEntitiesInRange<DHAssaultFighterComponent>(coordinates, ent.Comp.Radius))
        {
            if (other.Comp.Faction != capturer && _mobState.IsAlive(other))
                return true;
        }

        return false;
    }

    private void OnCaptured(Entity<DHControlPointComponent> ent, ref DHControlPointCaptureDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (!TryComp<DHAssaultFighterComponent>(args.User, out var fighter) || ent.Comp.Holder == fighter.Faction)
            return;

        if (Contested(ent, fighter.Faction))
        {
            _popup.PopupEntity(Loc.GetString("dh-control-point-contested"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        var previous = ent.Comp.Holder;
        SetOwner(ent, fighter.Faction);

        var name = Loc.GetString(ent.Comp.PointName);
        var sender = Loc.GetString("dh-control-point-sender");
        _assault.AnnounceToFaction(fighter.Faction, sender,
            Loc.GetString("dh-control-point-captured-ours", ("point", name)), CaptureSound);

        var enemy = fighter.Faction == DHAssaultFaction.Republic ? DHAssaultFaction.Empire : DHAssaultFaction.Republic;
        _assault.AnnounceToFaction(enemy, sender,
            Loc.GetString(previous == enemy ? "dh-control-point-lost" : "dh-control-point-captured-enemy",
                ("point", name)), CaptureSound);
    }

    /// <summary>Сменить владельца зоны (захват, админ, правило).</summary>
    public void SetOwner(Entity<DHControlPointComponent> ent, DHAssaultFaction? owner)
    {
        ent.Comp.Holder = owner;
        ent.Comp.NextOutput = _timing.CurTime + ent.Comp.Interval;
        Dirty(ent);
        UpdateAppearance(ent);

        var powered = EntityQueryEnumerator<DHControlPointPoweredComponent>();
        while (powered.MoveNext(out var uid, out var comp))
        {
            if (comp.Point == ent.Comp.PointId)
                ApplyPower((uid, comp), owner);
        }
    }

    private void UpdateAppearance(Entity<DHControlPointComponent> ent)
    {
        var value = ent.Comp.Holder switch
        {
            DHAssaultFaction.Republic => 1,
            DHAssaultFaction.Empire => 2,
            _ => 0,
        };
        _appearance.SetData(ent, DHControlPointVisuals.Owner, value);
    }

    private void ApplyPower(Entity<DHControlPointPoweredComponent> ent, DHAssaultFaction? owner)
    {
        var enabled = owner == ent.Comp.Faction;
        ent.Comp.Disabled = !enabled;

        // Питаемое от сети — отключаем питание; без проводки (фонари «всегда включены») — гасим свет.
        if (TryComp<ApcPowerReceiverComponent>(ent, out var receiver))
            _power.SetPowerDisabled(ent, !enabled, receiver);
        else if (_light.TryGetLight(ent, out var light))
            _light.SetEnabled(ent, enabled, light);
    }

    #endregion

    private void OnExamined(Entity<DHControlPointComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("dh-control-point-examine-name", ("point", Loc.GetString(ent.Comp.PointName))));
        args.PushMarkup(Loc.GetString(ent.Comp.Holder switch
        {
            DHAssaultFaction.Republic => "dh-control-point-examine-republic",
            DHAssaultFaction.Empire => "dh-control-point-examine-empire",
            _ => "dh-control-point-examine-neutral",
        }));

        if (ent.Comp.PointDescription is { } desc)
            args.PushMarkup(Loc.GetString(desc));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;

        var query = EntityQueryEnumerator<DHControlPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var point, out var xform))
        {
            if (point.Holder is not { } owner || now < point.NextOutput)
                continue;

            point.NextOutput = now + point.Interval;
            var output = owner == DHAssaultFaction.Republic ? point.RepublicOutput : point.EmpireOutput;
            if (output.Count == 0)
                continue;

            // Ящики — на клетку южнее терминала.
            var coordinates = xform.Coordinates.Offset(new System.Numerics.Vector2(0f, -1f));
            foreach (var proto in output)
            {
                Spawn(proto, coordinates);
            }

            _assault.AnnounceToFaction(owner, Loc.GetString("dh-control-point-sender"),
                Loc.GetString("dh-control-point-supply", ("point", Loc.GetString(point.PointName))));
        }
    }
}
