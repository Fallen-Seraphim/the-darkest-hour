using Content.Shared.Access.Components;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.City;

// «Восстание», ключи от квартир (решение автора 2026-11-05): щёлкнул ключом по двери — она заперта. Первая дверь,
// по которой щёлкнули, запоминается ключом на весь раунд: дальше ключ запирает и отпирает только её.

/// <summary>Ключ от квартиры: дверь, к которой он подошёл (первая, по которой им щёлкнули).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHDoorKeyComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Door;

    [DataField]
    public SoundSpecifier LockSound = new SoundPathSpecifier("/Audio/Machines/door_lock_on.ogg");

    [DataField]
    public SoundSpecifier UnlockSound = new SoundPathSpecifier("/Audio/Machines/door_lock_off.ogg");
}

/// <summary>Дверь под ключ: запертая не открывается.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHKeyLockedComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Locked;
}

public sealed partial class DHDoorKeySystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHDoorKeyComponent, AfterInteractEvent>(OnKeyInteract);
        SubscribeLocalEvent<DHDoorKeyComponent, ExaminedEvent>(OnKeyExamined);
        SubscribeLocalEvent<DHKeyLockedComponent, BeforeDoorOpenedEvent>(OnBeforeOpen);
        SubscribeLocalEvent<DHKeyLockedComponent, ExaminedEvent>(OnDoorExamined);
    }

    private void OnKeyInteract(Entity<DHDoorKeyComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target ||
            !TryComp<DoorComponent>(target, out var door))
        {
            return;
        }

        args.Handled = true;

        // Двери с доступом (шлюзы военпола, LA-V, служб) ключом не запираются — только квартирные.
        if (HasComp<AccessReaderComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("dh-key-not-a-lock"), target, args.User);
            return;
        }

        if (ent.Comp.Door is { } bound && bound != target)
        {
            _popup.PopupClient(Loc.GetString("dh-key-wrong-door"), target, args.User);
            return;
        }

        var locked = EnsureComp<DHKeyLockedComponent>(target);
        if (!locked.Locked && door.State != DoorState.Closed)
        {
            _popup.PopupClient(Loc.GetString("dh-key-close-first"), target, args.User);
            return;
        }

        if (ent.Comp.Door == null)
        {
            ent.Comp.Door = target;
            Dirty(ent);
        }

        locked.Locked = !locked.Locked;
        Dirty(target, locked);

        _audio.PlayPredicted(locked.Locked ? ent.Comp.LockSound : ent.Comp.UnlockSound, target, args.User);
        _popup.PopupClient(Loc.GetString(locked.Locked ? "dh-key-locked" : "dh-key-unlocked"), target, args.User);
    }

    private void OnKeyExamined(Entity<DHDoorKeyComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Door == null ? "dh-key-examine-unbound" : "dh-key-examine-bound"));
    }

    private void OnBeforeOpen(Entity<DHKeyLockedComponent> ent, ref BeforeDoorOpenedEvent args)
    {
        if (!ent.Comp.Locked)
            return;

        args.Cancel();
        if (args.User is { } user)
            _popup.PopupClient(Loc.GetString("dh-key-door-locked"), ent, user);
    }

    private void OnDoorExamined(Entity<DHKeyLockedComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Locked)
            args.PushMarkup(Loc.GetString("dh-key-door-examine-locked"));
    }
}
