using System.Linq;
using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Shared._DarkestHour.CCVar;
using Content.Shared._DarkestHour.Medical.LifeSupport;
using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Traits.Assorted;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._DarkestHour.Medical.LifeSupport;

/// <summary>
/// Стационар (этап 6 переработки медицины, очередь пп. 10–13):
/// <list type="bullet">
/// <item>клиническая смерть — окно реанимации <see cref="DHMedicalCVars.ReviveWindow"/>, потом смерть мозга
/// (ванильный Unrevivable: дефибриллятор не поможет);</item>
/// <item>СЛР — повторяющееся действие над пациентом в критическом состоянии или мёртвым: дыхание «рот в рот» снимает
/// удушье, массаж сердца останавливает отсчёт клинической смерти и изредка запускает сердце; может сломать рёбра;</item>
/// <item>аппарат ИВЛ и система жизнеобеспечения — подключаются к пациенту рядом (койка, операционный стол).
/// ИВЛ дышит за пациента; жизнеобеспечение не даёт умереть (поднимает порог смерти), заменяет сердце и лёгкие
/// при операциях и останавливает отсчёт клинической смерти.</item>
/// </list>
/// </summary>
public sealed partial class DHLifeSupportSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRottingSystem _rotting = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";
    private static readonly ProtoId<DamageTypePrototype> Bloodloss = "Bloodloss";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";

    /// <summary>Один цикл СЛР (30 компрессий и 2 вдоха).</summary>
    private static readonly TimeSpan CprCycle = TimeSpan.FromSeconds(4);

    /// <summary>Сколько после цикла СЛР мозг считается снабжаемым кровью.</summary>
    private static readonly TimeSpan CprPerfusion = TimeSpan.FromSeconds(8);

    private const float CprAsphyxiationHeal = 5f;
    private const float CprRestartChance = 0.05f;
    private const float CprRibChance = 0.05f;

    private float _window;
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, DHMedicalCVars.ReviveWindow, v => _window = v, true);

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<DHBrainDeadComponent, RejuvenateEvent>(OnRejuvenate);

        SubscribeLocalEvent<DHTraumaComponent, GetVerbsEvent<InteractionVerb>>(OnGetCprVerbs);
        SubscribeLocalEvent<DHTraumaComponent, DHCPRDoAfterEvent>(OnCprDoAfter);

        SubscribeLocalEvent<DHMedicalMachineComponent, GetVerbsEvent<AlternativeVerb>>(OnGetMachineVerbs);
        SubscribeLocalEvent<DHMedicalMachineComponent, ComponentShutdown>(OnMachineShutdown);
        SubscribeLocalEvent<DHMachinePatientComponent, ComponentShutdown>(OnPatientShutdown);
    }

    #region Клиническая смерть

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (!HasComp<DHTraumaComponent>(args.Target))
            return;

        if (args.NewMobState == MobState.Dead)
        {
            var death = EnsureComp<DHClinicalDeathComponent>(args.Target);
            death.Elapsed = 0f;
            death.PerfusedUntil = TimeSpan.Zero;
        }
        else
        {
            RemCompDeferred<DHClinicalDeathComponent>(args.Target);
        }
    }

    private void OnRejuvenate(Entity<DHBrainDeadComponent> ent, ref RejuvenateEvent args)
    {
        RemComp<UnrevivableComponent>(ent);
        RemComp<DHBrainDeadComponent>(ent);
    }

    /// <summary>Мозг сейчас снабжается кровью: идёт СЛР или работает жизнеобеспечение.</summary>
    public bool IsPerfused(EntityUid uid, DHClinicalDeathComponent? death = null)
    {
        if (TryComp<DHMachinePatientComponent>(uid, out var patient) && patient.LifeSupport != null)
            return true;

        return Resolve(uid, ref death, false) && _timing.CurTime < death.PerfusedUntil;
    }

    private void TickClinicalDeath()
    {
        if (_window <= 0f)
            return;

        var query = EntityQueryEnumerator<DHClinicalDeathComponent>();
        while (query.MoveNext(out var uid, out var death))
        {
            if (HasComp<UnrevivableComponent>(uid) || !_mobState.IsDead(uid) || IsPerfused(uid, death))
                continue;

            death.Elapsed += 1f;
            if (death.Elapsed < _window)
                continue;

            var unrevivable = EnsureComp<UnrevivableComponent>(uid);
            unrevivable.ReasonMessage = "dh-revive-brain-death";
            Dirty(uid, unrevivable);
            EnsureComp<DHBrainDeadComponent>(uid);
        }
    }

    #endregion

    #region СЛР

    private void OnGetCprVerbs(Entity<DHTraumaComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || args.User == args.Target ||
            !_mobState.IsIncapacitated(ent))
        {
            return;
        }

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("dh-cpr-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/rejuvenate.svg.192dpi.png")),
            Act = () => StartCpr(user, ent),
            Priority = -1,
        });
    }

    private void StartCpr(EntityUid user, EntityUid target)
    {
        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, CprCycle, new DHCPRDoAfterEvent(),
            target, target: target)
        {
            BreakOnMove = true,
            NeedHand = true,
            BlockDuplicate = true,
        });

        if (started)
        {
            _popup.PopupEntity(Loc.GetString("dh-cpr-start", ("user", user), ("target", target)), target,
                PopupType.Medium);
        }
    }

    private void OnCprDoAfter(Entity<DHTraumaComponent> ent, ref DHCPRDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !_mobState.IsIncapacitated(ent))
            return;

        args.Handled = true;

        // Вдохи «рот в рот»: удушье уходит.
        _damageable.TryChangeDamage(ent.Owner, Damage(Asphyxiation, -CprAsphyxiationHeal), true, false);

        // Компрессии: иногда ломаются рёбра.
        if (_random.Prob(CprRibChance))
        {
            _damageable.TryChangeDamage(ent.Owner, Damage(Blunt, 6f), true, false, args.User);
            _popup.PopupEntity(Loc.GetString("dh-cpr-rib"), ent, PopupType.SmallCaution);
        }

        if (_mobState.IsDead(ent))
        {
            var death = EnsureComp<DHClinicalDeathComponent>(ent);
            death.PerfusedUntil = _timing.CurTime + CprPerfusion;

            if (_random.Prob(CprRestartChance) && TryRestartHeart(ent, args.User))
            {
                _popup.PopupEntity(Loc.GetString("dh-cpr-restart", ("target", ent.Owner)), ent, PopupType.Large);
                return;
            }
        }

        // Пока пациент без сознания — продолжаем.
        args.Repeat = _mobState.IsIncapacitated(ent);
    }

    /// <summary>Сердце запускается (как от дефибриллятора, но без разряда): только в окне клинической смерти.</summary>
    private bool TryRestartHeart(EntityUid target, EntityUid user)
    {
        if (_rotting.IsRotten(target) || HasComp<UnrevivableComponent>(target) ||
            !TryComp<MobStateComponent>(target, out var mobState) ||
            !_thresholds.TryGetThresholdForState(target, MobState.Dead, out var threshold) ||
            _damageable.GetTotalDamage(target) >= threshold)
        {
            return false;
        }

        _mobState.ChangeMobState(target, MobState.Critical, mobState, user);
        if (_mind.TryGetMind(target, out var mindUid, out var mind) &&
            mind.CurrentEntity != target &&
            _player.TryGetSessionById(mind.UserId, out var session))
        {
            _eui.OpenEui(new ReturnToBodyEui(mind, _mind, _player), session);
        }

        return true;
    }

    #endregion

    #region Аппараты

    private void OnGetMachineVerbs(Entity<DHMedicalMachineComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        if (ent.Comp.Patient is { } patient)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("dh-machine-disconnect-verb"),
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
                Act = () => Disconnect(ent, user),
            });
            return;
        }

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dh-machine-connect-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/plus.svg.192dpi.png")),
            Act = () => Connect(ent, user),
        });
    }

    private void Connect(Entity<DHMedicalMachineComponent> ent, EntityUid user)
    {
        if (ent.Comp.Patient != null)
            return;

        // Ближайший подходящий пациент: без сознания (или мёртвый) — к ИВЛ и жизнеобеспечению подключают только таких.
        var coords = _transform.GetMapCoordinates(ent);
        EntityUid? best = null;
        var bestDist = float.MaxValue;
        foreach (var candidate in _lookup.GetEntitiesInRange<DHTraumaComponent>(coords, ent.Comp.Range))
        {
            if (!_mobState.IsIncapacitated(candidate) || Occupied(candidate, ent.Comp.Kind))
                continue;

            var dist = (_transform.GetMapCoordinates(candidate).Position - coords.Position).Length();
            if (dist < bestDist)
            {
                bestDist = dist;
                best = candidate;
            }
        }

        if (best is not { } patient)
        {
            _popup.PopupEntity(Loc.GetString("dh-machine-no-patient"), ent, user);
            return;
        }

        ent.Comp.Patient = patient;
        var link = EnsureComp<DHMachinePatientComponent>(patient);
        if (ent.Comp.Kind == DHMedicalMachineKind.Ventilator)
        {
            link.Ventilator = ent;
        }
        else
        {
            link.LifeSupport = ent;
            RaiseDeathThreshold((patient, link), ent.Comp.DeathThresholdBonus);
        }

        _popup.PopupEntity(Loc.GetString(ent.Comp.Kind == DHMedicalMachineKind.Ventilator
                ? "dh-ventilator-connected"
                : "dh-life-support-connected", ("user", user), ("target", patient)),
            patient, PopupType.Medium);
    }

    private bool Occupied(EntityUid patient, DHMedicalMachineKind kind)
    {
        return TryComp<DHMachinePatientComponent>(patient, out var link) &&
               (kind == DHMedicalMachineKind.Ventilator ? link.Ventilator : link.LifeSupport) != null;
    }

    private void Disconnect(Entity<DHMedicalMachineComponent> ent, EntityUid? user)
    {
        if (ent.Comp.Patient is not { } patient)
            return;

        ent.Comp.Patient = null;
        if (TryComp<DHMachinePatientComponent>(patient, out var link))
        {
            if (link.Ventilator == ent.Owner)
                link.Ventilator = null;

            if (link.LifeSupport == ent.Owner)
            {
                link.LifeSupport = null;
                RestoreDeathThreshold((patient, link));
            }

            if (link.Ventilator == null && link.LifeSupport == null)
                RemCompDeferred<DHMachinePatientComponent>(patient);
        }

        if (!TerminatingOrDeleted(patient))
        {
            _popup.PopupEntity(Loc.GetString(user == null ? "dh-machine-tubes-pulled" : "dh-machine-disconnected",
                ("target", patient)), patient, PopupType.SmallCaution);
        }
    }

    private void RaiseDeathThreshold(Entity<DHMachinePatientComponent> patient, float bonus)
    {
        if (!_thresholds.TryGetThresholdForState(patient, MobState.Dead, out var threshold))
            return;

        patient.Comp.OriginalDeadThreshold ??= threshold.Value;
        _thresholds.SetMobStateThreshold(patient, patient.Comp.OriginalDeadThreshold.Value + bonus, MobState.Dead);
    }

    private void RestoreDeathThreshold(Entity<DHMachinePatientComponent> patient)
    {
        if (patient.Comp.OriginalDeadThreshold is not { } original || TerminatingOrDeleted(patient))
            return;

        patient.Comp.OriginalDeadThreshold = null;
        _thresholds.SetMobStateThreshold(patient, original, MobState.Dead);
    }

    private void OnMachineShutdown(Entity<DHMedicalMachineComponent> ent, ref ComponentShutdown args)
    {
        Disconnect(ent, null);
    }

    private void OnPatientShutdown(Entity<DHMachinePatientComponent> ent, ref ComponentShutdown args)
    {
        foreach (var machine in new[] { ent.Comp.Ventilator, ent.Comp.LifeSupport })
        {
            if (machine is { } m && TryComp<DHMedicalMachineComponent>(m, out var comp) && comp.Patient == ent.Owner)
                comp.Patient = null;
        }

        RestoreDeathThreshold(ent);
    }

    private void TickMachines()
    {
        var query = EntityQueryEnumerator<DHMedicalMachineComponent>();
        while (query.MoveNext(out var uid, out var machine))
        {
            if (machine.Patient is not { } patient)
                continue;

            if (TerminatingOrDeleted(patient) ||
                !_transform.InRange(Transform(uid).Coordinates, Transform(patient).Coordinates, machine.Range + 0.5f))
            {
                Disconnect((uid, machine), null);
                continue;
            }

            switch (machine.Kind)
            {
                case DHMedicalMachineKind.Ventilator:
                    _damageable.TryChangeDamage(patient, Damage(Asphyxiation, -machine.AsphyxiationHeal), true, false);
                    break;
                case DHMedicalMachineKind.LifeSupport:
                    var heal = new DamageSpecifier();
                    heal.DamageDict[Bloodloss] = FixedPoint2.New(-machine.BloodlossHeal);
                    heal.DamageDict[Asphyxiation] = FixedPoint2.New(-1);
                    _damageable.TryChangeDamage(patient, heal, true, false);
                    break;
            }
        }
    }

    /// <summary>Аппараты заменяют органы: лёгкие — ИВЛ и жизнеобеспечение, сердце — жизнеобеспечение.</summary>
    public bool ReplacesOrgan(EntityUid patient, string category)
    {
        if (!TryComp<DHMachinePatientComponent>(patient, out var link))
            return false;

        return category switch
        {
            "Lungs" => link.Ventilator != null || link.LifeSupport != null,
            "Heart" => link.LifeSupport != null,
            _ => false,
        };
    }

    #endregion

    private static DamageSpecifier Damage(ProtoId<DamageTypePrototype> type, float amount)
    {
        var spec = new DamageSpecifier();
        spec.DamageDict[type] = FixedPoint2.New(amount);
        return spec;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + TimeSpan.FromSeconds(1);
        TickClinicalDeath();
        TickMachines();
    }
}
