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
    }

    private void OnScreenUnload()
    {
        _panel?.Orphan();
        _panel = null;
        _doll = null;
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
    }
}
