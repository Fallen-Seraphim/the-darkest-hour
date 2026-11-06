using System.Numerics;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.City;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client._DarkestHour.City;

/// <summary>Текущая обстановка в городе (для телефонов и терминалов).</summary>
public sealed partial class DHCityStatusClientSystem : EntitySystem
{
    public DHCityStatus Status { get; private set; } = DHCityStatus.Normal;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DHCityStatusEvent>(OnStatus);
    }

    private void OnStatus(DHCityStatusEvent ev)
    {
        Status = ev.Status;
    }
}

// ---------------------------------------------------------------- компьютер связи района / ВП

[UsedImplicitly]
public sealed class DHCityCommsBoundUserInterface : BoundUserInterface
{
    private DHCityCommsWindow? _window;

    public DHCityCommsBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHCityCommsWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.OnAnnounce += text => SendMessage(new DHCityCommsAnnounceMessage(text));
        _window.OnStatus += status => SendMessage(new DHCityCommsSetStatusMessage(status));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHCityCommsState comms)
            _window?.UpdateState(comms);
    }
}

public sealed class DHCityCommsWindow : DefaultWindow
{
    public event Action<string>? OnAnnounce;
    public event Action<DHCityStatus>? OnStatus;

    private readonly Label _sender = new() { FontColorOverride = DHPhoneUi.Accent };
    private readonly Label _status = new();
    private readonly TextEdit _text = new() { HorizontalExpand = true, VerticalExpand = true, MinHeight = 120 };
    private readonly Button _send = new();
    private readonly Label _cooldown = new() { FontColorOverride = DHPhoneUi.Warn };
    private readonly BoxContainer _statusButtons = DHPhoneUi.Row(6);

    public DHCityCommsWindow()
    {
        DHFonts.RegisterTerminal(this);
        MinSize = new Vector2(440, 360);
        SetSize = new Vector2(460, 400);

        var root = DHPhoneUi.Column(8);
        root.Margin = new Thickness(8);
        root.VerticalExpand = true;

        root.AddChild(_sender);
        var status = DHPhoneUi.Row(6);
        status.AddChild(new Label { Text = Loc.GetString("dh-city-comms-ui-status"), FontColorOverride = DHPhoneUi.Dim });
        status.AddChild(_status);
        root.AddChild(status);

        foreach (var value in Enum.GetValues<DHCityStatus>())
        {
            var button = new Button
            {
                Text = Loc.GetString(DHCityStatuses.Name(value)),
                HorizontalExpand = true,
                ModulateSelfOverride = DHCityStatuses.TextColor(value),
            };
            button.OnPressed += _ => OnStatus?.Invoke(value);
            _statusButtons.AddChild(button);
        }

        root.AddChild(_statusButtons);
        root.AddChild(new Label { Text = Loc.GetString("dh-city-comms-ui-announce"), FontColorOverride = DHPhoneUi.Dim });
        root.AddChild(_text);

        var bottom = DHPhoneUi.Row(6);
        _cooldown.HorizontalExpand = true;
        bottom.AddChild(_cooldown);
        _send.Text = Loc.GetString("dh-city-comms-ui-send");
        _send.OnPressed += _ =>
        {
            var text = Rope.Collapse(_text.TextRope).Trim();
            if (text.Length == 0)
                return;

            OnAnnounce?.Invoke(text);
            _text.TextRope = new Rope.Leaf(string.Empty);
        };
        bottom.AddChild(_send);
        root.AddChild(bottom);

        Contents.AddChild(root);
    }

    public void UpdateState(DHCityCommsState state)
    {
        _sender.Text = Loc.GetString("dh-city-comms-ui-sender", ("sender", state.Sender));
        _status.Text = Loc.GetString(DHCityStatuses.Name(state.Status));
        _status.FontColorOverride = DHCityStatuses.TextColor(state.Status);
        _statusButtons.Visible = state.CanSetStatus;
        _send.Disabled = state.Cooldown > 0;
        _cooldown.Text = state.Cooldown > 0 ? Loc.GetString("dh-city-comms-ui-wait", ("seconds", state.Cooldown)) : string.Empty;
    }
}
