using Content.Shared._DarkestHour.ThirdParty;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.ThirdParty;

/// <summary>Окно записи на роли событий третьей стороны (для наблюдателей).</summary>
public sealed class DHThirdPartyWindow : DefaultWindow
{
    private readonly DHThirdPartyClientSystem _system;
    private readonly BoxContainer _list;

    public DHThirdPartyWindow(DHThirdPartyClientSystem system)
    {
        _system = system;
        Title = Loc.GetString("dh-third-party-window-title");
        MinSize = new System.Numerics.Vector2(460, 260);

        _list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        Contents.AddChild(new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, Children = { _list } });

        _system.Updated += Rebuild;
        OnClose += () => _system.Updated -= Rebuild;
        Rebuild();
    }

    private void Rebuild()
    {
        _list.RemoveAllChildren();
        if (_system.Events.Count == 0)
        {
            _list.AddChild(new Label { Text = Loc.GetString("dh-third-party-none") });
            return;
        }

        foreach (var ev in _system.Events)
        {
            _list.AddChild(new Label { Text = ev.Name, StyleClasses = { "LabelHeading" } });
            _list.AddChild(new RichTextLabel { Text = ev.Description });
            _list.AddChild(new Label
            {
                Text = Loc.GetString("dh-third-party-time-left", ("minutes", ev.SecondsLeft / 60), ("seconds", ev.SecondsLeft % 60)),
                FontColorOverride = Color.FromHex("#9A9A9A"),
            });

            foreach (var role in ev.Roles)
            {
                var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
                var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
                text.AddChild(new Label { Text = Loc.GetString("dh-third-party-role", ("name", role.Name), ("count", role.Interested)) });
                text.AddChild(new RichTextLabel { Text = role.Description });
                row.AddChild(text);

                var button = new Button
                {
                    Text = Loc.GetString(role.Mine ? "dh-third-party-leave" : "dh-third-party-join"),
                    MinWidth = 120,
                    VerticalAlignment = VAlignment.Top,
                };
                var eventId = ev.Id;
                var roleId = role.Id;
                var mine = role.Mine;
                button.OnPressed += _ => _system.SetInterest(eventId, roleId, !mine);
                row.AddChild(button);
                _list.AddChild(row);
            }
        }
    }
}
