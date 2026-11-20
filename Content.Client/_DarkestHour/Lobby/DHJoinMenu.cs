using System.Linq;
using System.Numerics;
using Content.Client.GameTicking;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client.Console;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._DarkestHour.Lobby;

/// <summary>
/// Меню входа в идущий раунд (план осады п. 23, решения автора 2026-11-18 и 2026-11-20) — заменяет старое позднее
/// присоединение: «Присоединиться» в лобби открывает его. Слева — категории цветом стороны (Империя — жёлтая,
/// Республика — зелёная; ЗГА, Федерация Солара и ОНК — заготовки «скоро»; «Другие» — роли призраков для событий),
/// справа — должности выбранной стороны со свободными местами. Наверху — режим, карта, время раунда, число игроков и
/// «Наблюдать». Вход — та же команда <c>joingame</c>, что и раньше.
/// </summary>
public sealed partial class DHJoinMenu : DefaultWindow
{
    [Dependency] private IClientConsoleHost _console = default!;
    [Dependency] private IEntitySystemManager _systems = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private JobRequirementsManager _jobRequirements = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IClientPreferencesManager _preferences = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private const string EmpireSide = "dh-jobs-side-empire";
    private const string RepublicSide = "dh-jobs-side-republic";

    /// <summary>Категории в меню: сторона (вкладка отдела <c>dhSide</c>) или заготовка.</summary>
    private sealed record Category(string Key, string Name, Color Color, bool Placeholder, string? Hint);

    private static readonly Category[] KnownCategories =
    [
        new(EmpireSide, "dh-join-category-empire", Color.FromHex("#F2C230"), false, "dh-join-hint-empire"),
        new(RepublicSide, "dh-join-category-republic", Color.FromHex("#7FAE46"), false, "dh-join-hint-republic"),
        new("dh-join-category-zga", "dh-join-category-zga", Color.FromHex("#8FCBE6"), true, null),
        new("dh-join-category-solar", "dh-join-category-solar", Color.FromHex("#4F7FD6"), true, null),
        new("dh-join-category-onk", "dh-join-category-onk", Color.FromHex("#A8A8A8"), true, null),
    ];

    private static readonly Color OthersColor = Color.FromHex("#F0F0F0");
    private static readonly Color Dim = Color.FromHex("#9C9384");

    private readonly ClientGameTicker _ticker;
    private readonly DHLobbyInfoSystem _info;
    private readonly Content.Client._DarkestHour.ThirdParty.DHThirdPartyClientSystem _thirdParty;
    private readonly SpriteSystem _sprites;

    private readonly Label _mode = new();
    private readonly Label _map = new();
    private readonly Label _time = new();
    private readonly Label _players = new();
    private readonly BoxContainer _categories = new() { Orientation = LayoutOrientation.Vertical, SeparationOverride = 8 };
    private readonly BoxContainer _jobs = new() { Orientation = LayoutOrientation.Vertical, SeparationOverride = 6 };
    private readonly Label _title = new() { StyleClasses = { "LabelHeading" } };
    private readonly RichTextLabel _hint = new();

    private string? _selected;

    public DHJoinMenu()
    {
        IoCManager.InjectDependencies(this);
        _ticker = _systems.GetEntitySystem<ClientGameTicker>();
        _info = _systems.GetEntitySystem<DHLobbyInfoSystem>();
        _thirdParty = _systems.GetEntitySystem<Content.Client._DarkestHour.ThirdParty.DHThirdPartyClientSystem>();
        _sprites = _systems.GetEntitySystem<SpriteSystem>();

        Title = Loc.GetString("dh-join-title");
        MinSize = SetSize = new Vector2(1100, 680);

        var observe = new Button { Text = Loc.GetString("ui-lobby-observe-button"), MinSize = new Vector2(0, 40) };
        observe.OnPressed += _ =>
        {
            new ObserveWarningWindow().OpenCentered();
            Close();
        };

        var infoRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 24,
            Margin = new Thickness(4, 0, 4, 8),
        };
        infoRow.AddChild(_mode);
        infoRow.AddChild(_map);
        infoRow.AddChild(_time);
        infoRow.AddChild(_players);
        infoRow.AddChild(new Control { HorizontalExpand = true });
        infoRow.AddChild(observe);

