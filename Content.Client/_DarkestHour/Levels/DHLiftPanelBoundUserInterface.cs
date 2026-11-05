using System.Numerics;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Levels;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Levels;

/// <summary>Пульт Z-лифта: все этажи шахты сверху вниз; «Отправить» — груз с этой платформы туда, «Вызвать» — оттуда сюда.</summary>
[UsedImplicitly]
public sealed class DHLiftPanelBoundUserInterface : BoundUserInterface
{
    private DHLiftPanelWindow? _window;

    public DHLiftPanelBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHLiftPanelWindow>();
        _window.OnSend += lift => SendMessage(new DHLiftSendMessage(lift));
        _window.OnCall += lift => SendMessage(new DHLiftCallMessage(lift));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHLiftPanelState panel)
            _window?.UpdateState(panel);
    }
}

public sealed class DHLiftPanelWindow : DefaultWindow
{
    public event Action<NetEntity>? OnSend;
    public event Action<NetEntity>? OnCall;

    private static readonly Color HereColor = Color.FromHex("#E8B04A");

    private readonly Label _status = new();
    private readonly BoxContainer _floors = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 3 };

    public DHLiftPanelWindow()
    {
        DHFonts.RegisterTerminal(this);
        Title = Loc.GetString("dh-lift-ui-title");
        MinSize = new Vector2(360, 380);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };
        root.AddChild(_status);
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(_floors);
        root.AddChild(scroll);
        Contents.AddChild(root);
    }

    public void UpdateState(DHLiftPanelState state)
    {
        _status.Text = Loc.GetString(state.Busy ? "dh-lift-ui-busy" : "dh-lift-ui-ready");
        _status.FontColorOverride = state.Busy ? HereColor : Color.Gray;

        _floors.RemoveAllChildren();
        foreach (var floor in state.Floors)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
            var name = new Label
            {
                Text = floor.Here ? Loc.GetString("dh-lift-ui-here", ("level", floor.Name)) : floor.Name,
                HorizontalExpand = true,
            };
            if (floor.Here)
                name.FontColorOverride = HereColor;
            row.AddChild(name);

            if (!floor.Here)
            {
                var lift = floor.Lift;
                var send = new Button { Text = Loc.GetString("dh-lift-ui-send"), Disabled = state.Busy };
                send.OnPressed += _ => OnSend?.Invoke(lift);
                var call = new Button { Text = Loc.GetString("dh-lift-ui-call"), Disabled = state.Busy };
                call.OnPressed += _ => OnCall?.Invoke(lift);
                row.AddChild(send);
                row.AddChild(call);
            }

            _floors.AddChild(row);
        }
    }
}
