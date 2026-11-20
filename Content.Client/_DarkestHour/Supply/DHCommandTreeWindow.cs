using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Supply;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._DarkestHour.Supply;

/// <summary>
/// Окно компьютера штаба: очки командования, доход за точки и древо по веткам и ступеням. Собрано кодом, без XAML.
/// </summary>
public sealed partial class DHCommandTreeWindow : DefaultWindow
{
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly Color BranchColor = Color.FromHex("#9fd6a0");
    private static readonly Color OpenedColor = Color.FromHex("#7fd890");
    private static readonly Color LockedColor = Color.FromHex("#808080");

    public event Action<string>? OnUnlock;

    private readonly Label _points = new();
    private readonly BoxContainer _list = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };

    public DHCommandTreeWindow()
    {
        IoCManager.InjectDependencies(this);
        DHFonts.RegisterTerminal(this);
        Title = Loc.GetString("dh-command-tree-ui-title");
        MinSize = new System.Numerics.Vector2(520, 560);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(6),
        };

        root.AddChild(_points);
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_list);
        root.AddChild(scroll);
        Contents.AddChild(root);
    }

    public void UpdateState(DHCommandTreeState state)
    {
        _points.Text = Loc.GetString("dh-command-tree-ui-points", ("points", state.Points), ("income", state.Income));

        _list.RemoveAllChildren();
        string? branch = null;
        foreach (var node in state.Nodes)
        {
            if (node.Branch != branch)
            {
                branch = node.Branch;
                _list.AddChild(new Label { Text = Loc.GetString(branch), FontColorOverride = BranchColor });
            }

            var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8, 0, 0, 4) };
            var line = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
            line.AddChild(new Label
            {
                Text = Loc.GetString("dh-command-tree-ui-node", ("tier", node.Tier), ("name", Loc.GetString(node.Name))),
                HorizontalExpand = true,
                ClipText = true,
                FontColorOverride = node.State switch
                {
                    DHCommandTreeNodeState.Opened => OpenedColor,
                    DHCommandTreeNodeState.Locked => LockedColor,
                    _ => null,
                },
            });

            if (node.State == DHCommandTreeNodeState.Opened)
            {
                line.AddChild(new Label { Text = Loc.GetString("dh-command-tree-ui-opened"), FontColorOverride = OpenedColor });
            }
            else
            {
                var id = node.Id;
                var button = new Button
                {
                    Text = Loc.GetString("dh-command-tree-ui-open", ("cost", node.Cost)),
                    Disabled = node.State == DHCommandTreeNodeState.Locked || node.Cost > state.Points,
                };
                button.OnPressed += _ => OnUnlock?.Invoke(id);
                line.AddChild(button);
            }

            box.AddChild(line);

            var details = new List<string>();
            if (node.Description is { } description)
                details.Add(Loc.GetString(description));

            if (node.Unlocks.Count > 0)
            {
                var names = new List<string>();
                foreach (var proto in node.Unlocks)
                {
                    names.Add(_proto.TryIndex<EntityPrototype>(proto, out var entity) ? entity.Name : proto);
                }

                details.Add(Loc.GetString("dh-command-tree-ui-unlocks", ("items", string.Join(", ", names))));
            }

            if (node.State == DHCommandTreeNodeState.Locked && node.Requires.Count > 0)
            {
                var names = new List<string>();
                foreach (var required in node.Requires)
                {
                    names.Add(Loc.GetString(required));
                }

                details.Add(Loc.GetString("dh-command-tree-ui-requires", ("nodes", string.Join(", ", names))));
            }

            foreach (var detail in details)
            {
                // Длинные описания переносятся.
                var message = new FormattedMessage();
                message.PushColor(LockedColor);
                message.AddText(detail);
                message.Pop();
                var label = new RichTextLabel { Margin = new Thickness(12, 0, 0, 0) };
                label.SetMessage(message);
                box.AddChild(label);
            }

            _list.AddChild(box);
        }
    }
}
