using System.Linq;
using System.Numerics;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Overwatch;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.TacticalMap;

/// <summary>Окно тактической карты (открывается из консоли и планшета наблюдения). Есть Z-уровни — выбор уровня.</summary>
public sealed class DHTacticalMapWindow : DefaultWindow
{
    private readonly DHTacticalMapControl _map = new() { HorizontalExpand = true, VerticalExpand = true };
    private readonly BoxContainer _levels = new()
    {
        Orientation = BoxContainer.LayoutOrientation.Horizontal,
        SeparationOverride = 4,
        Visible = false,
    };

    private string _levelsKey = string.Empty;

    /// <summary>Выбран другой уровень: карта уровня (нужна её местность).</summary>
    public event Action<NetEntity?>? OnLevelSelected;

    public DHTacticalMapWindow()
    {
        DHFonts.RegisterTerminal(this);
        Title = Loc.GetString("dh-tacmap-title");
        MinSize = new Vector2(520, 520);
        SetSize = new Vector2(760, 800);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        header.AddChild(new Label { Text = Loc.GetString("dh-tacmap-legend"), HorizontalExpand = true });
        var reset = new Button { Text = Loc.GetString("dh-tacmap-reset") };
        reset.OnPressed += _ => _map.ResetView();
        header.AddChild(reset);
        root.AddChild(header);
        root.AddChild(_levels);
        root.AddChild(_map);
        Contents.AddChild(root);
    }

    public NetEntity? SelectedMap => _map.SelectedMap();

    public void UpdateState(DHOverwatchState state)
    {
        _map.SetState(state);
        RebuildLevels(state);
    }

    private void RebuildLevels(DHOverwatchState state)
    {
        var key = string.Join(",", state.Levels.Select(l => l.Level));
        if (key == _levelsKey)
            return;

        _levelsKey = key;
        _levels.RemoveAllChildren();
        _levels.Visible = state.Levels.Count > 1;
        if (!_levels.Visible)
        {
            _map.SelectedLevel = 0;
            return;
        }

        if (state.Levels.All(l => l.Level != _map.SelectedLevel))
            _map.SelectedLevel = 0;

        _levels.AddChild(new Label { Text = Loc.GetString("dh-tacmap-level") });
        var group = new ButtonGroup();
        // Сверху вниз: крыши слева, подвалы справа.
        foreach (var level in state.Levels.OrderByDescending(l => l.Level))
        {
            var number = level.Level;
            var button = new Button
            {
                Text = number switch
                {
                    0 => Loc.GetString("dh-tacmap-level-ground"),
                    > 0 => $"+{number}",
                    _ => number.ToString(),
                },
                ToggleMode = true,
                Group = group,
                Pressed = number == _map.SelectedLevel,
            };
            var map = level.Map;
            button.OnPressed += _ =>
            {
                _map.SelectedLevel = number;
                OnLevelSelected?.Invoke(map);
            };
            _levels.AddChild(button);
        }
    }
}
