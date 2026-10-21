using Content.Server._DarkestHour.Medical.Disease;
using Content.Shared._DarkestHour.Medical.Disease;
using Content.Shared._DarkestHour.Medical.Radiation;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Medical.Radiation;

/// <summary>
/// Накопленные дозы: радиация копится из полученного урона «Radiation», хронит — от источников хронита.
/// По порогам дозы ставит стадию лучевой и хронитовой кожно-покровной болезни; хронит даёт фиолетовое свечение кожи.
/// Дозиметр показывает дозы. Тик раз в 2 секунды, только у облучённых.
/// </summary>
public sealed partial class DHDoseSystem : EntitySystem
{
    [Dependency] private DHDiseaseSystem _disease = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PointLightSystem _light = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public static readonly ProtoId<DHDiseasePrototype> RadiationSickness = "DHDiseaseRadiationSickness";
    public static readonly ProtoId<DHDiseasePrototype> ChroniteSkin = "DHDiseaseChroniteSkin";

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);
    private static readonly Color ChroniteGlow = Color.FromHex("#9b4dff");

    private readonly HashSet<Entity<DHDoseComponent>> _nearby = new();
    private TimeSpan _nextEmit;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHDoseComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<DHDosimeterComponent, UseInHandEvent>(OnDosimeterUse);
        SubscribeLocalEvent<DHDosimeterComponent, AfterInteractEvent>(OnDosimeterAfterInteract);
    }

    private void OnDamageChanged(Entity<DHDoseComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is not { } delta)
            return;

        if (delta.DamageDict.TryGetValue("Radiation", out var rads) && rads > 0)
        {
            ent.Comp.Radiation += rads.Float();
            Wake(ent);
        }
    }

    private void Wake(Entity<DHDoseComponent> ent)
    {
        if (ent.Comp.NextUpdate < _timing.CurTime || ent.Comp.NextUpdate == TimeSpan.MaxValue)
            ent.Comp.NextUpdate = _timing.CurTime + Interval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var seconds = (float) Interval.TotalSeconds;

        // Источники хронита облучают всех рядом раз в 2 секунды. Их мало (кассеты, хронит), поиск только вокруг них.
        if (now >= _nextEmit)
            Emit(seconds);

        var query = EntityQueryEnumerator<DHDoseComponent>();
        while (query.MoveNext(out var uid, out var dose))
        {
            if (now < dose.NextUpdate)
                continue;

            if (dose.Radiation <= 0f && dose.Chronite <= 0f && !dose.GlowAdded)
            {
                // Чисто — тик не нужен, пока снова не облучат (Wake).
                dose.NextUpdate = TimeSpan.MaxValue;
                _disease.SetStage(uid, RadiationSickness, null);
                _disease.SetStage(uid, ChroniteSkin, null);
                continue;
            }

            dose.NextUpdate = now + Interval;

            if (!_mobState.IsDead(uid))
                dose.Radiation = MathF.Max(0f, dose.Radiation - dose.RadiationDecay * seconds);

            _disease.SetStage(uid, RadiationSickness, Stage(dose.Radiation, dose.RadiationStages));

            var chroniteStage = Stage(dose.Chronite, dose.ChroniteStages);
            _disease.SetStage(uid, ChroniteSkin, chroniteStage);
            UpdateGlow((uid, dose), chroniteStage);
        }
    }

    private void Emit(float seconds)
    {
        _nextEmit = _timing.CurTime + Interval;

        var emitters = EntityQueryEnumerator<DHChroniteEmitterComponent, TransformComponent>();
        while (emitters.MoveNext(out _, out var emitter, out var xform))
        {
            if (emitter.Rate <= 0f || xform.MapID == MapId.Nullspace)
                continue;

            var coords = _transform.GetMapCoordinates(xform);
            _nearby.Clear();
            _lookup.GetEntitiesInRange(coords, emitter.Range, _nearby);
            foreach (var target in _nearby)
            {
                var distance = (_transform.GetMapCoordinates(target).Position - coords.Position).Length();
                var falloff = MathF.Max(0f, 1f - distance / MathF.Max(0.1f, emitter.Range));
                target.Comp.Chronite += emitter.Rate * falloff * seconds;
                Wake(target);
            }
        }
    }

    /// <summary>Номер стадии по порогам (0 — первая), null — ниже первого порога.</summary>
    private static int? Stage(float dose, List<float> thresholds)
    {
        int? stage = null;
        for (var i = 0; i < thresholds.Count; i++)
        {
            if (dose >= thresholds[i])
                stage = i;
        }

        return stage;
    }

    /// <summary>Хронит в коже светится фиолетовым — тем ярче, чем дальше болезнь.</summary>
    private void UpdateGlow(Entity<DHDoseComponent> ent, int? stage)
    {
        if (stage is not { } index)
        {
            if (ent.Comp.GlowAdded)
            {
                RemComp<PointLightComponent>(ent);
                ent.Comp.GlowAdded = false;
            }

            return;
        }

        // Если у сущности уже есть свой свет — не трогаем его.
        if (!ent.Comp.GlowAdded && HasComp<PointLightComponent>(ent))
            return;

        var light = _light.EnsureLight(ent);
        ent.Comp.GlowAdded = true;
        _light.SetColor(ent, ChroniteGlow, light);
        _light.SetRadius(ent, 1.2f + index * 0.4f, light);
        _light.SetEnergy(ent, 0.6f + index * 0.25f, light);
        _light.SetCastShadows(ent, false, light);
    }

    #region API

    public float GetDose(EntityUid uid, DHDoseKind kind)
    {
        if (!TryComp<DHDoseComponent>(uid, out var dose))
            return 0f;

        return kind == DHDoseKind.Radiation ? dose.Radiation : dose.Chronite;
    }

    /// <summary>Кристаллизация кожи есть, но болезнь ещё не на последней (неизлечимой) стадии — можно иссекать.</summary>
    public bool CanExciseChronite(EntityUid uid)
    {
        if (!TryComp<DHDoseComponent>(uid, out var dose) || dose.ChroniteStages.Count < 2)
            return false;

        return dose.Chronite >= dose.ChroniteStages[1] && dose.Chronite < dose.ChroniteStages[^1];
    }

    /// <summary>Облучить хронитом (разрывной прыжок, авария реактора).</summary>
    public void AddChronite(EntityUid uid, float amount)
    {
        if (amount <= 0f || !TryComp<DHDoseComponent>(uid, out var dose))
            return;

        dose.Chronite += amount;
        Wake((uid, dose));
    }

    /// <summary>Иссечение поражённой кожи убирает часть накопленного хронита.</summary>
    public void ExciseChronite(EntityUid uid, float amount)
    {
        if (!TryComp<DHDoseComponent>(uid, out var dose))
            return;

        dose.Chronite = MathF.Max(0f, dose.Chronite - amount);
        Wake((uid, dose));
    }

    #endregion

    #region Dosimeter

    private void OnDosimeterUse(Entity<DHDosimeterComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ShowReading(args.User, args.User);
    }

    private void OnDosimeterAfterInteract(Entity<DHDosimeterComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<DHDoseComponent>(target))
            return;

        args.Handled = true;
        ShowReading(args.User, target);
    }

    private void ShowReading(EntityUid user, EntityUid target)
    {
        if (!TryComp<DHDoseComponent>(target, out var dose))
            return;

        var message = Loc.GetString("dh-dosimeter-reading",
            ("target", target),
            ("radiation", (int) MathF.Round(dose.Radiation)),
            ("radStage", StageName(Stage(dose.Radiation, dose.RadiationStages))),
            ("chronite", (int) MathF.Round(dose.Chronite)),
            ("chrStage", StageName(Stage(dose.Chronite, dose.ChroniteStages))));

        _popup.PopupEntity(message, target, user, PopupType.Medium);
    }

    private string StageName(int? stage)
    {
        return stage is { } index
            ? Loc.GetString("dh-dosimeter-stage", ("stage", index + 1))
            : Loc.GetString("dh-dosimeter-safe");
    }

    #endregion
}
