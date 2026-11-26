using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Announcements;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Announcements;

/// <summary>Объявления стороны: только своим, с паузой между объявлениями; доступ — по считывателю компьютера.</summary>
public sealed partial class DHFactionAnnouncerSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private DHAssaultRuleSystem _assault = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<DHFactionAnnouncerComponent>(DHFactionAnnouncerUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>((ent, ref _) => UpdateUi(ent));
            subs.Event<DHFactionAnnounceMessage>(OnAnnounce);
        });
    }

    private void OnAnnounce(Entity<DHFactionAnnouncerComponent> ent, ref DHFactionAnnounceMessage args)
    {
        var user = args.Actor;
        if (!_access.IsAllowed(user, ent))
        {
            _popup.PopupEntity(Loc.GetString("dh-faction-announce-denied"), ent, user);
            return;
        }

        var now = _timing.CurTime;
        if (now < ent.Comp.NextAnnounce)
            return;

        var text = SharedChatSystem.SanitizeAnnouncement(args.Text.Trim(), ent.Comp.MaxLength);
        if (text.Length == 0)
            return;

        ent.Comp.NextAnnounce = now + ent.Comp.Cooldown;
        var sender = Loc.GetString(ent.Comp.Sender, ("name", Name(user)));
        _assault.AnnounceToFaction(ent.Comp.Faction, sender, text, ent.Comp.Sound, onScreen: true);
        UpdateUi(ent);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextRefresh)
            return;

        _nextRefresh = now + TimeSpan.FromSeconds(1);
        var query = EntityQueryEnumerator<DHFactionAnnouncerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_ui.IsUiOpen(uid, DHFactionAnnouncerUiKey.Key))
                UpdateUi((uid, comp));
        }
    }

    private void UpdateUi(Entity<DHFactionAnnouncerComponent> ent)
    {
        var left = (int) Math.Ceiling(Math.Max(0, (ent.Comp.NextAnnounce - _timing.CurTime).TotalSeconds));
        _ui.SetUiState(ent.Owner, DHFactionAnnouncerUiKey.Key,
            new DHFactionAnnouncerState(ent.Comp.Faction, left, ent.Comp.MaxLength));
    }
}
