using Content.Server.Construction;
using Content.Server.Stack;
using Content.Shared._DarkestHour.Fortifications;
using Content.Shared.Climbing.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Fortifications;

/// <summary>
/// Колючая проволока на сетке-рабице. Отдельная система: подписка на InteractUsing должна идти раньше
/// ConstructionSystem (иначе кусачки режут сам забор), а у одной системы все подписки на одно событие
/// обязаны иметь одинаковый порядок.
/// </summary>
public sealed partial class DHWireableSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    private static readonly ProtoId<ToolQualityPrototype> CuttingQuality = "Cutting";
    private static readonly SoundSpecifier WireSound = new SoundPathSpecifier("/Audio/_RMC14/Items/barbed_wire_movement.ogg",
        AudioParams.Default.WithVariation(0.35f));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHWireableComponent, InteractUsingEvent>(OnWireableInteractUsing,
            before: new[] { typeof(ConstructionSystem) });
        SubscribeLocalEvent<DHWireableComponent, DHWireOnDoAfterEvent>(OnWireableWired);
        SubscribeLocalEvent<DHWireableComponent, DHWireOffDoAfterEvent>(OnWireableUnwired);
        SubscribeLocalEvent<DHWireableComponent, ClimbedOnEvent>(OnWireableClimbed);
        SubscribeLocalEvent<DHWireableComponent, ConstructionChangeEntityEvent>(OnWireableChanged);
    }

    private void OnWireableInteractUsing(Entity<DHWireableComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<DHBarbedWireComponent>(args.Used, out var wire))
        {
            args.Handled = true;
            if (ent.Comp.Wired)
            {
                _popup.PopupEntity(Loc.GetString("dh-wireable-already-wired"), ent, args.User);
                return;
            }

            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, wire.WireTime,
                new DHWireOnDoAfterEvent(), ent, ent, args.Used)
            {
                BreakOnMove = true,
                NeedHand = true,
            });
            return;
        }

        // С колючкой кусачки сначала срезают её, а не сам забор.
        if (ent.Comp.Wired && _tool.HasQuality(args.Used, CuttingQuality.Id))
            args.Handled = _tool.UseTool(args.Used, args.User, ent, 2f, CuttingQuality, new DHWireOffDoAfterEvent());
    }

    private void OnWireableWired(Entity<DHWireableComponent> ent, ref DHWireOnDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Wired || args.Used is not { } wire || !_stack.TryUse(wire, 1))
            return;

        args.Handled = true;
        SetWireableWired(ent, true);
        _audio.PlayPvs(WireSound, ent);
        _popup.PopupEntity(Loc.GetString("dh-wireable-wired", ("user", args.User)), ent, PopupType.Small);
    }

    private void OnWireableUnwired(Entity<DHWireableComponent> ent, ref DHWireOffDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !ent.Comp.Wired)
            return;

        args.Handled = true;
        SetWireableWired(ent, false);
        _hands.PickupOrDrop(args.User, Spawn(ent.Comp.Wire, Transform(ent).Coordinates));
    }

    private void SetWireableWired(Entity<DHWireableComponent> ent, bool wired)
    {
        ent.Comp.Wired = wired;
        _appearance.SetData(ent, DHWireVisuals.Wired, wired);
    }

    private void OnWireableClimbed(Entity<DHWireableComponent> ent, ref ClimbedOnEvent args)
    {
        if (!ent.Comp.Wired)
            return;

        _damageable.TryChangeDamage(args.Climber, ent.Comp.WireDamage, origin: ent.Owner);
        _audio.PlayPvs(WireSound, ent);
        _popup.PopupEntity(Loc.GetString("dh-sandbag-wire-cut"), args.Climber, args.Climber, PopupType.SmallCaution);
    }

    /// <summary>Забор сломали (стал обломком) или перестроили: колючка переходит на новый или падает мотком.</summary>
    // Событие-класс (не by-ref): обработчик со старой сигнатурой (uid, comp, args).
    private void OnWireableChanged(EntityUid uid, DHWireableComponent comp, ConstructionChangeEntityEvent args)
    {
        var ent = new Entity<DHWireableComponent>(uid, comp);
        if (!ent.Comp.Wired)
            return;

        if (TryComp<DHWireableComponent>(args.New, out var next))
            SetWireableWired((args.New, next), true);
        else
            Spawn(ent.Comp.Wire, Transform(ent).Coordinates);
    }
}
