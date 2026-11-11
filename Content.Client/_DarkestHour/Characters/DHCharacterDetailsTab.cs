using System.Linq;
using Content.Shared._DarkestHour.Characters;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._DarkestHour.Characters;

/// <summary>
/// Вкладка редактора персонажа «Описание» (решение автора 2026-11-11, как в CMU): короткая строка при осмотре,
/// рост, вес, телосложение, место рождения (список зависит от года рождения = 2318 − возраст) и «документы» —
/// биография, медицинская карта, судимости.
/// </summary>
public sealed class DHCharacterDetailsTab : BoxContainer
{
    [Dependency] private IPrototypeManager _proto = default!;

    public event Action<DHCharacterDetails>? OnChanged;

    private readonly LineEdit _short;
    private readonly LineEdit _height;
    private readonly LineEdit _weight;
    private readonly OptionButton _build;
    private readonly OptionButton _birthplace;
    private readonly Label _birthYear;
    private readonly TextEdit _general;
    private readonly TextEdit _medical;
    private readonly TextEdit _criminal;

    private readonly List<DHBirthplacePrototype?> _birthplaceIds = new();
    private DHCharacterDetails _details = new();
    private int _age = 18;
    private bool _updating;

    public DHCharacterDetailsTab()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;

        var box = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 6, Margin = new(10) };

        _short = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("dh-details-short-placeholder") };
        box.AddChild(Field("dh-details-short", _short));

        _height = new LineEdit { MinWidth = 70, PlaceHolder = "175" };
        _weight = new LineEdit { MinWidth = 70, PlaceHolder = "75" };
        _build = new OptionButton { MinWidth = 160 };
        foreach (var build in Enum.GetValues<DHBuild>())
        {
            _build.AddItem(Loc.GetString($"dh-build-{build.ToString().ToLowerInvariant()}"), (int) build);
        }

        box.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 12,
            Children =
            {
                Field("dh-details-height", _height),
                Field("dh-details-weight", _weight),
                Field("dh-details-build", _build),
            },
        });

        _birthplace = new OptionButton { HorizontalExpand = true };
        _birthYear = new Label { StyleClasses = { "LabelSubText" } };
        box.AddChild(Field("dh-details-birthplace", _birthplace));
        box.AddChild(_birthYear);

        _general = Text("dh-details-general-placeholder");
        _medical = Text("dh-details-medical-placeholder");
        _criminal = Text("dh-details-criminal-placeholder");
        box.AddChild(Field("dh-details-general", _general));
        box.AddChild(Field("dh-details-medical", _medical));
        box.AddChild(Field("dh-details-criminal", _criminal));

        AddChild(new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, Children = { box } });

        _short.OnTextChanged += _ => Changed(d => d.ShortExamine = _short.Text);
        _height.OnTextChanged += _ => Changed(d => d.Height = int.TryParse(_height.Text, out var h) ? h : 0);
        _weight.OnTextChanged += _ => Changed(d => d.Weight = int.TryParse(_weight.Text, out var w) ? w : 0);
        _build.OnItemSelected += args =>
        {
            _build.SelectId(args.Id);
            Changed(d => d.Build = (DHBuild) args.Id);
        };
        _birthplace.OnItemSelected += args =>
        {
            _birthplace.SelectId(args.Id);
            var place = args.Id >= 0 && args.Id < _birthplaceIds.Count ? _birthplaceIds[args.Id] : null;
            Changed(d => d.Birthplace = place == null ? null : new ProtoId<DHBirthplacePrototype>(place.ID));
        };
        _general.OnTextChanged += _ => Changed(d => d.GeneralRecord = Rope.Collapse(_general.TextRope));
        _medical.OnTextChanged += _ => Changed(d => d.MedicalRecord = Rope.Collapse(_medical.TextRope));
        _criminal.OnTextChanged += _ => Changed(d => d.CriminalRecord = Rope.Collapse(_criminal.TextRope));
    }

    private static Control Field(string label, Control control)
    {
        return new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = control.HorizontalExpand,
            Children = { new Label { Text = Loc.GetString(label) }, control },
        };
    }

    private static TextEdit Text(string placeholder)
    {
        return new TextEdit
        {
            HorizontalExpand = true,
            MinHeight = 80,
            Placeholder = new Rope.Leaf(Loc.GetString(placeholder)),
        };
    }

    private void Changed(Action<DHCharacterDetails> edit)
    {
        if (_updating)
            return;

        _details = _details.Clone();
        edit(_details);
        OnChanged?.Invoke(_details);
    }

    /// <summary>Заполнить вкладку из профиля.</summary>
    public void SetDetails(DHCharacterDetails details, int age)
    {
        _updating = true;
        _details = details.Clone();
        _age = age;
        _short.Text = details.ShortExamine;
        _height.Text = details.Height > 0 ? details.Height.ToString() : string.Empty;
        _weight.Text = details.Weight > 0 ? details.Weight.ToString() : string.Empty;
        _build.SelectId((int) details.Build);
        _general.TextRope = new Rope.Leaf(details.GeneralRecord);
        _medical.TextRope = new Rope.Leaf(details.MedicalRecord);
        _criminal.TextRope = new Rope.Leaf(details.CriminalRecord);
        RebuildBirthplaces();
        _updating = false;
    }

    /// <summary>Возраст изменился — меняется год рождения и список мест рождения.</summary>
    public void SetAge(int age)
    {
        if (_age == age)
            return;

        _age = age;
        _updating = true;
        RebuildBirthplaces();
        _updating = false;

        // Место рождения стало невозможным для нового года — сбросить.
        if (_details.Birthplace is { } current &&
            (!_proto.TryIndex(current, out var place) || !place.AvailableIn(DHBirthplaces.BirthYear(age))))
        {
            Changed(d => d.Birthplace = null);
        }
    }

    private void RebuildBirthplaces()
    {
        var year = DHBirthplaces.BirthYear(_age);
        _birthYear.Text = Loc.GetString("dh-details-birth-year", ("year", year));

        _birthplace.Clear();
        _birthplaceIds.Clear();
        _birthplace.AddItem(Loc.GetString("dh-details-birthplace-none"), 0);
        _birthplaceIds.Add(null);

        string? group = null;
        foreach (var place in _proto.EnumeratePrototypes<DHBirthplacePrototype>().OrderBy(p => p.Order))
        {
            if (!place.AvailableIn(year))
                continue;

            if (place.Group != group)
            {
                group = place.Group;
                var header = _birthplaceIds.Count;
                _birthplace.AddItem($"— {Loc.GetString(place.Group)} —", header);
                _birthplace.SetItemDisabled(_birthplace.GetIdx(header), true);
                _birthplaceIds.Add(null);
            }

            var id = _birthplaceIds.Count;
            _birthplace.AddItem(DHBirthplaces.Describe(place, year), id);
            _birthplaceIds.Add(place);
        }

        var selected = 0;
        for (var i = 0; i < _birthplaceIds.Count; i++)
        {
            if (_birthplaceIds[i] is { } p && p.ID == _details.Birthplace?.Id)
                selected = i;
        }

        _birthplace.SelectId(selected);
    }
}
