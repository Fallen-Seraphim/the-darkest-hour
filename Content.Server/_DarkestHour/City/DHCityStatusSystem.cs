using Content.Server.Chat.Systems;
using Content.Shared._DarkestHour.City;
using Content.Shared._DarkestHour.Overwatch;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.City;

/// <summary>
/// Обстановка в городе (обычная, комендантский час, ЧС) и компьютеры связи района и ВП: объявления на весь город
/// с перерывом, смена обстановки — только с компьютера ВП. Клиенты узнают обстановку событием при смене и при входе.
/// </summary>
public sealed partial class DHCityStatusSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const int MaxLength = 500;

    private static readonly SoundSpecifier StatusSound = new SoundPathSpecifier("/Audio/Misc/notice1.ogg");

    public DHCityStatus Status { get; private set; } = DHCityStatus.Normal;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<DHCityCommsComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<DHCityCommsComponent, DHCityCommsAnnounceMessage>(OnAnnounce);
        SubscribeLocalEvent<DHCityCommsComponent, DHCityCommsSetStatusMessage>(OnSetStatus);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        Status = DHCityStatus.Normal;
        RaiseNetworkEvent(new DHCityStatusEvent(Status));
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        RaiseNetworkEvent(new DHCityStatusEvent(Status), ev.Player);
    }

    private void OnOpened(Entity<DHCityCommsComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnAnnounce(Entity<DHCityCommsComponent> ent, ref DHCityCommsAnnounceMessage args)
    {
        var text = args.Text.Trim();
        if (text.Length == 0)
            return;

        if (_timing.CurTime < ent.Comp.NextAnnounce)
        {
            _popup.PopupEntity(Loc.GetString("dh-city-comms-cooldown"), ent, args.Actor);
            return;
        }

        if (text.Length > MaxLength)
            text = text[..MaxLength];

        ent.Comp.NextAnnounce = _timing.CurTime + ent.Comp.Cooldown;
        _chat.DispatchGlobalAnnouncement(text, Loc.GetString(ent.Comp.Sender), true, null, ent.Comp.Color);
        UpdateUi(ent);
    }

    private void OnSetStatus(Entity<DHCityCommsComponent> ent, ref DHCityCommsSetStatusMessage args)
    {
        if (!ent.Comp.CanSetStatus || args.Status == Status)
            return;

        SetStatus(args.Status, Loc.GetString(ent.Comp.Sender));
        UpdateUi(ent);
    }

    /// <summary>Новая обстановка: объявление в чат и на экран, телефоны и терминалы показывают её рядом со временем.</summary>
    public void SetStatus(DHCityStatus status, string sender)
    {
        Status = status;
        RaiseNetworkEvent(new DHCityStatusEvent(status));

        var name = Loc.GetString(DHCityStatuses.Name(status));
        var color = DHCityStatuses.TextColor(status);
        var text = Loc.GetString(status switch
        {
            DHCityStatus.Curfew => "dh-city-status-announce-curfew",
            DHCityStatus.Emergency => "dh-city-status-announce-emergency",
            _ => "dh-city-status-announce-normal",
        });

        _chat.DispatchGlobalAnnouncement(text, sender, true, StatusSound, color);
        RaiseNetworkEvent(new DHScreenAnnouncementEvent(sender, name, color));
    }

    private void UpdateUi(Entity<DHCityCommsComponent> ent)
    {
        var left = ent.Comp.NextAnnounce - _timing.CurTime;
        var cooldown = left > TimeSpan.Zero ? (int) Math.Ceiling(left.TotalSeconds) : 0;
        _ui.SetUiState(ent.Owner, DHCityCommsUiKey.Key,
            new DHCityCommsState(Loc.GetString(ent.Comp.Sender), Status, ent.Comp.CanSetStatus, cooldown));
    }
}
