using Content.Shared._DarkestHour.Fortifications;
using Content.Shared.Directions;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._DarkestHour.Fortifications;

/// <summary>
/// Сцепка складных баррикад-ворот (порт RMCFoldingBarricadeLinkingSystem RMC-14, MIT; решение автора 2026-11-12).
/// Пласталевые ворота можно сцепить (альтернативное действие): сцепленные соседи в одном ряду и с тем же
/// направлением открываются и закрываются вместе, на стыках рисуются соединения.
/// </summary>
public sealed partial class DHFoldingGateSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly Direction[] Cardinals = { Direction.North, Direction.South, Direction.East, Direction.West };
    private static readonly SoundSpecifier ToggleSound = new SoundPathSpecifier("/Audio/Items/crowbar.ogg");

    private bool _propagating;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHFoldingGateComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DHFoldingGateComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<DHFoldingGateComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeLocalEvent<DHFoldingGateComponent, DoorStateChangedEvent>(OnDoorStateChanged);
        SubscribeLocalEvent<DHFoldingGateComponent, AnchorStateChangedEvent>(OnAnchorChanged);
    }

    private void OnStartup(Entity<DHFoldingGateComponent> ent, ref ComponentStartup args)
    {
        UpdateAround(ent);
    }

    private void OnShutdown(Entity<DHFoldingGateComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.Linked = false;
        UpdateAround(ent);
    }

    private void OnAnchorChanged(Entity<DHFoldingGateComponent> ent, ref AnchorStateChangedEvent args)
    {
        UpdateAround(ent);
    }

    private void OnVerbs(Entity<DHFoldingGateComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !ent.Comp.Linkable || !Transform(ent).Anchored)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Linked ? "dh-folding-gate-unlink-verb" : "dh-folding-gate-link-verb"),
            Act = () =>
            {
                ent.Comp.Linked = !ent.Comp.Linked;
                Dirty(ent);
                _audio.PlayPvs(ToggleSound, ent);
                _popup.PopupEntity(Loc.GetString(ent.Comp.Linked ? "dh-folding-gate-linked" : "dh-folding-gate-unlinked"),
                    ent, user);
                UpdateAround(ent);
            },
        });
    }

    private void OnDoorStateChanged(Entity<DHFoldingGateComponent> ent, ref DoorStateChangedEvent args)
    {
        if (!_propagating)
            Propagate(ent, args.State);

        UpdateAround(ent);
    }

    private bool CanLink(Entity<DHFoldingGateComponent> ent)
    {
        return ent.Comp.Linkable && ent.Comp.Linked && Transform(ent).Anchored;
    }

    /// <summary>Сосед лежит вдоль ряда: ворота смотрят на север/юг — ряд восток–запад, и наоборот.</summary>
    private static bool IsRowDirection(Direction facing, Direction direction)
    {
        return facing is Direction.North or Direction.South
            ? direction is Direction.East or Direction.West
            : direction is Direction.North or Direction.South;
    }

    private void Propagate(Entity<DHFoldingGateComponent> ent, DoorState state)
    {
        if (state is not (DoorState.Opening or DoorState.Open or DoorState.Closing or DoorState.Closed) || !CanLink(ent))
            return;

        var facing = Transform(ent).LocalRotation.GetCardinalDir();
        var visited = new HashSet<EntityUid> { ent.Owner };
        _propagating = true;
        try
        {
            foreach (var direction in Cardinals)
            {
                if (!IsRowDirection(facing, direction))
                    continue;

                var current = ent.Owner;
                while (Neighbor(current, direction) is { } next && visited.Add(next))
                {
                    if (!Apply(next, state))
                        break;

                    current = next;
                }
            }
        }
        finally
        {
            _propagating = false;
        }
    }

    private bool Apply(EntityUid uid, DoorState state)
    {
        if (!TryComp<DoorComponent>(uid, out var door))
            return false;

        if (state is DoorState.Opening or DoorState.Open)
        {
            if (door.State is DoorState.Open or DoorState.Opening)
                return true;

            var sound = door.OpenSound;
            door.OpenSound = null;
            _door.StartOpening(uid, door);
            door.OpenSound = sound;
            return true;
        }

        if (door.State is DoorState.Closed or DoorState.Closing)
            return true;

        var closeSound = door.CloseSound;
        door.CloseSound = null;
        _door.StartClosing(uid, door);
        door.CloseSound = closeSound;
        return true;
    }

    /// <summary>Сцепленный сосед в направлении: те же ворота, закреплены, сцеплены, смотрят туда же.</summary>
    private EntityUid? Neighbor(EntityUid uid, Direction direction)
    {
        if (!TryComp<DHFoldingGateComponent>(uid, out var gate) || !CanLink((uid, gate)))
            return null;

        var xform = Transform(uid);
        var facing = xform.LocalRotation.GetCardinalDir();
        if (!IsRowDirection(facing, direction) || xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return null;
        }

        var tile = _map.LocalToTile(gridUid, grid, xform.Coordinates.Offset(direction));
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var other))
        {
            if (other == uid || !TryComp<DHFoldingGateComponent>(other, out var otherGate) ||
                !CanLink((other.Value, otherGate)) || !HasComp<DoorComponent>(other) ||
                Transform(other.Value).LocalRotation.GetCardinalDir() != facing)
            {
                continue;
            }

            return other;
        }

        return null;
    }

    private void UpdateAround(Entity<DHFoldingGateComponent> ent)
    {
        var xform = Transform(ent);
        UpdateVisuals(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        foreach (var direction in Cardinals)
        {
            var tile = _map.LocalToTile(gridUid, grid, xform.Coordinates.Offset(direction));
            var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
            while (anchored.MoveNext(out var other))
            {
                if (TryComp<DHFoldingGateComponent>(other, out var otherGate))
                    UpdateVisuals((other.Value, otherGate));
            }
        }
    }

    private void UpdateVisuals(Entity<DHFoldingGateComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _appearance.SetData(ent, DHFoldingGateLinkVisuals.North, VisualState(ent, Direction.North));
        _appearance.SetData(ent, DHFoldingGateLinkVisuals.South, VisualState(ent, Direction.South));
        _appearance.SetData(ent, DHFoldingGateLinkVisuals.East, VisualState(ent, Direction.East));
        _appearance.SetData(ent, DHFoldingGateLinkVisuals.West, VisualState(ent, Direction.West));
    }

    private DHFoldingGateLinkState VisualState(Entity<DHFoldingGateComponent> ent, Direction direction)
    {
        if (!TryComp<DoorComponent>(ent, out var door) || Neighbor(ent, direction) == null)
            return DHFoldingGateLinkState.None;

        return door.State is DoorState.Open or DoorState.Opening
            ? DHFoldingGateLinkState.Open
            : DHFoldingGateLinkState.Closed;
    }
}
