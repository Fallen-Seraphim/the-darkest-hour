using System.Linq;
using System.Numerics;
using Content.Shared._DarkestHour.Medical.Surgery;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Medical.Surgery;

/// <summary>
/// Окно хирургии: слева части тела, справа состояние выбранной части, идущая операция по шагам и доступные операции.
/// Собрано в коде, без XAML.
/// </summary>
public sealed class DHSurgeryWindow : DefaultWindow
{
    public event Action<string>? OnSelectPart;
    public event Action<string, string>? OnStart;
    public event Action<string>? OnStep;
    public event Action<string>? OnCancel;

    private static readonly Color DoneColor = Color.FromHex("#7fae6a");
    private static readonly Color NextColor = Color.FromHex("#e0c27a");
    private static readonly Color SkippedColor = Color.FromHex("#6b6b6b");
    private static readonly Color WarningColor = Color.FromHex("#e08a4a");
    private static readonly Color MissingColor = Color.FromHex("#b05050");

    private readonly BoxContainer _warnings = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _parts = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, MinWidth = 170 };
    private readonly BoxContainer _details = new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        HorizontalExpand = true,
        SeparationOverride = 4,
    };

    private string? _selected;
    private DHSurgeryBuiState? _state;

    public DHSurgeryWindow()
    {
        Title = Loc.GetString("dh-surgery-ui-title");
        MinSize = new Vector2(560, 420);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(6),
        };
        root.AddChild(_warnings);

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            VerticalExpand = true,
        };
        body.AddChild(_parts);
        body.AddChild(new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { _details },
        });
        root.AddChild(body);

        Contents.AddChild(root);
    }

    public void UpdateState(DHSurgeryBuiState state)
    {
        _state = state;
        Title = Loc.GetString("dh-surgery-ui-title-patient", ("patient", state.PatientName));

        _warnings.RemoveAllChildren();
        foreach (var warning in state.Warnings)
        {
            _warnings.AddChild(new Label { Text = "⚠ " + warning, FontColorOverride = WarningColor });
        }

        if (_selected == null || state.Parts.All(p => p.Category != _selected))
        {
            // По умолчанию — часть с идущей операцией, иначе торс.
            _selected = state.Parts.FirstOrDefault(p => p.ActiveId != null)?.Category
                        ?? state.Parts.FirstOrDefault(p => p.Category == "Torso")?.Category
                        ?? state.Parts.FirstOrDefault()?.Category;
            if (_selected != null)
                OnSelectPart?.Invoke(_selected);
        }

        RebuildParts();
        RebuildDetails();
    }

    private void RebuildParts()
    {
        _parts.RemoveAllChildren();
        if (_state == null)
            return;

        foreach (var part in _state.Parts)
        {
            var text = part.Name;
            if (!part.Present)
                text += " " + Loc.GetString("dh-surgery-ui-missing");
            else if (part.ActiveId != null)
                text += " ▶";
            else if (part.State != DHPartState.None)
                text += " ✚";

            var button = new Button
            {
                Text = text,
                ToggleMode = true,
                Pressed = part.Category == _selected,
                HorizontalExpand = true,
            };

            if (!part.Present)
                button.ModulateSelfOverride = MissingColor;

            var category = part.Category;
            button.OnPressed += _ =>
            {
                _selected = category;
                OnSelectPart?.Invoke(category);
                RebuildParts();
                RebuildDetails();
            };
            _parts.AddChild(button);
        }
    }

    private void RebuildDetails()
    {
        _details.RemoveAllChildren();
        var part = _state?.Parts.FirstOrDefault(p => p.Category == _selected);
        if (part == null)
            return;

        _details.AddChild(new Label { Text = part.Name, StyleClasses = { "LabelHeading" } });
        _details.AddChild(new Label
        {
            Text = Loc.GetString("dh-surgery-ui-state", ("state", StateText(part))),
            FontColorOverride = Color.LightGray,
        });

        if (part.ActiveId != null)
        {
            _details.AddChild(new Label
            {
                Text = Loc.GetString("dh-surgery-ui-active", ("surgery", part.ActiveName ?? string.Empty)),
                StyleClasses = { "LabelSubHeading" },
            });

            var nextShown = false;
            foreach (var step in part.ActiveSteps)
            {
                string mark;
                Color color;
                if (step.Done)
                {
                    mark = "✔";
                    color = DoneColor;
                }
                else if (step.Skipped)
                {
                    mark = "—";
                    color = SkippedColor;
                }
                else if (!nextShown)
                {
                    mark = "▶";
                    color = NextColor;
                    nextShown = true;
                }
                else
                {
                    mark = "·";
                    color = Color.White;
                }

                var text = $"{mark} {step.Name}";
                if (step.ToolHint != null && !step.Done && !step.Skipped)
                    text += $" ({step.ToolHint})";

                _details.AddChild(new Label { Text = text, FontColorOverride = color });
            }

            var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
            var stepButton = new Button { Text = Loc.GetString("dh-surgery-ui-step"), HorizontalExpand = true };
            stepButton.OnPressed += _ => OnStep?.Invoke(part.Category);
            var cancelButton = new Button { Text = Loc.GetString("dh-surgery-ui-cancel") };
            cancelButton.OnPressed += _ => OnCancel?.Invoke(part.Category);
            buttons.AddChild(stepButton);
            buttons.AddChild(cancelButton);
            _details.AddChild(buttons);
            _details.AddChild(new Label
            {
                Text = Loc.GetString("dh-surgery-ui-step-hint"),
                FontColorOverride = Color.Gray,
            });
        }

        _details.AddChild(new Label
        {
            Text = Loc.GetString("dh-surgery-ui-options"),
            StyleClasses = { "LabelSubHeading" },
            Margin = new Thickness(0, 6, 0, 0),
        });

        if (part.Options.Count == 0)
        {
            _details.AddChild(new Label { Text = Loc.GetString("dh-surgery-ui-no-options"), FontColorOverride = Color.Gray });
            return;
        }

        foreach (var option in part.Options)
        {
            var button = new Button
            {
                Text = option.Name,
                HorizontalExpand = true,
                ToolTip = option.Description,
            };
            var id = option.Id;
            button.OnPressed += _ => OnStart?.Invoke(part.Category, id);
            _details.AddChild(button);
        }
    }

    private static string StateText(DHSurgeryPartData part)
    {
        if (!part.Present)
            return Loc.GetString("dh-surgery-state-missing");

        if (part.State == DHPartState.None)
            return Loc.GetString("dh-surgery-state-intact");

        var states = new List<string>();
        if ((part.State & DHPartState.Incised) != 0)
            states.Add(Loc.GetString("dh-surgery-state-incised"));
        if ((part.State & DHPartState.Clamped) != 0)
            states.Add(Loc.GetString("dh-surgery-state-clamped"));
        if ((part.State & DHPartState.Retracted) != 0)
            states.Add(Loc.GetString("dh-surgery-state-retracted"));
        if ((part.State & DHPartState.BoneOpen) != 0)
            states.Add(Loc.GetString("dh-surgery-state-bone-open"));

        return string.Join(", ", states);
    }
}
