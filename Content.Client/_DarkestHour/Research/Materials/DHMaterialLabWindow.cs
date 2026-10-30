using Content.Client._DarkestHour.UI;
using System.Numerics;
using Content.Shared._DarkestHour.Research.Materials;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Research.Materials;

/// <summary>
/// Материаловедческий лабораторный стол: выбрать материал, тип решётки, атомы в узлах и электроны на внешней
/// оболочке; «Собрать» — стол проверяет сборку и даёт подсказку. Собрано в коде, без XAML.
/// </summary>
public sealed class DHMaterialLabWindow : DefaultWindow
{
    public event Action<string, DHLatticeType, List<DHElement>, List<int>>? OnAttempt;

    private const int MaxNodes = 6;

    private static readonly DHLatticeType[] Lattices =
        { DHLatticeType.Cubic, DHLatticeType.Hexagonal, DHLatticeType.Tetragonal, DHLatticeType.Amorphous, DHLatticeType.Tesseract };

    private static readonly DHElement[] Elements =
    {
        DHElement.None, DHElement.Fe, DHElement.C, DHElement.H, DHElement.Al, DHElement.Ti, DHElement.Cu,
        DHElement.Ni, DHElement.Si, DHElement.U, DHElement.Chr,
    };

    private readonly OptionButton _recipe = new() { HorizontalExpand = true };
    private readonly OptionButton _lattice = new() { HorizontalExpand = true };
    private readonly Label _consumes = new() { FontColorOverride = Color.LightGray };
    private readonly Label _materials = new() { FontColorOverride = Color.LightGray };
    private readonly RichTextLabel _result = new() { HorizontalExpand = true };
    private readonly List<(OptionButton Element, OptionButton Electrons, BoxContainer Row)> _nodes = new();

    private List<DHMaterialRecipeEntry> _recipes = new();
    private string? _selected;

    public DHMaterialLabWindow()
    {
        DHFonts.RegisterTerminal(this); // DarkestHour: терминальный шрифт
        Title = Loc.GetString("dh-material-lab-ui-title");
        MinSize = new Vector2(460, 520);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        root.AddChild(new Label { Text = Loc.GetString("dh-material-lab-ui-hint"), FontColorOverride = Color.Gray });

        root.AddChild(Heading("dh-material-lab-ui-material"));
        _recipe.OnItemSelected += args =>
        {
            _recipe.SelectId(args.Id);
            if (args.Id >= 0 && args.Id < _recipes.Count)
                _selected = _recipes[args.Id].Id;
            RefreshRecipe();
        };
        root.AddChild(_recipe);
        root.AddChild(_consumes);

        root.AddChild(Heading("dh-material-lab-ui-lattice"));
        foreach (var lattice in Lattices)
        {
            _lattice.AddItem(Loc.GetString($"dh-lattice-{lattice}"), (int) lattice);
        }
        _lattice.OnItemSelected += args => _lattice.SelectId(args.Id);
        root.AddChild(_lattice);

        root.AddChild(Heading("dh-material-lab-ui-nodes"));
        for (var i = 0; i < MaxNodes; i++)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
            row.AddChild(new Label { Text = Loc.GetString("dh-material-lab-ui-node", ("index", i + 1)), MinWidth = 70 });

            var element = new OptionButton { HorizontalExpand = true };
            foreach (var e in Elements)
            {
                element.AddItem(Loc.GetString($"dh-element-{e}"), (int) e);
            }
            element.OnItemSelected += args => element.SelectId(args.Id);
            row.AddChild(element);

            row.AddChild(new Label { Text = Loc.GetString("dh-material-lab-ui-electrons") });
            var electrons = new OptionButton { MinWidth = 50 };
            for (var n = 0; n <= 8; n++)
            {
                electrons.AddItem(n.ToString(), n);
            }
            electrons.OnItemSelected += args => electrons.SelectId(args.Id);
            row.AddChild(electrons);

            _nodes.Add((element, electrons, row));
            root.AddChild(row);
        }

        var attempt = new Button { Text = Loc.GetString("dh-material-lab-ui-attempt") };
        attempt.OnPressed += _ => Attempt();
        root.AddChild(attempt);

        root.AddChild(_result);
        root.AddChild(Heading("dh-material-lab-ui-stored"));
        root.AddChild(_materials);

        Contents.AddChild(new ScrollContainer { HScrollEnabled = false, Children = { root } });
    }

    private static Label Heading(string loc)
    {
        return new Label { Text = Loc.GetString(loc), StyleClasses = { "LabelHeading" } };
    }

    private void Attempt()
    {
        if (_selected == null)
            return;

        var elements = new List<DHElement>();
        var electrons = new List<int>();
        foreach (var (element, count, row) in _nodes)
        {
            if (!row.Visible)
                continue;

            elements.Add((DHElement) element.SelectedId);
            electrons.Add(count.SelectedId);
        }

        OnAttempt?.Invoke(_selected, (DHLatticeType) _lattice.SelectedId, elements, electrons);
    }

    private void RefreshRecipe()
    {
        var entry = _recipes.Find(r => r.Id == _selected);
        var nodes = entry?.Nodes ?? 0;
        for (var i = 0; i < _nodes.Count; i++)
        {
            _nodes[i].Row.Visible = i < nodes;
        }

        _consumes.Text = entry == null
            ? string.Empty
            : Loc.GetString("dh-material-lab-ui-consumes", ("materials", entry.Consumes));
        _consumes.FontColorOverride = entry is { CanAfford: false } ? Color.IndianRed : Color.LightGray;
    }

    public void UpdateState(DHMaterialLabState state)
    {
        _recipes = state.Recipes;

        _recipe.Clear();
        for (var i = 0; i < _recipes.Count; i++)
        {
            _recipe.AddItem(_recipes[i].Name, i);
        }

        var index = _recipes.FindIndex(r => r.Id == _selected);
        if (index < 0 && _recipes.Count > 0)
        {
            index = 0;
            _selected = _recipes[0].Id;
        }

        if (index >= 0)
            _recipe.SelectId(index);

        RefreshRecipe();

        _materials.Text = state.Materials;

        if (state.LastResult != null)
        {
            var color = state.LastSuccess ? "#7fd68a" : "#e0a040";
            _result.SetMessage(Robust.Shared.Utility.FormattedMessage.FromMarkupPermissive($"[color={color}]{state.LastResult}[/color]"));
        }
    }
}
