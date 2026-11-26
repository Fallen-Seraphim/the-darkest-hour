using System.Linq;
using Content.Client._DarkestHour.ThirdParty;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Ghost.Controls.Roles;

/// <summary>
/// DarkestHour: наборы событий третьей стороны (ассоциативный десантник, ЧВК LA-V) — вверху окна «Роли призраков», с
/// одной кнопкой «Интересоваться» на всё событие, как в RuCM (решение автора 2026-11-26).
/// </summary>
public sealed partial class GhostRolesWindow
{
    private DHThirdPartyClientSystem? _dhThirdParty;

    private bool DHHasEvents => _dhThirdParty is { Events.Count: > 0 };

    private void DHInit()
    {
        _dhThirdParty = IoCManager.Resolve<IEntityManager>().System<DHThirdPartyClientSystem>();
        _dhThirdParty.Updated += DHRebuild;
        OnClose += () =>
        {
            if (_dhThirdParty != null)
                _dhThirdParty.Updated -= DHRebuild;
        };
        DHRebuild();
    }

    private void DHRebuild()
    {
        DHEventContainer.RemoveAllChildren();
        var events = _dhThirdParty?.Events ?? Array.Empty<Content.Shared._DarkestHour.ThirdParty.DHThirdPartyEventInfo>();
        DHEventContainer.Visible = events.Count > 0;
        NoRolesMessage.Visible = events.Count == 0 && EntryContainer.ChildCount == 0;
        if (events.Count == 0)
            return;

        DHEventContainer.AddChild(new Label
        {
            Text = Loc.GetString("dh-third-party-heading"),
            StyleClasses = { "LabelHeading" },
        });

        foreach (var ev in events)
        {
            DHEventContainer.AddChild(new GhostRoleInfoBox(ev.Name, ev.Description));

            var details = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Margin = new Thickness(8, 0, 8, 4),
            };
            details.AddChild(new Label
            {
                Text = Loc.GetString("dh-third-party-roles", ("roles", string.Join(", ", ev.Roles.Select(r => r.Name)))),
                ClipText = true,
                HorizontalExpand = true,
            });
            details.AddChild(new Label
            {
                Text = Loc.GetString("dh-third-party-count", ("count", ev.Interested), ("required", ev.Required),
                    ("slots", ev.Slots)),
            });
            details.AddChild(new Label
            {
                Text = Loc.GetString("dh-third-party-time-left", ("minutes", ev.SecondsLeft / 60),
                    ("seconds", ev.SecondsLeft % 60)),
                FontColorOverride = Color.FromHex("#9A9A9A"),
            });
            DHEventContainer.AddChild(details);

            var button = new Button
            {
                Text = Loc.GetString(ev.Mine ? "dh-third-party-leave" : "dh-third-party-join"),
                ToolTip = Loc.GetString("dh-third-party-join-tooltip"),
                Margin = new Thickness(8, 0, 8, 8),
            };
            if (ev.Mine)
                button.StyleClasses.Add(Content.Client.Stylesheets.StyleClass.Negative);

            var id = ev.Id;
            var interested = !ev.Mine;
            button.OnPressed += _ => _dhThirdParty?.SetInterest(id, interested);
            DHEventContainer.AddChild(button);
        }
    }
}
