using System.Linq;
using System.Numerics;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Squads;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Squads;

/// <summary>Консоль отрядов: состав каждого отряда и бойцы без отряда; кнопки — перевести бойца в отряд.</summary>
public sealed class DHSquadConsoleWindow : DefaultWindow
{
    public event Action<NetEntity, string?>? OnAssign;

    private static readonly Color Dim = Color.FromHex("#8A8A8A");
    private static readonly Color Dead = Color.FromHex("#a05050");
    private static readonly Color Header = Color.FromHex("#E8B04A");

    private readonly BoxContainer _list = new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        SeparationOverride = 4,
        Margin = new Thickness(8),
    };

    public DHSquadConsoleWindow()
    {
        DHFonts.RegisterTerminal(this); // DarkestHour: терминальный шрифт
        Title = Loc.GetString("dh-squad-console-title");
        MinSize = new Vector2(640, 480);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(_list);
        Contents.AddChild(scroll);
    }

    public void UpdateState(DHSquadConsoleState state)
    {
        _list.RemoveAllChildren();

        // Сначала отряды (с составом), потом — бойцы без отряда.
        foreach (var squad in state.Squads)
        {
            var members = state.Fighters.Where(f => f.Squad == squad.Id).ToList();
            _list.AddChild(new Label
            {
                Text = Loc.GetString("dh-squad-console-squad", ("squad", squad.Name), ("count", members.Count)),
                FontColorOverride = squad.Color,
            });

            foreach (var fighter in members)
            {
                _list.AddChild(Row(fighter, state.Squads));
            }
        }

        var free = state.Fighters.Where(f => f.Squad == null || state.Squads.All(s => s.Id != f.Squad)).ToList();
        _list.AddChild(new Label
        {
            Text = Loc.GetString("dh-squad-console-unassigned", ("count", free.Count)),
            FontColorOverride = Header,
        });

        foreach (var fighter in free)
        {
            _list.AddChild(Row(fighter, state.Squads));
        }

        if (state.Fighters.Count == 0)
            _list.AddChild(new Label { Text = Loc.GetString("dh-squad-console-empty"), FontColorOverride = Dim });
    }

    private Control Row(DHSquadFighterEntry fighter, List<DHSquadInfo> squads)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            Margin = new Thickness(12, 0, 0, 0),
        };

        var text = string.IsNullOrEmpty(fighter.Job) ? fighter.Name : $"{fighter.Name} — {fighter.Job}";
        row.AddChild(new Label
        {
            Text = text,
            HorizontalExpand = true,
            ClipText = true,
            FontColorOverride = fighter.Alive ? null : Dead,
        });

        var entity = fighter.Entity;
        foreach (var squad in squads)
        {
            var button = new Button
            {
                Text = squad.Name,
                Disabled = fighter.Squad == squad.Id,
                ModulateSelfOverride = squad.Color,
                MinWidth = 70,
            };
            var id = squad.Id;
            button.OnPressed += _ => OnAssign?.Invoke(entity, id);
            row.AddChild(button);
        }

        var remove = new Button
        {
            Text = Loc.GetString("dh-squad-console-remove"),
            Disabled = fighter.Squad == null,
            MinWidth = 40,
        };
        remove.OnPressed += _ => OnAssign?.Invoke(entity, null);
        row.AddChild(remove);
        return row;
    }
}
