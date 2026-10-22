using System.Linq;
using System.Text;
using Content.Server._DarkestHour.Medical.Disease;
using Content.Server._DarkestHour.Medical.Radiation;
using Content.Server.GameTicking;
using Content.Shared._DarkestHour.Investigation;
using Content.Shared._DarkestHour.Medical.Disease;
using Content.Shared._DarkestHour.Medical.Radiation;
using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Investigation;

/// <summary>
/// Время смерти, записанное в момент смерти (для вскрытия). Снимается, если тело оживили.
/// </summary>
[RegisterComponent]
public sealed partial class DHDeathRecordComponent : Component
{
    [DataField]
    public TimeSpan TimeOfDeath;
}

/// <summary>
/// Вскрытие: сканер вскрытия на мёртвом теле → протокол: оценка времени смерти, повреждения по видам, травмы по
/// частям тела, накопленные дозы, болезни, вещества в крови.
/// </summary>
public sealed partial class DHAutopsySystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DHDiseaseSystem _disease = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private DHDoseSystem _dose = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<DHAutopsyScannerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DHAutopsyScannerComponent, DHAutopsyDoAfterEvent>(OnDoAfter);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            EnsureComp<DHDeathRecordComponent>(args.Target).TimeOfDeath = _gameTicker.RoundDuration();
        else if (args.OldMobState == MobState.Dead)
            RemComp<DHDeathRecordComponent>(args.Target);
    }

    private void OnAfterInteract(Entity<DHAutopsyScannerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<DamageableComponent>(target))
            return;

        args.Handled = true;
        if (!_mobState.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("dh-autopsy-not-dead"), target, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("dh-autopsy-start", ("target", Identity.Entity(target, EntityManager))), target, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new DHAutopsyDoAfterEvent(),
            ent, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnDoAfter(Entity<DHAutopsyScannerComponent> ent, ref DHAutopsyDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !_mobState.IsDead(target))
            return;

        args.Handled = true;
        var name = Identity.Name(target, EntityManager);
        var report = BuildReport(target, name);

        var paper = Spawn(ent.Comp.PaperPrototype, Transform(args.User).Coordinates);
        _metaData.SetEntityName(paper, Loc.GetString("dh-autopsy-report-name", ("name", name)));
        if (TryComp<PaperComponent>(paper, out var paperComp))
            _paper.SetContent((paper, paperComp), report);

        _audio.PlayPvs(PrintSound, ent);
    }

    private string BuildReport(EntityUid target, string name)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Loc.GetString("dh-autopsy-report-header", ("name", name)));
        sb.AppendLine();

        // время смерти: оценка с погрешностью до 2 минут
        var now = _gameTicker.RoundDuration();
        if (TryComp<DHDeathRecordComponent>(target, out var record))
        {
            var estimate = record.TimeOfDeath + TimeSpan.FromSeconds(_random.Next(-120, 121));
            if (estimate < TimeSpan.Zero)
                estimate = TimeSpan.Zero;
            var ago = (int) Math.Max(0, (now - record.TimeOfDeath).TotalMinutes);
            sb.AppendLine(Loc.GetString("dh-autopsy-report-time", ("time", FormatTime(estimate)), ("ago", ago)));
        }
        else
        {
            sb.AppendLine(Loc.GetString("dh-autopsy-report-time-unknown"));
        }

        // повреждения по видам: сначала основная причина
        var damage = _damageable.GetAllDamage(target).DamageDict
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .ToList();
        sb.AppendLine();
        if (damage.Count == 0)
        {
            sb.AppendLine(Loc.GetString("dh-autopsy-report-no-damage"));
        }
        else
        {
            sb.AppendLine(Loc.GetString("dh-autopsy-report-cause", ("type", DamageName(damage[0].Key))));
            sb.AppendLine(Loc.GetString("dh-autopsy-report-damage-header"));
            foreach (var (type, amount) in damage)
            {
                sb.AppendLine(Loc.GetString("dh-autopsy-report-damage-line", ("type", DamageName(type)), ("amount", (int) amount.Float())));
            }
        }

        // травмы по частям тела
        if (TryComp<DHTraumaComponent>(target, out var trauma) && trauma.Summary.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(Loc.GetString("dh-autopsy-report-trauma-header"));
            foreach (var line in trauma.Summary)
            {
                sb.AppendLine("- " + line);
            }
        }

        // дозы
        var rads = _dose.GetDose(target, DHDoseKind.Radiation);
        var chronite = _dose.GetDose(target, DHDoseKind.Chronite);
        if (rads >= 1f || chronite >= 1f)
        {
            sb.AppendLine();
            sb.AppendLine(Loc.GetString("dh-autopsy-report-dose", ("rads", (int) rads), ("chronite", (int) chronite)));
        }

        // болезни: опознанные станцией — по названию, остальные — «неопознанная инфекция»
        if (TryComp<DHDiseaseCarrierComponent>(target, out var carrier) && carrier.Diseases.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(Loc.GetString("dh-autopsy-report-disease-header"));
            foreach (var id in carrier.Diseases.Keys)
            {
                if (!_proto.TryIndex(id, out var disease))
                    continue;
                sb.AppendLine("- " + (_disease.IsKnown(disease)
                    ? Loc.GetString(disease.Name)
                    : Loc.GetString("dh-autopsy-report-disease-unknown")));
            }
        }

        // вещества в крови и следы метаболизма
        var chems = new Dictionary<string, float>();
        if (TryComp<BloodstreamComponent>(target, out var blood))
        {
            CollectReagents(target, blood.BloodSolutionName, chems);
            CollectReagents(target, blood.MetabolitesSolutionName, chems);
        }

        sb.AppendLine();
        if (chems.Count == 0)
        {
            sb.AppendLine(Loc.GetString("dh-autopsy-report-chems-none"));
        }
        else
        {
            sb.AppendLine(Loc.GetString("dh-autopsy-report-chems-header"));
            foreach (var (reagent, amount) in chems.OrderByDescending(kv => kv.Value))
            {
                var reagentName = _proto.TryIndex<ReagentPrototype>(reagent, out var proto) ? proto.LocalizedName : reagent;
                sb.AppendLine(Loc.GetString("dh-autopsy-report-chem-line", ("reagent", reagentName), ("amount", MathF.Round(amount, 1))));
            }
        }

        sb.AppendLine();
        sb.AppendLine(Loc.GetString("dh-autopsy-report-footer", ("time", FormatTime(now))));
        return sb.ToString();
    }

    private void CollectReagents(EntityUid target, string solutionName, Dictionary<string, float> chems)
    {
        if (!_solution.TryGetSolution(target, solutionName, out _, out var solution))
            return;

        foreach (var (reagent, quantity) in solution.Contents)
        {
            var id = reagent.Prototype;
            if (id == "Blood" || quantity <= 0)
                continue;
            chems[id] = chems.GetValueOrDefault(id) + quantity.Float();
        }
    }

    private string DamageName(string type)
    {
        return _proto.TryIndex<Content.Shared.Damage.Prototypes.DamageTypePrototype>(type, out var proto)
            ? proto.LocalizedName
            : type;
    }

    public static string FormatTime(TimeSpan time)
    {
        return $"{(int) time.TotalHours:00}:{time.Minutes:00}";
    }
}
