using Content.Client._DarkestHour.UI;
using System.Numerics;
using Content.Shared._DarkestHour.Comms;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._DarkestHour.Comms;

/// <summary>Полевой телефон: состояние линии, ответ/отбой и справочник своей сети.</summary>
public sealed class DHFieldPhoneWindow : DefaultWindow
{
    public event Action<NetEntity>? OnCall;
    public event Action? OnAnswer;
    public event Action? OnHangUp;

    private static readonly Color Ok = Color.FromHex("#8FD66B");
    private static readonly Color Warn = Color.FromHex("#E8B04A");
    private static readonly Color Dim = Color.FromHex("#8A8A8A");

    private readonly Label _status = new();
    private readonly Button _answer = new() { HorizontalExpand = true };
    private readonly Button _hangUp = new() { HorizontalExpand = true };
    private readonly BoxContainer _book = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 3 };

    public DHFieldPhoneWindow()
    {
        DHFonts.RegisterTerminal(this); // DarkestHour: терминальный шрифт
        Title = Loc.GetString("dh-phone-ui-title");
        MinSize = new Vector2(340, 320);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        root.AddChild(_status);
        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        _answer.Text = Loc.GetString("dh-phone-ui-answer");
        _answer.OnPressed += _ => OnAnswer?.Invoke();
        _hangUp.Text = Loc.GetString("dh-phone-ui-hang-up");
        _hangUp.OnPressed += _ => OnHangUp?.Invoke();
        buttons.AddChild(_answer);
        buttons.AddChild(_hangUp);
        root.AddChild(buttons);

        root.AddChild(new Label { Text = Loc.GetString("dh-phone-ui-book"), FontColorOverride = Warn });
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(_book);
        root.AddChild(scroll);
        Contents.AddChild(root);
    }

    public void UpdateState(DHFieldPhoneState state)
    {
        Title = state.Name;
        var other = state.Other ?? "—";
        _status.Text = state.Status switch
        {
            DHFieldPhoneStatus.Calling => Loc.GetString("dh-phone-ui-calling", ("other", other)),
            DHFieldPhoneStatus.Ringing => Loc.GetString("dh-phone-ui-ringing", ("other", other)),
            DHFieldPhoneStatus.InCall => Loc.GetString("dh-phone-ui-in-call", ("other", other)),
            _ => Loc.GetString("dh-phone-ui-idle"),
        };
        _status.FontColorOverride = state.Status switch
        {
            DHFieldPhoneStatus.InCall => Ok,
            DHFieldPhoneStatus.Idle => Dim,
            _ => Warn,
        };

        _answer.Disabled = state.Status != DHFieldPhoneStatus.Ringing;
        _hangUp.Disabled = state.Status == DHFieldPhoneStatus.Idle;

        _book.RemoveAllChildren();
        foreach (var entry in state.Book)
        {
            var button = new Button
            {
                Text = entry.Busy ? Loc.GetString("dh-phone-ui-entry-busy", ("name", entry.Name)) : entry.Name,
                Disabled = entry.Busy || state.Status != DHFieldPhoneStatus.Idle,
                HorizontalExpand = true,
            };
            var phone = entry.Phone;
            button.OnPressed += _ => OnCall?.Invoke(phone);
            _book.AddChild(button);
        }

        if (state.Book.Count == 0)
            _book.AddChild(new Label { Text = Loc.GetString("dh-phone-ui-empty"), FontColorOverride = Dim });
    }
}
