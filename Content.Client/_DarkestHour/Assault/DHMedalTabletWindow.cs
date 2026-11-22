using System.Numerics;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Assault;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client._DarkestHour.Assault;

/// <summary>
/// Наградной лист: боец своей стороны, медаль, формулировка, «Наградить». Сколько наград осталось — вверху.
/// </summary>
public sealed partial class DHMedalTabletWindow : DefaultWindow
{
    [Dependency] private IPrototypeManager _proto = default!;

    /// <summary>Наградить: кого, какой медалью, формулировка.</summary>
    public event Action<NetEntity, string, string>? OnAward;

    private static readonly Color Warn = Color.FromHex("#E8B04A");
    private static readonly Color Dim = Color.FromHex("#8A8A8A");

    private readonly Label _remaining = new();
    private readonly ItemList _candidates = new() { VerticalExpand = true, SelectMode = ItemList.ItemListSelectMode.Single };
    private readonly OptionButton _medal = new();
    private readonly RichTextLabel _medalDescription = new();
    private readonly LineEdit _citation = new() { HorizontalExpand = true };
    private readonly Button _award = new() { Text = Loc.GetString("dh-medal-ui-award"), MinSize = new Vector2(0, 36) };

    private readonly List<NetEntity> _candidateIds = new();
    private readonly List<string> _medalIds = new();
    private int _remainingAwards;

    public DHMedalTabletWindow()
    {
        IoCManager.InjectDependencies(this);
        DHFonts.RegisterTerminal(this);
        Title = Loc.GetString("dh-medal-ui-title");
        MinSize = SetSize = new Vector2(520, 560);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        root.AddChild(_remaining);
        root.AddChild(new Label { Text = Loc.GetString("dh-medal-ui-recipient"), FontColorOverride = Warn });
        root.AddChild(_candidates);
        root.AddChild(new Label { Text = Loc.GetString("dh-medal-ui-medal"), FontColorOverride = Warn });
        root.AddChild(_medal);
        root.AddChild(_medalDescription);
        root.AddChild(new Label { Text = Loc.GetString("dh-medal-ui-citation"), FontColorOverride = Warn });
        root.AddChild(_citation);
        root.AddChild(_award);
        Contents.AddChild(root);

        _medal.OnItemSelected += args =>
        {
            _medal.SelectId(args.Id);
            UpdateDescription();
        };
        _candidates.OnItemSelected += _ => UpdateButton();
        _candidates.OnItemDeselected += _ => UpdateButton();
        _citation.OnTextChanged += _ => UpdateButton();
        _award.OnPressed += _ => Award();
    }

    public void UpdateState(DHMedalTabletState state)
    {
        _remainingAwards = state.Remaining;
        _remaining.Text = Loc.GetString("dh-medal-ui-remaining", ("count", state.Remaining));
        _remaining.FontColorOverride = state.Remaining > 0 ? null : Dim;
        _citation.IsValid = text => text.Length <= state.MaxCitation;

        var selected = SelectedCandidate();
        _candidates.Clear();
        _candidateIds.Clear();
        foreach (var candidate in state.Candidates)
        {
            var text = Loc.GetString(candidate.Dead ? "dh-medal-ui-candidate-dead" : "dh-medal-ui-candidate",
                ("name", candidate.Name), ("job", candidate.Job));
            var item = _candidates.AddItem(text);
            _candidateIds.Add(candidate.Entity);
            if (candidate.Entity == selected)
                item.Selected = true;
        }

        var selectedMedal = _medal.SelectedId;
        _medal.Clear();
        _medalIds.Clear();
        for (var i = 0; i < state.Medals.Count; i++)
        {
            var id = state.Medals[i];
            _medalIds.Add(id);
            _medal.AddItem(_proto.TryIndex<DHMedalPrototype>(id, out var medal) ? Loc.GetString(medal.Name) : id, i);
        }

        if (_medalIds.Count > 0)
            _medal.SelectId(Math.Clamp(selectedMedal, 0, _medalIds.Count - 1));

        UpdateDescription();
        UpdateButton();
    }

    private NetEntity? SelectedCandidate()
    {
        for (var i = 0; i < _candidates.Count && i < _candidateIds.Count; i++)
        {
            if (_candidates[i].Selected)
                return _candidateIds[i];
        }

        return null;
    }

    private void UpdateDescription()
    {
        var index = _medal.SelectedId;
        if (index >= 0 && index < _medalIds.Count && _proto.TryIndex<DHMedalPrototype>(_medalIds[index], out var medal))
            _medalDescription.SetMessage(Loc.GetString(medal.Description));
        else
            _medalDescription.SetMessage(string.Empty);
    }

    private void UpdateButton()
    {
        _award.Disabled = _remainingAwards <= 0 || SelectedCandidate() == null || _medalIds.Count == 0 ||
                          _citation.Text.Trim().Length == 0;
    }

    private void Award()
    {
        if (SelectedCandidate() is not { } target || _medal.SelectedId < 0 || _medal.SelectedId >= _medalIds.Count)
            return;

        OnAward?.Invoke(target, _medalIds[_medal.SelectedId], _citation.Text);
        _citation.Text = string.Empty;
        UpdateButton();
    }
}
