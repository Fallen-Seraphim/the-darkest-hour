using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;

namespace Content.Shared._DarkestHour.Flags;

public sealed partial class DHPlantableFlagSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHPlantableFlagComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<DHPlantableFlagComponent, DHPlantFlagDoAfterEvent>(OnPlanted);
        SubscribeLocalEvent<DHPlantableFlagComponent, DHRemoveFlagDoAfterEvent>(OnRemoved);
        SubscribeLocalEvent<DHPlantableFlagComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnUseInHand(Entity<DHPlantableFlagComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!CanPlant(args.User, out _))
        {
            _popup.PopupClient(Loc.GetString("dh-flag-cant-plant"), args.User, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.PlantDelay, new DHPlantFlagDoAfterEvent(),
            ent, ent, ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _audio.PlayPredicted(ent.Comp.RaiseStartSound, ent, args.User);
    }

    /// <summary>Клетка под ногами: твёрдый пол, не космос.</summary>
    private bool CanPlant(EntityUid user, out EntityCoordinates target)
    {
        target = default;
        var xform = Transform(user);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var tile = _map.GetTileRef(grid, gridComp, xform.Coordinates);
        if (_turf.IsSpace(tile))
            return false;

        target = _map.GridTileToLocal(grid, gridComp, tile.GridIndices);
        return true;
    }

    private void OnPlanted(Entity<DHPlantableFlagComponent> ent, ref DHPlantFlagDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        if (!CanPlant(args.User, out var target))
            return;

        _transform.SetCoordinates(ent, target);
        _transform.SetLocalRotation(ent, Angle.Zero);
        _transform.AnchorEntity(ent);
        _appearance.SetData(ent, DHPlantableFlagVisuals.Planted, true);

        if (_net.IsServer)
            _audio.PlayPvs(ent.Comp.RaiseEndSound, ent);
    }

    private void OnGetVerbs(Entity<DHPlantableFlagComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || !Transform(ent).Anchored)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("dh-flag-remove-verb"),
            Act = () =>
            {
                var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.RemoveDelay,
                    new DHRemoveFlagDoAfterEvent(), ent, ent)
                {
                    BreakOnMove = true,
                    NeedHand = true,
                };

                if (_doAfter.TryStartDoAfter(doAfter))
                    _audio.PlayPredicted(ent.Comp.LowerStartSound, ent, user);
            },
        });
    }

    private void OnRemoved(Entity<DHPlantableFlagComponent> ent, ref DHRemoveFlagDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !Transform(ent).Anchored)
            return;

        args.Handled = true;
        _transform.Unanchor(ent);
        _appearance.SetData(ent, DHPlantableFlagVisuals.Planted, false);
        _hands.PickupOrDrop(args.User, ent);
    }
}
