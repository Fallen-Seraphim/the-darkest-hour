using System.Numerics;
using Content.Shared._DarkestHour.Characters;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Verbs;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._DarkestHour.Characters;

/// <summary>
/// «Подробное описание» при осмотре (как в CMU, решение автора 2026-11-11): окно с возрастом, местом рождения,
/// ростом, весом, телосложением, внешностью и «документами» персонажа. Только если лицо узнаваемо.
/// </summary>
public sealed partial class DHDetailedExamineSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private DefaultWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHCharacterDescriptionComponent, GetVerbsEvent<ExamineVerb>>(OnVerbs);
    }

    private void OnVerbs(Entity<DHCharacterDescriptionComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (Identity.Name(args.Target, EntityManager) != MetaData(args.Target).EntityName)
            return;

        var inRange = _examine.IsInDetailsRange(args.User, ent);
        args.Verbs.Add(new ExamineVerb
        {
            Text = Loc.GetString("dh-detailed-examine-verb"),
            Category = VerbCategory.Examine,
            ClientExclusive = true,
            Disabled = !inRange,
            Message = inRange ? null : Loc.GetString("detail-examinable-verb-disabled"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/examine.svg.192dpi.png")),
            Act = () => Open(ent),
        });
    }

    private void Open(Entity<DHCharacterDescriptionComponent> ent)
    {
        _window?.Close();
        var d = ent.Comp.Details;
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6, Margin = new(8) };

        void Row(string key, string value)
        {
            box.AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 8,
                Children =
                {
                    new Label { Text = Loc.GetString(key), MinWidth = 130, StyleClasses = { "LabelSubText" } },
                    new Label { Text = value, HorizontalExpand = true, ClipText = true },
                },
            });
        }

        void Section(string key, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            box.AddChild(new Label { Text = Loc.GetString(key), StyleClasses = { "LabelHeading" }, Margin = new(0, 6, 0, 0) });
            var rich = new RichTextLabel { HorizontalExpand = true };
            rich.SetMessage(FormattedMessage.FromUnformatted(text));
            box.AddChild(rich);
        }

        var year = DHBirthplaces.BirthYear(ent.Comp.Age);
        Row("dh-detailed-examine-age", Loc.GetString("dh-detailed-examine-age-value", ("age", ent.Comp.Age), ("year", year)));
        if (d.Birthplace is { } place && _proto.TryIndex(place, out var birthplace))
            Row("dh-detailed-examine-birthplace", DHBirthplaces.Describe(birthplace, year));
        if (d.Height > 0)
            Row("dh-detailed-examine-height", Loc.GetString("dh-detailed-examine-height-value", ("cm", d.Height)));
        if (d.Weight > 0)
            Row("dh-detailed-examine-weight", Loc.GetString("dh-detailed-examine-weight-value", ("kg", d.Weight)));
        Row("dh-detailed-examine-build", Loc.GetString($"dh-build-{d.Build.ToString().ToLowerInvariant()}"));

        Section("dh-detailed-examine-appearance", ent.Comp.Appearance);
        Section("dh-detailed-examine-general", d.GeneralRecord);
        Section("dh-detailed-examine-medical", d.MedicalRecord);
        Section("dh-detailed-examine-criminal", d.CriminalRecord);

        _window = new DefaultWindow
        {
            Title = Loc.GetString("dh-detailed-examine-title", ("name", MetaData(ent).EntityName)),
            MinSize = new Vector2(440, 360),
            SetSize = new Vector2(480, 520),
        };
        _window.Contents.AddChild(new ScrollContainer
        {
            HScrollEnabled = false,
            VerticalExpand = true,
            Children = { box },
        });
        _window.OpenCentered();
    }
}
