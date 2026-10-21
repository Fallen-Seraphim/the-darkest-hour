using System.Linq;
using System.Text;
using Content.Server._DarkestHour.Medical.Disease;
using Content.Server.Research.Systems;
using Content.Shared._DarkestHour.Medical.Disease;
using Content.Shared._DarkestHour.Medical.Virology;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Station.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Medical.Virology;

/// <summary>
/// Вирусология: мазок, сканер тела, анализатор (печатает строение), компьютер вирусолога (карточка случая → очки
/// исследований, болезнь известна станции, рецепт для химиков), банки с культурами.
/// </summary>
public sealed partial class DHVirologySystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DHContaminationSystem _contamination = default!;
    [Dependency] private DHDiseaseSystem _disease = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/diagnoser_printing.ogg");
    private static readonly SoundSpecifier BreakSound = new SoundCollectionSpecifier("GlassBreak");

    private readonly HashSet<Entity<DHDiseaseCarrierComponent>> _nearby = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHPathogenSwabComponent, AfterInteractEvent>(OnSwabAfterInteract);
        SubscribeLocalEvent<DHPathogenSwabComponent, DHSwabDoAfterEvent>(OnSwabDoAfter);
        SubscribeLocalEvent<DHPathogenSwabComponent, ExaminedEvent>(OnSwabExamined);

        SubscribeLocalEvent<DHPathogenScannerComponent, AfterInteractEvent>(OnScannerAfterInteract);
        SubscribeLocalEvent<DHPathogenScannerComponent, DHPathogenScanDoAfterEvent>(OnScanDoAfter);

        SubscribeLocalEvent<DHPathogenAnalyzerComponent, EntInsertedIntoContainerMessage>(OnAnalyzerInserted);

        SubscribeLocalEvent<DHCultureJarComponent, DestructionEventArgs>(OnJarDestroyed);

        Subs.BuiEvents<DHVirologyConsoleComponent>(DHVirologyConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnConsoleOpened);
            subs.Event<DHVirologySubmitMessage>(OnConsoleSubmit);
        });
    }

    #region Swab

    private void OnSwabAfterInteract(Entity<DHPathogenSwabComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!HasComp<DHDiseaseCarrierComponent>(target) && !HasComp<DHCultureJarComponent>(target))
            return;

        args.Handled = true;
        if (ent.Comp.Used)
        {
            _popup.PopupEntity(Loc.GetString("dh-swab-used"), ent, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new DHSwabDoAfterEvent(),
            ent, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnSwabDoAfter(Entity<DHPathogenSwabComponent> ent, ref DHSwabDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || ent.Comp.Used)
            return;

        args.Handled = true;
        ent.Comp.Samples.Clear();

        if (TryComp<DHDiseaseCarrierComponent>(target, out var carrier))
        {
            foreach (var id in carrier.Diseases.Keys)
            {
                if (_proto.TryIndex(id, out var proto) && proto.Identifiable)
                    ent.Comp.Samples.Add(id);
            }
        }
        else if (TryComp<DHCultureJarComponent>(target, out var jar))
        {
            ent.Comp.Samples.Add(jar.Disease);
        }

        // Мазок с больного или культуры сам заразен — после анализатора он уходит, но в руках и на полу опасен.
        _contamination.Contaminate(ent, ent.Comp.Samples);

        ent.Comp.Used = true;
        ent.Comp.Source = Name(target);
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("dh-swab-taken", ("target", target)), target, args.User);
    }

    private void OnSwabExamined(Entity<DHPathogenSwabComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(ent.Comp.Used
            ? Loc.GetString("dh-swab-examine-used", ("source", ent.Comp.Source ?? "?"))
            : Loc.GetString("dh-swab-examine-clean"));
    }

    #endregion

    #region Scanner

    private void OnScannerAfterInteract(Entity<DHPathogenScannerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<DHDiseaseCarrierComponent>(target))
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new DHPathogenScanDoAfterEvent(),
            ent, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnScanDoAfter(Entity<DHPathogenScannerComponent> ent, ref DHPathogenScanDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target ||
            !TryComp<DHDiseaseCarrierComponent>(target, out var carrier))
        {
            return;
        }

        args.Handled = true;

        var pathogen = false;
        var symptoms = new SortedSet<string>();
        foreach (var (id, disease) in carrier.Diseases)
        {
            if (!_proto.TryIndex(id, out var proto) || !proto.Identifiable)
                continue;

            pathogen = true;
            var stage = Math.Clamp((int) disease.Progress, 0, proto.Stages.Count - 1);
            if (!proto.Stages[stage].Symptomatic)
                continue;

            foreach (var symptom in proto.Symptoms)
            {
                symptoms.Add(Loc.GetString(symptom));
            }
        }

        string message;
        if (!pathogen)
            message = Loc.GetString("dh-scanner-clean", ("target", target));
        else if (symptoms.Count == 0)
            message = Loc.GetString("dh-scanner-pathogen-silent", ("target", target));
        else
            message = Loc.GetString("dh-scanner-pathogen", ("target", target), ("symptoms", string.Join(", ", symptoms)));

        _popup.PopupEntity(message, target, args.User, PopupType.Medium);
    }

    #endregion

    #region Analyzer

    private void OnAnalyzerInserted(Entity<DHPathogenAnalyzerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SlotId || ent.Comp.AnalysisEnd != null)
            return;

        if (!TryComp<DHPathogenSwabComponent>(args.Entity, out var swab) || !swab.Used)
            return;

        ent.Comp.AnalysisEnd = _timing.CurTime + ent.Comp.Duration;
        _appearance.SetData(ent, DHPathogenAnalyzerVisuals.Running, true);
        _itemSlots.SetLock(ent.Owner, ent.Comp.SlotId, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHPathogenAnalyzerComponent>();
        while (query.MoveNext(out var uid, out var analyzer))
        {
            if (analyzer.AnalysisEnd is not { } end || now < end)
                continue;

            analyzer.AnalysisEnd = null;
            FinishAnalysis((uid, analyzer));
        }
    }

    private void FinishAnalysis(Entity<DHPathogenAnalyzerComponent> ent)
    {
        _appearance.SetData(ent, DHPathogenAnalyzerVisuals.Running, false);
        _itemSlots.SetLock(ent.Owner, ent.Comp.SlotId, false);

        if (!_itemSlots.TryGetSlot(ent.Owner, ent.Comp.SlotId, out var slot) || slot.Item is not { } swabUid ||
            !TryComp<DHPathogenSwabComponent>(swabUid, out var swab))
        {
            return;
        }

        var text = new StringBuilder();
        text.AppendLine(Loc.GetString("dh-analyzer-report-header", ("source", swab.Source ?? "?")));
        text.AppendLine();

        if (swab.Samples.Count == 0)
        {
            text.AppendLine(Loc.GetString("dh-analyzer-report-clean"));
        }
        else
        {
            var index = 1;
            foreach (var id in swab.Samples.Distinct())
            {
                if (!_proto.TryIndex(id, out var proto))
                    continue;

                text.AppendLine(Loc.GetString("dh-analyzer-report-sample", ("index", index++)));
                text.AppendLine(Loc.GetString($"dh-virology-structure-{proto.Group}"));
                if (proto.Structure is { } structure)
                    text.AppendLine(Loc.GetString(structure));
                text.AppendLine();
            }
        }

        text.AppendLine(Loc.GetString("dh-analyzer-report-footer"));

        // Без вытяжного шкафа образец выходит аэрозолем в лабораторию.
        _contamination.TryAerosolLeak(ent, swab.Samples.Distinct().ToList());

        QueueDel(swabUid);
        Print(ent, ent.Comp.PaperPrototype, Loc.GetString("dh-analyzer-report-name"), text.ToString());
    }

    private void Print(EntityUid machine, EntProtoId paperProto, string name, string content)
    {
        var paper = Spawn(paperProto, Transform(machine).Coordinates);
        _metaData.SetEntityName(paper, name);
        if (TryComp<PaperComponent>(paper, out var paperComp))
            _paper.SetContent((paper, paperComp), content);

        _audio.PlayPvs(PrintSound, machine);
    }

    #endregion

    #region Culture jars

    private void OnJarDestroyed(Entity<DHCultureJarComponent> ent, ref DestructionEventArgs args)
    {
        _audio.PlayPvs(BreakSound, ent);

        _nearby.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.BreakRange, _nearby);
        foreach (var target in _nearby)
        {
            var chance = ent.Comp.BreakChance * (1f - _disease.GetProtection(target, DHSpreadVector.Airborne));
            if (_random.Prob(Math.Clamp(chance, 0f, 1f)))
                _disease.Infect(target, ent.Comp.Disease);
        }
    }

    #endregion

    #region Console

    private void OnConsoleOpened(Entity<DHVirologyConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateConsole(ent);
    }

    private void OnConsoleSubmit(Entity<DHVirologyConsoleComponent> ent, ref DHVirologySubmitMessage args)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.NextSubmit)
        {
            UpdateConsole(ent);
            return;
        }

        var match = FindMatch(args.Group, args.Spread, args.Symptoms, args.Treatment);
        if (match == null)
        {
            ent.Comp.NextSubmit = now + ent.Comp.WrongCooldown;
            ent.Comp.LastResult = Loc.GetString("dh-virology-result-wrong");
            ent.Comp.LastSuccess = false;
            UpdateConsole(ent);
            return;
        }

        var name = Loc.GetString(match.Name);
        var first = _disease.MarkKnown(match.ID);
        if (first)
        {
            AwardPoints(ent, match.ResearchPoints);
            ent.Comp.LastResult = Loc.GetString("dh-virology-result-new", ("disease", name), ("points", match.ResearchPoints));
        }
        else
        {
            ent.Comp.LastResult = Loc.GetString("dh-virology-result-known", ("disease", name));
        }

        ent.Comp.LastSuccess = true;

        var recipe = new StringBuilder();
        recipe.AppendLine(Loc.GetString("dh-virology-recipe-header", ("disease", name)));
        recipe.AppendLine();
        recipe.AppendLine(Loc.GetString("dh-virology-recipe-group", ("group", Loc.GetString($"dh-disease-group-{match.Group}"))));
        recipe.AppendLine(Loc.GetString("dh-virology-recipe-treatment", ("treatment", Loc.GetString($"dh-virology-treatment-{match.Treatment}"))));
        if (match.Recipe is { } recipeText)
        {
            recipe.AppendLine();
            recipe.AppendLine(Loc.GetString(recipeText));
        }

        Print(ent, ent.Comp.PaperPrototype, Loc.GetString("dh-virology-recipe-name", ("disease", name)), recipe.ToString());
        UpdateConsole(ent);
    }

    /// <summary>
    /// Карточка верна, если группа, путь передачи и лечение совпадают, а отмеченные симптомы — симптомы этой болезни
    /// и их не меньше половины.
    /// </summary>
    private DHDiseasePrototype? FindMatch(DHPathogenGroup group, List<DHSpreadVector> spread, List<string> symptoms,
        DHTreatmentKind treatment)
    {
        var spreadSet = spread.ToHashSet();
        foreach (var proto in _proto.EnumeratePrototypes<DHDiseasePrototype>())
        {
            if (!proto.Identifiable || proto.Group != group || proto.Treatment != treatment)
                continue;

            if (!proto.Spread.ToHashSet().SetEquals(spreadSet))
                continue;

            var diseaseSymptoms = proto.Symptoms.Select(s => s.Id).ToHashSet();
            if (symptoms.Count == 0 || !symptoms.All(diseaseSymptoms.Contains))
                continue;

            if (symptoms.Count * 2 < diseaseSymptoms.Count)
                continue;

            return proto;
        }

        return null;
    }

    private void AwardPoints(EntityUid console, int points)
    {
        var station = _station.GetOwningStation(console);
        var query = EntityQueryEnumerator<ResearchServerComponent>();
        while (query.MoveNext(out var server, out var serverComp))
        {
            if (station != null && _station.GetOwningStation(server) != station)
                continue;

            _research.ModifyServerPoints(server, points, serverComp);
            return;
        }
    }

    private void UpdateConsole(Entity<DHVirologyConsoleComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, DHVirologyConsoleUiKey.Key))
            return;

        var state = new DHVirologyConsoleState
        {
            LastResult = ent.Comp.LastResult,
            LastSuccess = ent.Comp.LastSuccess,
            CooldownSeconds = (float) Math.Max(0, (ent.Comp.NextSubmit - _timing.CurTime).TotalSeconds),
        };

        var seen = new HashSet<string>();
        foreach (var proto in _proto.EnumeratePrototypes<DHDiseasePrototype>().OrderBy(p => p.ID))
        {
            if (!proto.Identifiable)
                continue;

            foreach (var symptom in proto.Symptoms)
            {
                if (seen.Add(symptom.Id))
                    state.Symptoms.Add(new DHVirologySymptomData(symptom.Id, Loc.GetString(symptom)));
            }
        }

        state.Symptoms.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));

        foreach (var known in _disease.KnownDiseases)
        {
            if (_proto.TryIndex(known, out var proto))
                state.KnownDiseases.Add(Loc.GetString(proto.Name));
        }

        _ui.SetUiState(ent.Owner, DHVirologyConsoleUiKey.Key, state);
    }

    #endregion
}
