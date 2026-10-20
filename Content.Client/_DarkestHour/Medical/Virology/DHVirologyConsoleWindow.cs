using System.Linq;
using System.Numerics;
using Content.Shared._DarkestHour.Medical.Disease;
using Content.Shared._DarkestHour.Medical.Virology;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Medical.Virology;

/// <summary>
/// Компьютер вирусолога: карточка случая — группа возбудителя (по распечатке анализатора), путь передачи, симптомы
/// (по сканеру и осмотру), основное лечение. Собрано в коде, без XAML.
/// </summary>
public sealed class DHVirologyConsoleWindow : DefaultWindow
{
    public event Action<DHPathogenGroup, List<DHSpreadVector>, List<string>, DHTreatmentKind>? OnSubmit;

    private static readonly DHPathogenGroup[] Groups =
        { DHPathogenGroup.Virus, DHPathogenGroup.Bacteria, DHPathogenGroup.Fungus, DHPathogenGroup.Parasite };

    private readonly OptionButton _group = new() { HorizontalExpand = true };
    private readonly OptionButton _treatment = new() { HorizontalExpand = true };
    private readonly Dictionary<DHSpreadVector, CheckBox> _spread = new();
    private readonly GridContainer _symptoms = new() { Columns = 2, HorizontalExpand = true };
    private readonly Dictionary<string, CheckBox> _symptomBoxes = new();
    private readonly Label _result = new() { HorizontalExpand = true };
    private readonly Label _known = new() { FontColorOverride = Color.LightGray };
    private readonly Button _submit = new() { HorizontalExpand = true };

    private List<string> _symptomIds = new();

    public DHVirologyConsoleWindow()
    {
        Title = Loc.GetString("dh-virology-ui-title");
        MinSize = new Vector2(520, 560);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        root.AddChild(new Label { Text = Loc.GetString("dh-virology-ui-hint"), FontColorOverride = Color.Gray });

        root.AddChild(Heading("dh-virology-ui-group"));
        foreach (var group in Groups)
        {
            _group.AddItem(Loc.GetString($"dh-virology-group-{group}"), (int) group);
        }
        _group.OnItemSelected += args => _group.SelectId(args.Id);
        root.AddChild(_group);

        root.AddChild(Heading("dh-virology-ui-spread"));
        var spreadRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 12 };
        foreach (var vector in new[] { DHSpreadVector.Airborne, DHSpreadVector.Contact, DHSpreadVector.Blood })
        {
            var box = new CheckBox { Text = Loc.GetString($"dh-virology-spread-{vector}") };
            _spread[vector] = box;
            spreadRow.AddChild(box);
        }
        root.AddChild(spreadRow);

        root.AddChild(Heading("dh-virology-ui-symptoms"));
        root.AddChild(new ScrollContainer
        {
            MinHeight = 160,
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { _symptoms },
        });

        root.AddChild(Heading("dh-virology-ui-treatment"));
        foreach (var treatment in Enum.GetValues<DHTreatmentKind>())
        {
            _treatment.AddItem(Loc.GetString($"dh-virology-treatment-{treatment}"), (int) treatment);
        }
        _treatment.OnItemSelected += args => _treatment.SelectId(args.Id);
        root.AddChild(_treatment);

        _submit.Text = Loc.GetString("dh-virology-ui-submit");
        _submit.OnPressed += _ => Submit();
        root.AddChild(_submit);
        root.AddChild(_result);

        root.AddChild(Heading("dh-virology-ui-known"));
        root.AddChild(_known);

        Contents.AddChild(root);
    }

    private static Label Heading(string loc)
    {
        return new Label { Text = Loc.GetString(loc), StyleClasses = { "LabelSubHeading" } };
    }

    private void Submit()
    {
        var spread = _spread.Where(kv => kv.Value.Pressed).Select(kv => kv.Key).ToList();
        var symptoms = _symptomBoxes.Where(kv => kv.Value.Pressed).Select(kv => kv.Key).ToList();
        OnSubmit?.Invoke((DHPathogenGroup) _group.SelectedId, spread, symptoms, (DHTreatmentKind) _treatment.SelectedId);
    }

    public void UpdateState(DHVirologyConsoleState state)
    {
        var ids = state.Symptoms.Select(s => s.Id).ToList();
        if (!ids.SequenceEqual(_symptomIds))
        {
            _symptomIds = ids;
            _symptoms.RemoveAllChildren();
            _symptomBoxes.Clear();
            foreach (var symptom in state.Symptoms)
            {
                var box = new CheckBox { Text = symptom.Name, HorizontalExpand = true };
                _symptomBoxes[symptom.Id] = box;
                _symptoms.AddChild(box);
            }
        }

        _result.Text = state.LastResult ?? string.Empty;
        _result.FontColorOverride = state.LastSuccess ? Color.FromHex("#7fae6a") : Color.FromHex("#e08a4a");

        _known.Text = state.KnownDiseases.Count == 0
            ? Loc.GetString("dh-virology-ui-known-none")
            : string.Join(", ", state.KnownDiseases);

        if (state.CooldownSeconds > 0f)
        {
            _result.Text += "\n" + Loc.GetString("dh-virology-ui-cooldown", ("seconds", (int) MathF.Ceiling(state.CooldownSeconds)));
        }
    }
}
