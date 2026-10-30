using Content.Client._DarkestHour.UI;
using System.Linq;
using System.Numerics;
using Content.Shared._DarkestHour.Investigation;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Investigation;

/// <summary>
/// Архив наблюдения: журнал камер и датчиков экипажа (новые записи сверху), фильтр по источнику и поиск по тексту.
/// Собрано в коде, без XAML.
/// </summary>
public sealed class DHArchiveWindow : DefaultWindow
{
    public event Action? OnRefresh;

    private readonly OptionButton _kind = new() { MinWidth = 160 };
    private readonly LineEdit _search = new() { HorizontalExpand = true };
    private readonly BoxContainer _list = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
    private readonly Label _count = new() { FontColorOverride = Color.Gray };

    private List<DHArchiveEntry> _entries = new();

    public DHArchiveWindow()
    {
        DHFonts.RegisterTerminal(this); // DarkestHour: терминальный шрифт
        Title = Loc.GetString("dh-archive-ui-title");
        MinSize = new Vector2(560, 480);

        _kind.AddItem(Loc.GetString("dh-archive-ui-all"), 0);
        _kind.AddItem(Loc.GetString("dh-archive-ui-cameras"), 1);
        _kind.AddItem(Loc.GetString("dh-archive-ui-sensors"), 2);
        _kind.OnItemSelected += args =>
        {
            _kind.SelectId(args.Id);
            Rebuild();
        };

        _search.PlaceHolder = Loc.GetString("dh-archive-ui-search");
        _search.OnTextChanged += _ => Rebuild();

        var refresh = new Button { Text = Loc.GetString("dh-archive-ui-refresh") };
        refresh.OnPressed += _ => OnRefresh?.Invoke();

        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        top.AddChild(_kind);
        top.AddChild(_search);
        top.AddChild(refresh);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };
        root.AddChild(top);
        root.AddChild(_count);
        root.AddChild(new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { _list },
        });

        Contents.AddChild(root);
    }

    public void UpdateState(DHArchiveBoundUserInterfaceState state)
    {
        _entries = state.Entries;
        Rebuild();
    }

    private void Rebuild()
    {
        _list.RemoveAllChildren();

        var search = _search.Text.Trim();
        var shown = _entries
            .Where(e => _kind.SelectedId switch
            {
                1 => e.Kind == DHArchiveKind.Camera,
                2 => e.Kind == DHArchiveKind.Sensors,
                _ => true,
            })
            .Where(e => search.Length == 0 ||
                        e.Text.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        e.Source.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Reverse()
            .ToList();

        _count.Text = Loc.GetString("dh-archive-ui-count", ("count", shown.Count));

        foreach (var entry in shown)
        {
            var time = $"{(int) entry.Time.TotalHours:00}:{entry.Time.Minutes:00}:{entry.Time.Seconds:00}";
            _list.AddChild(new Label
            {
                Text = $"[{time}] {entry.Source}: {entry.Text}",
                ClipText = true,
                FontColorOverride = entry.Kind == DHArchiveKind.Sensors ? Color.FromHex("#e0a060") : null,
            });
        }
    }
}
