using Content.Shared._DarkestHour.Comms;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Comms;

/// <summary>
/// Нейтральная антенна ЗП-1 «Восстания»: канал ОФСК молчит, пока её не взломают взломщиком (у главы отряда);
/// военпол может восстановить её мультитулом.
/// </summary>
public sealed partial class DHRelayHackSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedToolSystem _tools = default!;

    private static readonly ProtoId<ToolQualityPrototype> PulsingQuality = "Pulsing";
    private static readonly SoundSpecifier HackSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHHackableRelayComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DHHackableRelayComponent, DHRelayHackDoAfterEvent>(OnHacked);
        SubscribeLocalEvent<DHHackableRelayComponent, DHRelayRestoreDoAfterEvent>(OnRestored);
        SubscribeLocalEvent<DHHackableRelayComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractUsing(Entity<DHHackableRelayComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<DHRelayHackerComponent>(args.Used))
        {
            args.Handled = true;
            if (ent.Comp.Hacked)
            {
                _popup.PopupEntity(Loc.GetString("dh-relay-hack-already"), ent, args.User);
                return;
            }

            _popup.PopupEntity(Loc.GetString("dh-relay-hack-start"), ent, args.User);
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.HackTime,
                new DHRelayHackDoAfterEvent(), ent, target: ent, used: args.Used)
            {
                BreakOnMove = true,
                NeedHand = true,
            });
            return;
        }

        if (ent.Comp.Hacked && _tools.HasQuality(args.Used, PulsingQuality.Id))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("dh-relay-restore-start"), ent, args.User);
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.RestoreTime,
                new DHRelayRestoreDoAfterEvent(), ent, target: ent, used: args.Used)
            {
                BreakOnMove = true,
                NeedHand = true,
            });
        }
    }

    private void OnHacked(Entity<DHHackableRelayComponent> ent, ref DHRelayHackDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Hacked)
            return;

        args.Handled = true;
        ent.Comp.Hacked = true;
        _audio.PlayPvs(HackSound, ent);
        _popup.PopupEntity(Loc.GetString("dh-relay-hacked"), ent, PopupType.Medium);
    }

    private void OnRestored(Entity<DHHackableRelayComponent> ent, ref DHRelayRestoreDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !ent.Comp.Hacked)
            return;

        args.Handled = true;
        ent.Comp.Hacked = false;
        _audio.PlayPvs(HackSound, ent);
        _popup.PopupEntity(Loc.GetString("dh-relay-restored"), ent, PopupType.Medium);
    }

    private void OnExamined(Entity<DHHackableRelayComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Hacked ? "dh-relay-examine-hacked" : "dh-relay-examine-sealed"));
    }
}
