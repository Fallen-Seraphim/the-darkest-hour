using System.Linq;
using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.ControlPoints;
using Content.Shared._DarkestHour.Flags;
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
/// Зоны захвата: захват флагами (решение автора 2026-11-26), питание света и ПВО от зоны (АЭС). Поставок с точек нет.
/// </summary>
public sealed partial class DHControlPointSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private Content.Server._DarkestHour.Battlefield.DHBattlefieldSystem _battlefield = default!;
    [Dependency] private Content.Server._DarkestHour.Supply.DHCommandPointsSystem _commandPoints = default!;
    [Dependency] private DHAssaultRuleSystem _assault = default!;
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
    private TimeSpan _nextResync;

    /// <summary>Как часто сверять питание с владельцами зон (на случай рассинхрона при загрузке карты).</summary>
    private static readonly TimeSpan ResyncInterval = TimeSpan.FromSeconds(5);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHControlPointComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DHControlPointComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DHPlantableFlagComponent, DHFlagRemoveDelayEvent>(OnFlagRemoveDelay);
        SubscribeLocalEvent<DHControlPointPoweredComponent, MapInitEvent>(OnPoweredMapInit);
        SubscribeLocalEvent<DHControlPointPoweredComponent, ExaminedEvent>(OnPoweredExamined);
    }

    private void OnPoweredExamined(Entity<DHControlPointPoweredComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.BackupUntil is { } until)
        {
            var left = Math.Max(0, (int) Math.Ceiling((until - _timing.CurTime).TotalMinutes));
            args.PushMarkup(Loc.GetString("dh-control-point-powered-backup", ("minutes", left)));
        }
        else if (ent.Comp.Disabled)
        {
            // Причина: объект зоны сломан, зона заглушена флагом LA-V или потеряна.
            var point = ent.Comp.Requires.FirstOrDefault(r => EffectiveHolder(r) != ent.Comp.Faction) ?? ent.Comp.Point;
            var key = _battlefield.PointOutOfAction(point) ? "dh-control-point-powered-off-broken"
                : Jammed(point) ? "dh-control-point-powered-off-jammed"
                : "dh-control-point-powered-off";
            args.PushMarkup(Loc.GetString(key));
        }
    }

    private void OnMapInit(Entity<DHControlPointComponent> ent, ref MapInitEvent args)
    {
        UpdateAppearance(ent);

        // Точка у стороны с начала раунда: бонуса за её «захват» не будет.
        if (ent.Comp.Holder is { } holder)
            _commandPoints.HeldFromStart(ent.Comp, holder);

        // Питаемое могло инициализироваться раньше зоны — сверить заново.
        var powered = EntityQueryEnumerator<DHControlPointPoweredComponent>();
        while (powered.MoveNext(out var uid, out var comp))
        {
            if (comp.Point != ent.Comp.PointId && !comp.Requires.Contains(ent.Comp.PointId))
                continue;

            comp.BackupUntil = null;
            comp.Disabled = false;
            comp.Applied = false;
            ApplyPower((uid, comp));
        }
    }

    private void OnPoweredMapInit(Entity<DHControlPointPoweredComponent> ent, ref MapInitEvent args)
    {
        // Зона могла появиться раньше или позже — сверяемся с владельцами зон по id.
        ApplyPower(ent);
    }

    #region Захват

    /// <summary>Чужой флаг снимать дольше своего.</summary>
    private void OnFlagRemoveDelay(Entity<DHPlantableFlagComponent> ent, ref DHFlagRemoveDelayEvent args)
    {
        var own = ent.Comp.Faction is { } faction && TryComp<DHAssaultFighterComponent>(args.User, out var fighter) &&
                  fighter.Faction == faction ||
                  ent.Comp.ThirdParty && HasComp<Content.Server._DarkestHour.ThirdParty.DHLAVMemberComponent>(args.User);
        if (!own)
            args.Delay = ent.Comp.EnemyRemoveDelay;
    }

    /// <summary>Воткнутые флаги в зоне точки: стороны «Осады» и флаг третьей стороны.</summary>
    public (bool Republic, bool Empire, bool ThirdParty) FlagsIn(EntityUid point, float radius)
    {
        var republic = false;
        var empire = false;
        var third = false;
        foreach (var flag in _lookup.GetEntitiesInRange<DHPlantableFlagComponent>(Transform(point).Coordinates, radius))
        {
            if (!Transform(flag).Anchored)
                continue;

            if (flag.Comp.ThirdParty)
                third = true;
            else if (flag.Comp.Faction == DHAssaultFaction.Republic)
                republic = true;
            else if (flag.Comp.Faction == DHAssaultFaction.Empire)
                empire = true;
        }

        return (republic, empire, third);
    }

    /// <summary>
    /// Захват флагами: свой флаг в зоне без чужих <see cref="DHControlPointComponent.CaptureTime"/> — точка переходит;
    /// флаги обеих сторон — оспорена (очков никому); флаг LA-V — заглушена (нет очков и эффектов).
    /// </summary>
    private void UpdateCapture(Entity<DHControlPointComponent> ent, TimeSpan now)
    {
        var (republic, empire, third) = FlagsIn(ent, ent.Comp.Radius);
        var contested = republic && empire && !third;
        if (ent.Comp.Jammed != third || ent.Comp.Contested != contested)
        {
            var wasJammed = ent.Comp.Jammed;
            ent.Comp.Jammed = third;
            ent.Comp.Contested = contested;
            Dirty(ent);
            UpdateAppearance(ent);
            RefreshPoint(ent.Comp.PointId);

            if (third && !wasJammed)
                AnnounceJam(ent);
        }

        DHAssaultFaction? capturer = null;
        if (!third && republic != empire)
            capturer = republic ? DHAssaultFaction.Republic : DHAssaultFaction.Empire;

        if (capturer == null || capturer == ent.Comp.Holder)
        {
            ent.Comp.Capturing = null;
            return;
        }

        if (ent.Comp.Capturing != capturer)
        {
            ent.Comp.Capturing = capturer;
            ent.Comp.CaptureStart = now;

            // Хозяину точки — тревога: противник водружает флаг.
            if (ent.Comp.Holder is { } holder)
            {
                _assault.AnnounceToFaction(holder, Loc.GetString("dh-control-point-sender"),
                    Loc.GetString("dh-control-point-flag-raised", ("point", Loc.GetString(ent.Comp.PointName)),
                        ("seconds", (int) ent.Comp.CaptureTime.TotalSeconds)), CaptureSound);
            }

            return;
        }

        if (now - ent.Comp.CaptureStart < ent.Comp.CaptureTime)
            return;

        ent.Comp.Capturing = null;
        Capture(ent, capturer.Value);
    }

    private void AnnounceJam(Entity<DHControlPointComponent> ent)
    {
        var text = Loc.GetString("dh-control-point-jammed", ("point", Loc.GetString(ent.Comp.PointName)));
        foreach (var faction in new[] { DHAssaultFaction.Republic, DHAssaultFaction.Empire })
        {
            _assault.AnnounceToFaction(faction, Loc.GetString("dh-control-point-sender"), text, CaptureSound);
        }
    }

    private void Capture(Entity<DHControlPointComponent> ent, DHAssaultFaction faction)
    {
        var previous = ent.Comp.Holder;
        SetOwner(ent, faction);
        _commandPoints.Captured(ent.Comp, faction);

        var name = Loc.GetString(ent.Comp.PointName);
        // Итоги раунда (план п. 6): хронология и захват — бойцам стороны в зоне точки.
        _assault.RecordCapture(name, faction, previous, CapturersOf(ent, faction));
        var sender = Loc.GetString("dh-control-point-sender");
        _assault.AnnounceToFaction(faction, sender,
            Loc.GetString("dh-control-point-captured-ours", ("point", name)), CaptureSound);

        var enemy = faction == DHAssaultFaction.Republic ? DHAssaultFaction.Empire : DHAssaultFaction.Republic;
        _assault.AnnounceToFaction(enemy, sender,
            Loc.GetString(previous == enemy ? "dh-control-point-lost" : "dh-control-point-captured-enemy",
                ("point", name)), CaptureSound);
    }

    /// <summary>Кто участвовал в захвате: живые бойцы стороны в зоне точки.</summary>
    private List<EntityUid> CapturersOf(Entity<DHControlPointComponent> ent, DHAssaultFaction faction)
    {
        var result = new List<EntityUid>();
        foreach (var fighter in _lookup.GetEntitiesInRange<DHAssaultFighterComponent>(Transform(ent).Coordinates,
                     ent.Comp.Radius))
        {
            if (fighter.Comp.Faction == faction && _mobState.IsAlive(fighter))
                result.Add(fighter.Owner);
        }

        return result;
    }

    /// <summary>Сменить владельца зоны (захват, админ, правило).</summary>
    public void SetOwner(Entity<DHControlPointComponent> ent, DHAssaultFaction? owner)
    {
        ent.Comp.Holder = owner;
        ent.Comp.Capturing = null;
        Dirty(ent);
        UpdateAppearance(ent);

        var powered = EntityQueryEnumerator<DHControlPointPoweredComponent>();
        while (powered.MoveNext(out var uid, out var comp))
        {
            if (comp.Point == ent.Comp.PointId || comp.Requires.Contains(ent.Comp.PointId))
                ApplyPower((uid, comp));
        }
    }

    private void UpdateAppearance(Entity<DHControlPointComponent> ent)
    {
        var value = ent.Comp.Jammed ? 3 : ent.Comp.Holder switch
        {
            DHAssaultFaction.Republic => 1,
            DHAssaultFaction.Empire => 2,
            _ => 0,
        };
        _appearance.SetData(ent, DHControlPointVisuals.Owner, value);
    }

    /// <summary>Кто держит зону с этим id (null — ничья или такой зоны нет).</summary>
    public DHAssaultFaction? HolderOf(string pointId)
    {
        var query = EntityQueryEnumerator<DHControlPointComponent>();
        while (query.MoveNext(out _, out var point))
        {
            if (point.PointId == pointId)
                return point.Holder;
        }

        return null;
    }

    /// <summary>
    /// Чей эффект даёт зона: владелец, если её объекты в строю. Сломанный объект, выключающий зону (энергоблок АЭС),
    /// гасит её эффект, пока его не починят (разрушаемость POI, решение автора 2026-11-19). Флаг LA-V в зоне глушит
    /// эффект (решение автора 2026-11-26).
    /// </summary>
    private DHAssaultFaction? EffectiveHolder(string pointId)
    {
        return _battlefield.PointOutOfAction(pointId) || Jammed(pointId) ? null : HolderOf(pointId);
    }

    private bool Jammed(string pointId)
    {
        var query = EntityQueryEnumerator<DHControlPointComponent>();
        while (query.MoveNext(out _, out var point))
        {
            if (point.PointId == pointId)
                return point.Jammed;
        }

        return false;
    }

    /// <summary>Объект зоны сломан или починен: пересчитать всё, что от неё питается.</summary>
    public void RefreshPoint(string pointId)
    {
        var powered = EntityQueryEnumerator<DHControlPointPoweredComponent>();
        while (powered.MoveNext(out var uid, out var comp))
        {
            if (comp.Point == pointId || comp.Requires.Contains(pointId))
                ApplyPower((uid, comp));
        }
    }

    /// <summary>
    /// Пересчитать питание: нужна основная зона (её потеря — переход на резервный дизель, если он есть) и все
    /// <see cref="DHControlPointPoweredComponent.Requires"/> (их потеря — сразу молчит).
    /// </summary>
    private void ApplyPower(Entity<DHControlPointPoweredComponent> ent)
    {
        foreach (var required in ent.Comp.Requires)
        {
            if (EffectiveHolder(required) != ent.Comp.Faction)
            {
                ent.Comp.BackupUntil = null;
                SetPowered(ent, false);
                return;
            }
        }

        var enabled = EffectiveHolder(ent.Comp.Point) == ent.Comp.Faction;

        // Сеть пропала — военный объект переходит на резервный дизель (если он есть и ещё не на нём).
        if (!enabled && ent.Comp.Backup > TimeSpan.Zero && !ent.Comp.Disabled)
        {
            ent.Comp.BackupUntil ??= _timing.CurTime + ent.Comp.Backup;
            return;
        }

        ent.Comp.BackupUntil = null;
        SetPowered(ent, enabled);
    }

    private void SetPowered(Entity<DHControlPointPoweredComponent> ent, bool enabled)
    {
        if (ent.Comp.Applied && ent.Comp.Disabled == !enabled)
            return;

        ent.Comp.Applied = true;
        ent.Comp.Disabled = !enabled;

        // Питаемое от сети — отключаем питание; без проводки (фонари «всегда включены») — гасим свет.
        if (TryComp<Content.Server.Power.Components.PowerSupplierComponent>(ent, out var supplier))
            supplier.Enabled = enabled; // ввод от сети АЭС на базе (DHGridIntake)
        else if (TryComp<ApcPowerReceiverComponent>(ent, out var receiver))
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

        if (ent.Comp.Jammed)
            args.PushMarkup(Loc.GetString("dh-control-point-examine-jammed"));
        else if (ent.Comp.Contested)
            args.PushMarkup(Loc.GetString("dh-control-point-examine-contested"));
        else if (ent.Comp.Capturing is { } capturing)
        {
            var left = Math.Max(0, (int) Math.Ceiling((ent.Comp.CaptureTime - (_timing.CurTime - ent.Comp.CaptureStart)).TotalSeconds));
            args.PushMarkup(Loc.GetString(capturing == DHAssaultFaction.Republic
                ? "dh-control-point-examine-capturing-republic"
                : "dh-control-point-examine-capturing-empire", ("seconds", left)));
        }

        args.PushMarkup(Loc.GetString("dh-control-point-examine-how", ("radius", (int) ent.Comp.Radius)));

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

        // Сверка: питаемое могло выставиться раньше, чем зона (порядок загрузки карты) — ввод АЭС на базе
        // показывал «сеть АЭС потеряна» при АЭС у Республики (2026-11-26). Раз в 5 с пересчитываем всё.
        if (now >= _nextResync)
        {
            _nextResync = now + ResyncInterval;
            var all = EntityQueryEnumerator<DHControlPointPoweredComponent>();
            while (all.MoveNext(out var puid, out var pcomp))
            {
                ApplyPower((puid, pcomp));
            }
        }

        // Резерв кончился — гаснет.
        var backups = EntityQueryEnumerator<DHControlPointPoweredComponent>();
        while (backups.MoveNext(out var buid, out var bcomp))
        {
            if (bcomp.BackupUntil is { } until && now >= until)
            {
                bcomp.BackupUntil = null;
                SetPowered((buid, bcomp), false);
            }
        }

        var query = EntityQueryEnumerator<DHControlPointComponent>();
        while (query.MoveNext(out var uid, out var point))
        {
            UpdateCapture((uid, point), now);
        }
    }
}