        var left = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            MinWidth = 330,
            Margin = new Thickness(0, 0, 12, 0),
        };
        left.AddChild(_categories);
        left.AddChild(new Control { VerticalExpand = true });
        left.AddChild(new Label
        {
            Text = Loc.GetString("dh-join-others-note"),
            FontColorOverride = Dim,
            Autowrap = true,
            MaxWidth = 330,
        });

        var right = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 8,
        };
        right.AddChild(_title);
        right.AddChild(_hint);
        right.AddChild(new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { _jobs },
        });

        var body = new BoxContainer { Orientation = LayoutOrientation.Horizontal, VerticalExpand = true };
        body.AddChild(left);
        body.AddChild(right);

        var root = new BoxContainer { Orientation = LayoutOrientation.Vertical, VerticalExpand = true };
        root.AddChild(infoRow);
        root.AddChild(body);
        ContentsContainer.AddChild(root);

        _jobRequirements.Updated += Rebuild;
        _ticker.LobbyJobsAvailableUpdated += OnJobsUpdated;
        _info.Updated += UpdateInfo;
        _thirdParty.Updated += Rebuild;

        _selected = PreferredSide();
        Rebuild();
        UpdateInfo();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var duration = _ticker.IsGameStarted ? _timing.CurTime - _ticker.RoundStartTimeSpan : TimeSpan.Zero;
        _time.Text = Loc.GetString("dh-join-info-time", ("time", $"{(int) duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}"));
        _players.Text = Loc.GetString("dh-join-info-players", ("count", _player.PlayerCount));
    }

    private void UpdateInfo()
    {
        _mode.Text = Loc.GetString("dh-join-info-mode", ("mode", _info.Mode.Length > 0 ? _info.Mode : "—"));
        _map.Text = Loc.GetString("dh-join-info-map", ("map", _info.Map.Length > 0 ? _info.Map : "—"));
    }

    private void OnJobsUpdated(IReadOnlyDictionary<NetEntity, Dictionary<ProtoId<JobPrototype>, int?>> _)
    {
        Rebuild();
    }

    /// <summary>Сторона должности с высоким приоритетом у выбранного персонажа — она открыта первой.</summary>
    private string? PreferredSide()
    {
        if (_preferences.Preferences?.SelectedCharacter is not HumanoidCharacterProfile profile)
            return null;

        var job = profile.JobPriorities
            .Where(p => p.Value != JobPriority.Never)
            .OrderByDescending(p => p.Value)
            .Select(p => p.Key)
            .FirstOrDefault();

        return string.IsNullOrEmpty(job.Id) ? null : SideOf(job);
    }

    private string? SideOf(ProtoId<JobPrototype> job)
    {
        foreach (var department in _proto.EnumeratePrototypes<DepartmentPrototype>())
        {
            if (department.Roles.Contains(job))
                return department.DHSide?.Id ?? department.ID;
        }

        return null;
    }

    /// <summary>Свободные должности по сторонам: сторона → (станция, отдел, должность, мест).</summary>
    private Dictionary<string, List<(NetEntity Station, DepartmentPrototype Department, JobPrototype Job, int? Slots)>>
        CollectJobs()
    {
        var result = new Dictionary<string, List<(NetEntity, DepartmentPrototype, JobPrototype, int?)>>();
        var departments = _proto.EnumeratePrototypes<DepartmentPrototype>().ToArray();
        Array.Sort(departments, DepartmentUIComparer.Instance);

        foreach (var (station, available) in _ticker.JobsAvailable)
        {
            foreach (var department in departments)
            {
                var side = department.DHSide?.Id ?? department.ID;
                var jobs = new List<JobPrototype>();
                foreach (var jobId in department.Roles)
                {
                    if (available.ContainsKey(jobId) && _proto.TryIndex(jobId, out var job))
                        jobs.Add(job);
                }

                if (jobs.Count == 0)
                    continue;

                if (JobUIComparer.TryCreate(_proto, _ticker.JobWeightsByStation.GetValueOrDefault(station),
                        out var comparer))
                {
                    jobs.Sort(comparer);
                }

                if (!result.TryGetValue(side, out var list))
                    result[side] = list = new();

                foreach (var job in jobs)
                {
                    list.Add((station, department, job, available[job.ID]));
                }
            }
        }

        return result;
    }

    private void Rebuild()
    {
        var bySide = CollectJobs();

        // Стороны, которые сейчас есть в раунде, но не известны меню (например, «Восстание»), — после известных.
        var categories = KnownCategories.ToList();
        foreach (var side in bySide.Keys)
        {
            if (categories.All(c => c.Key != side))
                categories.Add(new Category(side, side, OthersColor, false, null));
        }

        if (_selected == null || !bySide.ContainsKey(_selected))
            _selected = categories.FirstOrDefault(c => bySide.ContainsKey(c.Key))?.Key;

        _categories.RemoveAllChildren();
        foreach (var category in categories)
        {
            var open = !category.Placeholder && bySide.ContainsKey(category.Key);
            var note = category.Placeholder
                ? Loc.GetString("dh-join-soon")
                : open
                    ? FreeSlots(bySide[category.Key])
                    : Loc.GetString("dh-join-closed");
            _categories.AddChild(CategoryButton(category, note, open, category.Key == _selected));
        }

        // «Другие» — наборы событий третьей стороны (ассоциативный десантник, ЧВК LA-V): открывает окно записи.
        // Записаться можно только наблюдателем — в окне это написано.
        var events = _thirdParty.Events.Count;
        var others = CategoryButton(new Category("others", "dh-join-category-others", OthersColor, events == 0, null),
            events == 0 ? Loc.GetString("dh-join-no-events") : Loc.GetString("dh-join-events", ("count", events)),
            events > 0, false);
        if (events > 0)
            ((ContainerButton) others).OnPressed += _ => _thirdParty.OpenWindow();
        _categories.AddChild(others);

        BuildJobs(_selected, categories.FirstOrDefault(c => c.Key == _selected),
            _selected != null ? bySide.GetValueOrDefault(_selected) : null);
    }

    private string FreeSlots(List<(NetEntity Station, DepartmentPrototype Department, JobPrototype Job, int? Slots)> jobs)
    {
        if (jobs.Any(j => j.Slots == null))
            return Loc.GetString("dh-join-free-unlimited");

        return Loc.GetString("dh-join-free", ("count", jobs.Sum(j => j.Slots ?? 0)));
    }

    private Control CategoryButton(Category category, string note, bool enabled, bool selected)
    {
        var button = new ContainerButton
        {
            MinSize = new Vector2(0, category.Placeholder ? 52 : 64),
            Disabled = !enabled,
            ToggleMode = false,
        };

        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = category.Color.WithAlpha(selected ? 0.22f : 0.07f),
                BorderColor = category.Color.WithAlpha(enabled ? 1f : 0.45f),
                BorderThickness = new Thickness(selected ? 2 : 1),
            },
        };

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(14, 0, 14, 0),
        };
        row.AddChild(new Label
        {
            Text = Loc.GetString(category.Name),
            FontColorOverride = category.Color.WithAlpha(enabled || category.Placeholder ? 1f : 0.6f),
            StyleClasses = { category.Placeholder ? "LabelHeading" : "LabelBig" },
            HorizontalExpand = true,
            VAlign = Label.VAlignMode.Center,
            ClipText = true,
        });
        row.AddChild(new Label
        {
            Text = note,
            FontColorOverride = Dim,
            VAlign = Label.VAlignMode.Center,
        });
        panel.AddChild(row);
        button.AddChild(panel);

        var key = category.Key;
        button.OnPressed += _ =>
        {
            _selected = key;
            Rebuild();
        };

        return button;
    }

    private void BuildJobs(string? side, Category? category,
        List<(NetEntity Station, DepartmentPrototype Department, JobPrototype Job, int? Slots)>? jobs)
    {
        _jobs.RemoveAllChildren();
        _title.Text = category != null ? Loc.GetString(category.Name) : string.Empty;
        _title.FontColorOverride = category?.Color ?? Color.White;
        _hint.Visible = category?.Hint != null;
        if (category?.Hint is { } hint)
            _hint.SetMarkup(Loc.GetString(hint));

        if (side == null || jobs == null || jobs.Count == 0)
        {
            _jobs.AddChild(new Label { Text = Loc.GetString("dh-join-nothing"), FontColorOverride = Dim });
            return;
        }

        var profile = _preferences.Preferences?.SelectedCharacter as HumanoidCharacterProfile;
        string? lastDepartment = null;
        foreach (var (station, department, job, slots) in jobs)
        {
            if (department.ID != lastDepartment)
            {
                lastDepartment = department.ID;
                _jobs.AddChild(new Label
                {
                    Text = Loc.GetString(department.Name).ToUpperInvariant(),
                    FontColorOverride = Dim,
                    Margin = new Thickness(0, _jobs.ChildCount == 0 ? 0 : 10, 0, 2),
                });
            }

            _jobs.AddChild(JobRow(station, job, slots, profile, category!.Color));
        }
    }

    private Control JobRow(NetEntity station, JobPrototype job, int? slots, HumanoidCharacterProfile? profile,
        Color color)
    {
        var allowed = _jobRequirements.IsAllowed(job, profile, out var reason);
        var high = profile != null && profile.JobPriorities.TryGetValue(job.ID, out var priority) &&
                   priority == JobPriority.High;

        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = high ? color.WithAlpha(0.12f) : Color.FromHex("#171410"),
                BorderColor = high ? color : Color.FromHex("#2F281C"),
                BorderThickness = new Thickness(1),
            },
            MinSize = new Vector2(0, 44),
        };

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Margin = new Thickness(10, 0, 10, 0),
        };

        if (_proto.TryIndex(job.Icon, out var icon))
        {
            row.AddChild(new TextureRect
            {
                Texture = _sprites.Frame0(icon.Icon),
                TextureScale = new Vector2(2, 2),
                VerticalAlignment = VAlignment.Center,
            });
        }

        var name = new BoxContainer { Orientation = LayoutOrientation.Horizontal, HorizontalExpand = true };
        name.AddChild(new Label { Text = job.LocalizedName, VAlign = Label.VAlignMode.Center });
        if (high)
        {
            name.AddChild(new Label
            {
                Text = Loc.GetString("dh-join-your-priority"),
                FontColorOverride = color,
                VAlign = Label.VAlignMode.Center,
                Margin = new Thickness(8, 0, 0, 0),
            });
        }

        row.AddChild(name);
        row.AddChild(new Label
        {
            Text = slots == null ? "∞" : Loc.GetString("dh-join-slots", ("count", slots.Value)),
            FontColorOverride = Dim,
            VAlign = Label.VAlignMode.Center,
            MinWidth = 70,
        });

        var join = new Button
        {
            Text = Loc.GetString(!allowed ? "dh-join-locked" : slots == 0 ? "dh-join-full" : "dh-join-enter"),
            Disabled = !allowed || slots == 0,
            MinSize = new Vector2(110, 32),
            VerticalAlignment = VAlignment.Center,
        };

        if (!allowed && !reason.IsEmpty)
        {
            var tooltip = new Tooltip();
            tooltip.SetMessage(reason);
            join.TooltipSupplier = _ => tooltip;
        }

        var jobId = job.ID;
        join.OnPressed += _ =>
        {
            _console.ExecuteCommand($"joingame {CommandParsing.Escape(jobId)} {station}");
            Close();
        };
        row.AddChild(join);

        panel.AddChild(row);
        return panel;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _jobRequirements.Updated -= Rebuild;
        _ticker.LobbyJobsAvailableUpdated -= OnJobsUpdated;
        _info.Updated -= UpdateInfo;
        _thirdParty.Updated -= Rebuild;
    }
}
