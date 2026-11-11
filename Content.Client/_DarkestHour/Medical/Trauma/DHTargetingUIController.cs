using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._DarkestHour.Medical.Trauma;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.Medical.Trauma;

/// <summary>
/// Клиентская часть выбора зоны: запрос серверу + мгновенная смена у себя.
/// </summary>
public sealed partial class DHTargetingClientSystem : EntitySystem
{
    public void RequestTarget(EntityUid player, string category)
    {
        if (TryComp<DHTargetingComponent>(player, out var targeting))
            targeting.Target = category;

        RaiseNetworkEvent(new DHSetTargetRequestEvent(category));
    }
}

/// <summary>
/// Кукла выбора зоны попадания в правом нижнем углу экрана. Добавляется на игровой экран без правки ванильных XAML;
/// видна, только если у текущего персонажа есть <see cref="DHTargetingComponent"/>.
/// </summary>
public sealed partial class DHTargetingUIController : UIController
{
    [Dependency] private IPlayerManager _player = default!;

    [UISystemDependency] private readonly DHTargetingClientSystem? _system = default;

    private PanelContainer? _panel;
    private DHTargetingDoll? _doll;
    private Control? _hud;

    /// <summary>Отступ куклы от правого края экрана и от плашки HUD.</summary>
    private const float Gap = 10f;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        if (UIManager.ActiveScreen is not { } screen)
            return;

        _doll = new DHTargetingDoll();
        _doll.OnZoneSelected += OnZoneSelected;

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(4),
        };
        box.AddChild(new Label
        {
            Text = Loc.GetString("dh-targeting-title"),
            HorizontalAlignment = Control.HAlignment.Center,
            FontColorOverride = Color.FromHex("#9aa0a6"),
        });
        box.AddChild(_doll);

        _panel = new PanelContainer
        {
            PanelOverride = new Robust.Client.Graphics.StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#16181acc"),
                BorderColor = Color.FromHex("#3a3f44"),
                BorderThickness = new Thickness(1),
            },
            Visible = false,
        };
        _panel.AddChild(box);

        screen.AddChild(_panel);
        LayoutContainer.SetAnchorAndMarginPreset(_panel, LayoutContainer.LayoutPreset.BottomRight, margin: 10);
        // Прижата к правому нижнему углу — расти должна влево и вверх, иначе уходит за край экрана.
        LayoutContainer.SetGrowHorizontal(_panel, LayoutContainer.GrowDirection.Begin);
        LayoutContainer.SetGrowVertical(_panel, LayoutContainer.GrowDirection.Begin);
    }

    private void OnScreenUnload()
    {
        _panel?.Orphan();
        _panel = null;
        _doll = null;
        _hud = null;
    }

    /// <summary>
    /// Плашка HUD TDH (уровень, компас, рация) стоит в правом нижнем углу игрового окна. В обычной раскладке это тот же
    /// угол экрана — кукла встаёт левее плашки; в раздельной плашка у края игрового окна, а кукла — у края экрана.
    /// </summary>
    private void PlaceBesideHud(Control screen)
    {
        _hud ??= FindHud(screen);
        var right = Gap;
        if (_hud is { Visible: true } hud)
        {
            var hudLeft = hud.GlobalPosition.X;
            var hudRight = hudLeft + hud.Size.X;
            // Плашка прижата к правому краю экрана — сдвигаемся за её левый край.
            if (screen.Size.X - hudRight < _panel!.Size.X + Gap)
                right = screen.Size.X - hudLeft + Gap;
        }

        if (Math.Abs(_panel!.GetValue<float>(LayoutContainer.MarginRightProperty) + right) > 0.5f)
        {
            LayoutContainer.SetMarginLeft(_panel!, -right - _panel!.Size.X);
            LayoutContainer.SetMarginRight(_panel!, -right);
        }
    }

    private static Control? FindHud(Control root)
    {
        foreach (var child in root.Children)
        {
            if (child is Content.Client._DarkestHour.UI.DHHudWidget)
                return child;

            if (FindHud(child) is { } found)
                return found;
        }

        return null;
    }

    private void OnZoneSelected(string zone)
    {
        if (_player.LocalEntity is { } player)
            _system?.RequestTarget(player, zone);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_panel == null || _doll == null)
            return;

        if (_player.LocalEntity is not { } player ||
            !EntityManager.TryGetComponent(player, out DHTargetingComponent? targeting))
        {
            _panel.Visible = false;
            return;
        }

        _panel.Visible = true;
        _doll.Selection = targeting.Target;
        if (_panel.Parent is { } screen)
            PlaceBesideHud(screen);
    }
}
