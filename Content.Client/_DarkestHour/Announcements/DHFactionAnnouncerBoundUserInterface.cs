using System.Numerics;
using Content.Client._DarkestHour.UI;
using Content.Shared._DarkestHour.Announcements;
using Content.Shared._DarkestHour.Assault;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client._DarkestHour.Announcements;

/// <summary>Окно объявлений стороны: текст, кнопка «Объявить», сколько ждать до следующего.</summary>
[UsedImplicitly]
public sealed partial class DHFactionAnnouncerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DHFactionAnnouncerWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DHFactionAnnouncerWindow>();
        _window.OnAnnounce += text => SendMessage(new DHFactionAnnounceMessage(text));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is DHFactionAnnouncerState cast)
            _window?.UpdateState(cast);
    }
}

public sealed class DHFactionAnnouncerWindow : DefaultWindow
{
    public event Action<string>? OnAnnounce;

    private readonly Label _audience = new();
    private readonly TextEdit _text = new() { HorizontalExpand = true, VerticalExpand = true, MinHeight = 120 };
    private readonly Button _send = new();
    private readonly Label _status = new() { FontColorOverride = Color.FromHex("#9A9A9A") };
    private int _maxLength = 500;

    public DHFactionAnnouncerWindow()
    {
        DHFonts.RegisterTerminal(this);
        Title = Loc.GetString("dh-faction-announce-title");
        MinSize = new Vector2(420, 260);
        SetSize = new Vector2(480, 300);

        _send.Text = Loc.GetString("dh-faction-announce-send");
        _send.OnPressed += _ => Send();

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };
        root.AddChild(_audience);
        var panel = new PanelContainer
        {
            VerticalExpand = true,
            PanelOverride = new Robust.Client.Graphics.StyleBoxFlat { BackgroundColor = Color.FromHex("#0b0d0f") },
        };
        panel.AddChild(_text);
        root.AddChild(panel);
        var bottom = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        _status.HorizontalExpand = true;
        bottom.AddChild(_status);
        bottom.AddChild(_send);
        root.AddChild(bottom);
        Contents.AddChild(root);
    }

    private void Send()
    {
        var text = Rope.Collapse(_text.TextRope).Trim();
        if (text.Length == 0)
            return;

        if (text.Length > _maxLength)
            text = text[.._maxLength];

        OnAnnounce?.Invoke(text);
        _text.TextRope = new Rope.Leaf(string.Empty);
    }

    public void UpdateState(DHFactionAnnouncerState state)
    {
        _maxLength = state.MaxLength;
        _audience.Text = Loc.GetString(state.Faction == DHAssaultFaction.Republic
            ? "dh-faction-announce-audience-republic"
            : "dh-faction-announce-audience-empire");
        _send.Disabled = state.SecondsLeft > 0;
        _status.Text = state.SecondsLeft > 0
            ? Loc.GetString("dh-faction-announce-wait", ("seconds", state.SecondsLeft))
            : Loc.GetString("dh-faction-announce-ready", ("max", state.MaxLength));
    }
}
