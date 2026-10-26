using System.Numerics;
using Content.Shared._DarkestHour.Recon;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stealth.Components;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Recon;

/// <summary>Носитель получил невидимость от маскхалата (снимается вместе с ним).</summary>
[RegisterComponent]
public sealed partial class DHCamoAppliedComponent : Component
{
}

/// <summary>
/// Детектор движения (отметки движущихся рядом — владельцу), маскхалат (невидимость, пока стоишь) и маскировочная
/// сеть (развернуть над собой, свернуть). Орбитальный скан — в DHHeavyGunSystem.Recon.
/// </summary>
public sealed partial class DHReconSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly SoundSpecifier ToggleSound = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg");
    private static readonly SoundSpecifier PingSound = new SoundPathSpecifier("/Audio/Machines/beep.ogg",
        AudioParams.Default.WithVolume(-8f));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHMotionDetectorComponent, UseInHandEvent>(OnDetectorUse);

        SubscribeLocalEvent<DHCamoCloakComponent, GotEquippedEvent>(OnCloakEquipped);
        SubscribeLocalEvent<DHCamoCloakComponent, GotUnequippedEvent>(OnCloakUnequipped);

        SubscribeLocalEvent<DHCamoNetFoldedComponent, UseInHandEvent>(OnNetUse);
        SubscribeLocalEvent<DHCamoNetFoldedComponent, DHCamoNetDeployDoAfterEvent>(OnNetDeployed);
        SubscribeLocalEvent<DHCamoNetComponent, GetVerbsEvent<AlternativeVerb>>(OnNetVerbs);
        SubscribeLocalEvent<DHCamoNetComponent, DHCamoNetFoldDoAfterEvent>(OnNetFolded);
    }

    #region Детектор движения

    private void OnDetectorUse(Entity<DHMotionDetectorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Active = !ent.Comp.Active;
        _appearance.SetData(ent, DHMotionDetectorVisuals.Active, ent.Comp.Active);
        _audio.PlayPvs(ToggleSound, ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Active ? "dh-motion-detector-on" : "dh-motion-detector-off"),
            args.User, args.User);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHMotionDetectorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var detector, out var xform))
        {
            if (!detector.Active || now < detector.NextScan)
                continue;

            detector.NextScan = now + detector.Interval;

            // Работает в руках или на поясе: родитель — живой игрок.
            var holder = xform.ParentUid;
            if (!TryComp<ActorComponent>(holder, out var actor))
                continue;

            var center = _transform.GetMapCoordinates(holder);
            var blips = new List<Vector2>();
            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(center, detector.Range))
            {
                if (mob.Owner == holder || !TryComp<PhysicsComponent>(mob, out var physics) ||
                    physics.LinearVelocity.Length() < detector.MinSpeed)
                {
                    continue;
                }

                blips.Add(_transform.GetWorldPosition(mob));
            }

            if (blips.Count == 0)
                continue;

            RaiseNetworkEvent(new DHMotionBlipsEvent(blips), actor.PlayerSession);
            _audio.PlayEntity(PingSound, Filter.SinglePlayer(actor.PlayerSession), uid, false);
        }
    }

    #endregion

    #region Маскхалат

    private void OnCloakEquipped(Entity<DHCamoCloakComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & Content.Shared.Inventory.SlotFlags.OUTERCLOTHING) == 0 ||
            HasComp<StealthComponent>(args.EquipTarget) || !_proto.TryIndex(ent.Comp.Preset, out var preset))
        {
            return;
        }

        EntityManager.AddComponents(args.EquipTarget, preset, removeExisting: false);
        EnsureComp<DHCamoAppliedComponent>(args.EquipTarget);
    }

    private void OnCloakUnequipped(Entity<DHCamoCloakComponent> ent, ref GotUnequippedEvent args)
    {
        if (!RemComp<DHCamoAppliedComponent>(args.EquipTarget))
            return;

        RemComp<StealthOnMoveComponent>(args.EquipTarget);
        RemComp<StealthComponent>(args.EquipTarget);
    }

    #endregion

    #region Маскировочная сеть

    private void OnNetUse(Entity<DHCamoNetFoldedComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.DeployTime,
            new DHCamoNetDeployDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnNetDeployed(Entity<DHCamoNetFoldedComponent> ent, ref DHCamoNetDeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var userXform = Transform(args.User);
        var coordinates = userXform.Coordinates;
        if (userXform.GridUid is { } grid && TryComp<MapGridComponent>(grid, out var gridComp))
            coordinates = _map.GridTileToLocal(grid, gridComp, _map.TileIndicesFor(grid, gridComp, coordinates));

        var net = Spawn(ent.Comp.Net, coordinates);
        _transform.AnchorEntity(net, Transform(net));
        _popup.PopupEntity(Loc.GetString("dh-camo-net-deployed", ("user", args.User)), net, PopupType.Small);
        QueueDel(ent);
    }

    private void OnNetVerbs(Entity<DHCamoNetComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dh-camo-net-fold-verb"),
            Act = () => _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(4),
                new DHCamoNetFoldDoAfterEvent(), ent, target: ent)
            {
                BreakOnMove = true,
                NeedHand = true,
            }),
        });
    }

    private void OnNetFolded(Entity<DHCamoNetComponent> ent, ref DHCamoNetFoldDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var folded = Spawn(ent.Comp.Folded, _transform.GetMapCoordinates(ent));
        _hands.PickupOrDrop(args.User, folded);
        QueueDel(ent);
    }

    #endregion
}
