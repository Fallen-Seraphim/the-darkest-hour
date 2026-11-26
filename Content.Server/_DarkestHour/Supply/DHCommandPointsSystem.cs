using System.Linq;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.ControlPoints;
using Content.Shared._DarkestHour.Supply;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Supply;

/// <summary>
/// Очки командования стороны (план осады п. 3, решения автора 2026-11-19):
/// <list type="bullet">
/// <item>в начале раунда — <see cref="DHSupplyCatalogPrototype.StartPoints"/> из каталога стороны;</item>
/// <item>за удержание POI — <see cref="DHControlPointComponent.PointsPerMinute"/> каждой точки;</item>
/// <item>за первый захват точки стороной — <see cref="DHControlPointComponent.CaptureBonus"/> (повторный захват той
/// же точки и точка, которая была у стороны с начала, бонуса не дают);</item>
/// <item>тратятся на пополнение карго (компьютер снабжения) и узлы древа (компьютер штаба, только командование).</item>
/// </list>
/// </summary>
public sealed partial class DHCommandPointsSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DHSupplySystem _supply = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly TimeSpan AccrueInterval = TimeSpan.FromSeconds(5);

    /// <summary>Очки сторон (дробные: доход идёт мелкими порциями).</summary>
    private readonly Dictionary<DHAssaultFaction, float> _points = new();

    /// <summary>Открытые узлы древа.</summary>
    private readonly HashSet<string> _opened = new();

    /// <summary>Какие точки сторона уже брала (или держала с начала): бонус за захват — один раз.</summary>
    private readonly HashSet<(DHAssaultFaction, string)> _captured = new();

    private TimeSpan _nextAccrue;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<DHCommandTreeConsoleComponent, BoundUIOpenedEvent>(OnTreeOpened);
        SubscribeLocalEvent<DHCommandTreeConsoleComponent, DHCommandTreeUnlockMessage>(OnUnlock);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _points.Clear();
        _opened.Clear();
        _captured.Clear();
    }

    #region Очки

    public int Points(DHAssaultFaction faction)
    {
        return (int) MathF.Floor(PointsExact(faction));
    }

    private float PointsExact(DHAssaultFaction faction)
    {
        if (_points.TryGetValue(faction, out var points))
            return points;

        points = _supply.CatalogOf(faction)?.StartPoints ?? 0;
        _points[faction] = points;
        return points;
    }

    public void Add(DHAssaultFaction faction, float amount)
    {
        _points[faction] = PointsExact(faction) + amount;
    }

    /// <summary>Списать очки, если хватает.</summary>
    public bool TrySpend(DHAssaultFaction faction, int cost)
    {
        var points = PointsExact(faction);
        if (points < cost)
            return false;

        _points[faction] = points - cost;
        UpdateAll();
        return true;
    }

    /// <summary>Очков в минуту за удерживаемые точки.</summary>
    public int Income(DHAssaultFaction faction)
    {
        var income = 0;
        var query = EntityQueryEnumerator<DHControlPointComponent>();
        while (query.MoveNext(out _, out var point))
        {
            if (point.Holder == faction && !point.Paused)
                income += point.PointsPerMinute;
        }

        return income;
    }

    /// <summary>Точка перешла к стороне (захват). Первый раз — бонус.</summary>
    public void Captured(DHControlPointComponent point, DHAssaultFaction faction)
    {
        if (!_captured.Add((faction, point.PointId)) || point.CaptureBonus <= 0)
            return;

        Add(faction, point.CaptureBonus);
        UpdateAll();
    }

    /// <summary>Точка у стороны с начала раунда: за неё бонуса не будет.</summary>
    public void HeldFromStart(DHControlPointComponent point, DHAssaultFaction faction)
    {
        _captured.Add((faction, point.PointId));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextAccrue)
            return;

        _nextAccrue = now + AccrueInterval;
        var minutes = (float) AccrueInterval.TotalMinutes;
        var any = false;
        var query = EntityQueryEnumerator<DHControlPointComponent>();
        while (query.MoveNext(out _, out var point))
        {
            // Оспоренная (флаги обеих сторон) или заглушенная флагом LA-V точка очков не даёт.
            if (point.Holder is not { } holder || point.PointsPerMinute <= 0 || point.Paused)
                continue;

            Add(holder, point.PointsPerMinute * minutes);
            any = true;
        }

        if (any)
            UpdateAll();
    }

    private void UpdateAll()
    {
        var trees = EntityQueryEnumerator<DHCommandTreeConsoleComponent>();
        while (trees.MoveNext(out var uid, out var tree))
        {
            UpdateTree((uid, tree));
        }

        _supply.UpdateAllConsoles();
    }

    #endregion

    #region Древо

    public bool IsOpened(string node)
    {
        return _opened.Contains(node);
    }

    /// <summary>Открытые узлы стороны.</summary>
    public IEnumerable<DHSupplyNodePrototype> OpenedNodes(DHAssaultFaction faction)
    {
        foreach (var node in _proto.EnumeratePrototypes<DHSupplyNodePrototype>())
        {
            if (node.Faction == faction && _opened.Contains(node.ID))
                yield return node;
        }
    }

    /// <summary>Позиция каталога закрыта узлом, который ещё не открыт.</summary>
    public bool EntryLocked(DHAssaultFaction faction, string entry)
    {
        foreach (var node in _proto.EnumeratePrototypes<DHSupplyNodePrototype>())
        {
            if (node.Faction == faction && node.Unlocks.Contains(entry) && !_opened.Contains(node.ID))
                return true;
        }

        return false;
    }

    private void OnTreeOpened(Entity<DHCommandTreeConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateTree(ent);
    }

    private void OnUnlock(Entity<DHCommandTreeConsoleComponent> ent, ref DHCommandTreeUnlockMessage args)
    {
        if (!_proto.TryIndex<DHSupplyNodePrototype>(args.Node, out var node) || node.Faction != ent.Comp.Faction ||
            _opened.Contains(node.ID))
        {
            return;
        }

        if (node.Requires.Any(r => !_opened.Contains(r)))
        {
            _popup.PopupEntity(Loc.GetString("dh-command-tree-requires"), ent, args.Actor);
            return;
        }

        if (!TrySpend(node.Faction, node.Cost))
        {
            _popup.PopupEntity(Loc.GetString("dh-command-points-not-enough", ("cost", node.Cost)), ent, args.Actor);
            return;
        }

        _opened.Add(node.ID);
        _supply.NodeOpened(node);
        _popup.PopupEntity(Loc.GetString("dh-command-tree-opened", ("node", Loc.GetString(node.Name))), ent,
            args.Actor);
        UpdateAll();
    }

    private void UpdateTree(Entity<DHCommandTreeConsoleComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, DHCommandTreeUiKey.Key))
            return;

        var faction = ent.Comp.Faction;
        var rows = new List<DHCommandTreeNodeRow>();
        foreach (var node in _proto.EnumeratePrototypes<DHSupplyNodePrototype>()
                     .Where(n => n.Faction == faction)
                     .OrderBy(n => n.Branch.Id)
                     .ThenBy(n => n.Tier)
                     .ThenBy(n => n.ID))
        {
            var state = _opened.Contains(node.ID) ? DHCommandTreeNodeState.Opened
                : node.Requires.All(r => _opened.Contains(r)) ? DHCommandTreeNodeState.Available
                : DHCommandTreeNodeState.Locked;

            var requires = new List<string>();
            foreach (var required in node.Requires)
            {
                if (_proto.TryIndex(required, out var requiredNode))
                    requires.Add(requiredNode.Name);
            }

            var unlocks = new List<string>();
            if (_supply.CatalogOf(faction) is { } catalog)
            {
                foreach (var entry in catalog.Entries)
                {
                    if (node.Unlocks.Contains(entry.Id))
                        unlocks.Add(entry.Proto);
                }
            }

            rows.Add(new DHCommandTreeNodeRow(node.ID, node.Branch, node.Name, node.Description?.Id, node.Tier, node.Cost,
                state, requires, unlocks));
        }

        _ui.SetUiState(ent.Owner, DHCommandTreeUiKey.Key,
            new DHCommandTreeState(Points(faction), Income(faction), rows));
    }

    #endregion
}
