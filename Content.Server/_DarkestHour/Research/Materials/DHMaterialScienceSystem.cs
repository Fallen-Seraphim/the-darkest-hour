using System.Linq;
using System.Text;
using Content.Server.Research.Systems;
using Content.Shared._DarkestHour.Research.Materials;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GameTicking;
using Content.Shared.Materials;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Station.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Research.Materials;

/// <summary>
/// Материаловедение: сборка решётки на лабораторном столе (подсказки, расход материалов, образец) и химико-материальный
/// сканер (технодиск, отчёт, очки исследований за первый образец за раунд).
/// </summary>
public sealed partial class DHMaterialScienceSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedMaterialStorageSystem _materials = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly SoundSpecifier SuccessSound = new SoundPathSpecifier("/Audio/Machines/ding.ogg");
    private static readonly SoundSpecifier FailSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");
    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    /// <summary>Электроны на внешней оболочке для каждого рецепта и элемента — свои в каждом раунде.</summary>
    private readonly Dictionary<(string Recipe, DHElement Element), int> _electrons = new();

    /// <summary>Образцы, за которые уже начислены очки в этом раунде.</summary>
    private readonly HashSet<string> _scanned = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        Subs.BuiEvents<DHMaterialLabComponent>(DHMaterialLabUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnLabOpened);
            subs.Event<DHMaterialLabAttemptMessage>(OnAttempt);
        });
        SubscribeLocalEvent<DHMaterialLabComponent, MaterialAmountChangedEvent>(OnLabMaterialsChanged);

        SubscribeLocalEvent<DHMaterialScannerComponent, EntInsertedIntoContainerMessage>(OnScannerInserted);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _electrons.Clear();
        _scanned.Clear();
    }

    private int TargetElectrons(DHMaterialRecipePrototype recipe, DHElement element)
    {
        if (_electrons.TryGetValue((recipe.ID, element), out var value))
            return value;

        value = _random.Next(recipe.MinElectrons, recipe.MaxElectrons + 1);
        _electrons[(recipe.ID, element)] = value;
        return value;
    }

    #region Lab table

    private void OnLabOpened(Entity<DHMaterialLabComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateLab(ent);
    }

    private void OnLabMaterialsChanged(Entity<DHMaterialLabComponent> ent, ref MaterialAmountChangedEvent args)
    {
        if (_ui.IsUiOpen(ent.Owner, DHMaterialLabUiKey.Key))
            UpdateLab(ent);
    }

    private void OnAttempt(Entity<DHMaterialLabComponent> ent, ref DHMaterialLabAttemptMessage args)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.NextAttempt || !_proto.TryIndex<DHMaterialRecipePrototype>(args.Recipe, out var recipe))
            return;

        ent.Comp.NextAttempt = now + ent.Comp.Cooldown;

        var (success, result) = Evaluate(ent, recipe, args);
        ent.Comp.LastResult = result;
        ent.Comp.LastSuccess = success;

        if (success)
        {
            var cost = recipe.Consumes.ToDictionary(p => p.Key.Id, p => -p.Value);
            _materials.TryChangeMaterialAmount(ent.Owner, cost);

            var sample = Spawn(ent.Comp.SamplePrototype, Transform(ent).Coordinates);
            EnsureComp<DHMaterialSampleComponent>(sample).Recipe = recipe.ID;
            Dirty(sample, Comp<DHMaterialSampleComponent>(sample));
            _metaData.SetEntityName(sample, Loc.GetString("dh-material-sample-name", ("material", Loc.GetString(recipe.Name))));
            _audio.PlayPvs(SuccessSound, ent);
        }
        else
        {
            _audio.PlayPvs(FailSound, ent);
        }

        _popup.PopupEntity(result, ent, args.Actor, success ? PopupType.Medium : PopupType.SmallCaution);
        UpdateLab(ent);
    }

    /// <summary>
    /// Проверка сборки. Подсказки по ступеням: материалы → тип решётки и состав → электроны (больше/меньше по элементу).
    /// </summary>
    private (bool, string) Evaluate(EntityUid lab, DHMaterialRecipePrototype recipe, DHMaterialLabAttemptMessage args)
    {
        if (!CanAfford(lab, recipe))
            return (false, Loc.GetString("dh-material-lab-no-materials", ("materials", ConsumesText(recipe))));

        var nodes = new List<(DHElement Element, int Electrons)>();
        for (var i = 0; i < args.Elements.Count && i < args.Electrons.Count; i++)
        {
            if (args.Elements[i] != DHElement.None)
                nodes.Add((args.Elements[i], Math.Clamp(args.Electrons[i], 0, 8)));
        }

        // Состав: сколько узлов совпадает с рецептом (как мультимножество).
        var need = recipe.Elements.GroupBy(e => e).ToDictionary(g => g.Key, g => g.Count());
        var given = nodes.GroupBy(n => n.Element).ToDictionary(g => g.Key, g => g.Count());
        var matched = need.Sum(p => Math.Min(p.Value, given.GetValueOrDefault(p.Key)));
        var compositionOk = matched == recipe.Elements.Count && nodes.Count == recipe.Elements.Count;
        var latticeOk = args.Lattice == recipe.Lattice;

        if (!latticeOk || !compositionOk)
        {
            var text = new StringBuilder();
            text.Append(Loc.GetString(latticeOk ? "dh-material-lab-lattice-ok" : "dh-material-lab-lattice-wrong"));
            text.Append(' ');
            text.Append(compositionOk
                ? Loc.GetString("dh-material-lab-composition-ok")
                : Loc.GetString("dh-material-lab-composition-wrong", ("matched", matched), ("total", recipe.Elements.Count), ("given", nodes.Count)));
            return (false, text.ToString());
        }

        // Электроны: подсказка по каждому элементу — больше или меньше.
        var hints = new List<string>();
        foreach (var element in need.Keys)
        {
            var target = TargetElectrons(recipe, element);
            var low = nodes.Any(n => n.Element == element && n.Electrons < target);
            var high = nodes.Any(n => n.Element == element && n.Electrons > target);
            if (low)
                hints.Add(Loc.GetString("dh-material-lab-electrons-low", ("element", Loc.GetString($"dh-element-{element}"))));
            else if (high)
                hints.Add(Loc.GetString("dh-material-lab-electrons-high", ("element", Loc.GetString($"dh-element-{element}"))));
        }

        if (hints.Count > 0)
            return (false, Loc.GetString("dh-material-lab-shells-unstable", ("hints", string.Join(", ", hints))));

        return (true, Loc.GetString("dh-material-lab-success", ("material", Loc.GetString(recipe.Name))));
    }

    private bool CanAfford(EntityUid lab, DHMaterialRecipePrototype recipe)
    {
        foreach (var (material, amount) in recipe.Consumes)
        {
            if (_materials.GetMaterialAmount(lab, material.Id) < amount)
                return false;
        }

        return true;
    }

    private string ConsumesText(DHMaterialRecipePrototype recipe)
    {
        return string.Join(", ", recipe.Consumes.Select(p =>
            $"{(_proto.TryIndex(p.Key, out var mat) ? Loc.GetString(mat.Name) : p.Key.Id)} ×{p.Value / 100f:0.#}"));
    }

    private void UpdateLab(Entity<DHMaterialLabComponent> ent)
    {
        var recipes = new List<DHMaterialRecipeEntry>();
        var materials = new HashSet<string>();
        foreach (var recipe in _proto.EnumeratePrototypes<DHMaterialRecipePrototype>().OrderBy(r => r.Order))
        {
            recipes.Add(new DHMaterialRecipeEntry(recipe.ID, Loc.GetString(recipe.Name), recipe.Elements.Count,
                ConsumesText(recipe), CanAfford(ent, recipe)));
            foreach (var material in recipe.Consumes.Keys)
            {
                materials.Add(material.Id);
            }
        }

        var stored = string.Join(", ", materials
            .Select(m => (Id: m, Amount: _materials.GetMaterialAmount(ent.Owner, m)))
            .Where(m => m.Amount > 0)
            .Select(m => $"{(_proto.TryIndex<MaterialPrototype>(m.Id, out var mat) ? Loc.GetString(mat.Name) : m.Id)} ×{m.Amount / 100f:0.#}"));

        if (stored.Length == 0)
            stored = Loc.GetString("dh-material-lab-empty");

        _ui.SetUiState(ent.Owner, DHMaterialLabUiKey.Key,
            new DHMaterialLabState(recipes, stored, ent.Comp.LastResult, ent.Comp.LastSuccess));
    }

    #endregion

    #region Scanner

    private void OnScannerInserted(Entity<DHMaterialScannerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.SlotId || ent.Comp.ScanEnd != null)
            return;

        if (!HasComp<DHMaterialSampleComponent>(args.Entity) && GetReverseData(args.Entity) == null)
        {
            _popup.PopupEntity(Loc.GetString("dh-material-scanner-nothing"), ent);
            return;
        }

        ent.Comp.ScanEnd = _timing.CurTime + ent.Comp.Duration;
        _appearance.SetData(ent, DHMaterialScannerVisuals.Running, true);
        _itemSlots.SetLock(ent.Owner, ent.Comp.SlotId, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHMaterialScannerComponent>();
        while (query.MoveNext(out var uid, out var scanner))
        {
            if (scanner.ScanEnd is not { } end || now < end)
                continue;

            scanner.ScanEnd = null;
            FinishScan((uid, scanner));
        }
    }

    private void FinishScan(Entity<DHMaterialScannerComponent> ent)
    {
        _appearance.SetData(ent, DHMaterialScannerVisuals.Running, false);
        _itemSlots.SetLock(ent.Owner, ent.Comp.SlotId, false);

        if (_itemSlots.TryGetSlot(ent.Owner, ent.Comp.SlotId, out var trophySlot) && trophySlot.Item is { } trophy &&
            GetReverseData(trophy) is { } reverse)
        {
            ReverseEngineer(ent, trophy, reverse.Technologies, reverse.Discount, reverse.Points);
            return;
        }

        if (!_itemSlots.TryGetSlot(ent.Owner, ent.Comp.SlotId, out var slot) || slot.Item is not { } sampleUid ||
            !TryComp<DHMaterialSampleComponent>(sampleUid, out var sample) ||
            sample.Recipe is not { } recipeId ||
            !_proto.TryIndex(recipeId, out var recipe))
        {
            return;
        }

        QueueDel(sampleUid);

        var coords = Transform(ent).Coordinates;
        if (recipe.Disk is { } disk)
            Spawn(disk, coords);

        var name = Loc.GetString(recipe.Name);
        var report = new StringBuilder();
        report.AppendLine(Loc.GetString("dh-material-scanner-report-header", ("material", name)));
        report.AppendLine();
        report.AppendLine(Loc.GetString("dh-material-scanner-report-lattice", ("lattice", Loc.GetString($"dh-lattice-{recipe.Lattice}"))));
        report.AppendLine(Loc.GetString("dh-material-scanner-report-composition",
            ("composition", string.Join(", ", recipe.Elements.GroupBy(e => e).Select(g => $"{Loc.GetString($"dh-element-{g.Key}")} ×{g.Count()}")))));

        if (_scanned.Add(recipe.ID) && recipe.ResearchPoints > 0)
        {
            AwardPoints(ent, recipe.ResearchPoints);
            report.AppendLine(Loc.GetString("dh-material-scanner-report-points", ("points", recipe.ResearchPoints)));
        }

        report.AppendLine();
        report.AppendLine(Loc.GetString(recipe.Disk != null ? "dh-material-scanner-report-disk" : "dh-material-scanner-report-no-disk"));

        var paper = Spawn(ent.Comp.PaperPrototype, coords);
        _metaData.SetEntityName(paper, Loc.GetString("dh-material-scanner-report-name", ("material", name)));
        if (TryComp<PaperComponent>(paper, out var paperComp))
            _paper.SetContent((paper, paperComp), report.ToString());

        _audio.PlayPvs(PrintSound, ent);
    }

    /// <summary>
    /// Реверс-инжиниринг трофея: скидки на связанные технологии на сервере РнД станции, очки за первый трофей этого
    /// вида за раунд, отчёт. Трофей остаётся в сканере — его можно вынуть.
    /// </summary>
    /// <summary>Данные реверс-инжиниринга предмета: компонент или прототип dhReverseEngineering по прототипу предмета.</summary>
    private (List<ProtoId<TechnologyPrototype>> Technologies, float Discount, int Points)? GetReverseData(EntityUid item)
    {
        if (TryComp<DHReverseEngineerableComponent>(item, out var comp))
            return (comp.Technologies, comp.Discount, comp.ResearchPoints);

        if (MetaData(item).EntityPrototype?.ID is not { } id)
            return null;

        foreach (var proto in _proto.EnumeratePrototypes<DHReverseEngineeringPrototype>())
        {
            if (proto.Items.Contains(id))
                return (proto.Technologies, proto.Discount, proto.ResearchPoints);
        }

        return null;
    }

    private void ReverseEngineer(Entity<DHMaterialScannerComponent> ent, EntityUid trophy,
        List<ProtoId<TechnologyPrototype>> technologies, float discount, int points)
    {
        var name = Name(trophy);
        var report = new StringBuilder();
        report.AppendLine(Loc.GetString("dh-reverse-report-header", ("item", name)));
        report.AppendLine();

        var server = FindServer(ent);
        var protoId = MetaData(trophy).EntityPrototype?.ID ?? name;
        var first = _scanned.Add("reverse:" + protoId);

        if (server == null)
        {
            report.AppendLine(Loc.GetString("dh-reverse-report-no-server"));
        }
        else if (!first)
        {
            report.AppendLine(Loc.GetString("dh-reverse-report-repeat"));
        }
        else
        {
            foreach (var tech in technologies)
            {
                if (!_proto.TryIndex(tech, out var techProto))
                    continue;

                var total = _research.AddTechnologyDiscount(server.Value, tech, discount);
                report.AppendLine(Loc.GetString("dh-reverse-report-discount",
                    ("technology", Loc.GetString(techProto.Name)), ("discount", (int) MathF.Round(total * 100f))));
            }

            if (points > 0)
            {
                _research.ModifyServerPoints(server.Value, points);
                report.AppendLine(Loc.GetString("dh-material-scanner-report-points", ("points", points)));
            }
        }

        var paper = Spawn(ent.Comp.PaperPrototype, Transform(ent).Coordinates);
        _metaData.SetEntityName(paper, Loc.GetString("dh-reverse-report-name", ("item", name)));
        if (TryComp<PaperComponent>(paper, out var paperComp))
            _paper.SetContent((paper, paperComp), report.ToString());

        _audio.PlayPvs(PrintSound, ent);
    }

    private EntityUid? FindServer(EntityUid machine)
    {
        var station = _station.GetOwningStation(machine);
        var query = EntityQueryEnumerator<ResearchServerComponent>();
        while (query.MoveNext(out var server, out _))
        {
            if (station != null && _station.GetOwningStation(server) != station)
                continue;

            return server;
        }

        return null;
    }

    private void AwardPoints(EntityUid scanner, int points)
    {
        var station = _station.GetOwningStation(scanner);
        var query = EntityQueryEnumerator<ResearchServerComponent>();
        while (query.MoveNext(out var server, out var serverComp))
        {
            if (station != null && _station.GetOwningStation(server) != station)
                continue;

            _research.ModifyServerPoints(server, points, serverComp);
            return;
        }
    }

    #endregion
}
