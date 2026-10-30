using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Fortifications;
using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.Burial.Components;
using Content.Shared.DoAfter;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Fortifications;

/// <summary>
/// Окопы (решение автора 2026-10-30): копать лопатой по клетке голой земли рядом с собой, засыпать — лопатой по окопу.
/// Взрыв не в окопе бьёт по сидящим в нём слабее (<see cref="DHTrenchComponent.ExplosionCoefficient"/>); пули — см.
/// <see cref="DHTrenchCoverSystem"/>.
/// </summary>
public sealed partial class DHTrenchSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private DHGroundSystem _ground = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DHTrenchCoverSystem _trench = default!;
    [Dependency] private TurfSystem _turf = default!;

    private static readonly EntProtoId TrenchProto = "DHTrench";
    private static readonly SoundSpecifier DigSound = new SoundPathSpecifier("/Audio/Items/shovel_dig.ogg");
    private static readonly TimeSpan DigTime = TimeSpan.FromSeconds(6);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHEntrenchingToolComponent, AfterInteractEvent>(OnShovelAfterInteract);
        SubscribeLocalEvent<ShovelComponent, DHTrenchDigDoAfterEvent>(OnDigDoAfter);
        SubscribeLocalEvent<DHTrenchComponent, InteractUsingEvent>(OnTrenchInteractUsing);
        SubscribeLocalEvent<DHTrenchComponent, DHTrenchFillDoAfterEvent>(OnFillDoAfter);
        SubscribeLocalEvent<DHTraumaComponent, GetExplosionResistanceEvent>(OnExplosionResistance);
    }

    #region Копать и засыпать

    private void OnShovelAfterInteract(Entity<DHEntrenchingToolComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target != null)
            return;

        var coordinates = args.ClickLocation;
        if (!CanDig(coordinates, args.User, out var tileCenter))
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, DigTime,
            new DHTrenchDigDoAfterEvent(GetNetCoordinates(tileCenter)), ent, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
        _audio.PlayPvs(DigSound, tileCenter);
    }

    /// <summary>Клетка голой земли, без окопа и без стен/мебели.</summary>
    private bool CanDig(EntityCoordinates coordinates, EntityUid user, out EntityCoordinates tileCenter)
    {
        tileCenter = default;
        if (_transform.GetGrid(coordinates) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var tile = _map.TileIndicesFor(gridUid, grid, coordinates);
        tileCenter = _map.GridTileToLocal(gridUid, grid, tile);

        if (!_ground.IsGroundAt(tileCenter))
        {
            _popup.PopupEntity(Loc.GetString("dh-trench-need-ground"), user, user);
            return false;
        }

        if (_trench.TrenchAt(tileCenter) != null)
            return false;

        if (_turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable | CollisionGroup.MidImpassable, grid))
        {
            _popup.PopupEntity(Loc.GetString("dh-trench-blocked"), user, user);
            return false;
        }

        return true;
    }

    private void OnDigDoAfter(Entity<ShovelComponent> ent, ref DHTrenchDigDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var coordinates = GetCoordinates(args.Coordinates);
        if (!CanDig(coordinates, args.User, out var tileCenter))
            return;

        var trench = Spawn(TrenchProto, tileCenter);
        var xform = Transform(trench);
        if (!xform.Anchored)
            _transform.AnchorEntity((trench, xform));

        _audio.PlayPvs(DigSound, tileCenter);
    }

    private void OnTrenchInteractUsing(Entity<DHTrenchComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<ShovelComponent>(args.Used))
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.FillTime,
            new DHTrenchFillDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
        _audio.PlayPvs(DigSound, ent);
    }

    private void OnFillDoAfter(Entity<DHTrenchComponent> ent, ref DHTrenchFillDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        _audio.PlayPvs(DigSound, Transform(ent).Coordinates);
        QueueDel(ent);
    }

    #endregion

    #region Взрывы

    private void OnExplosionResistance(Entity<DHTraumaComponent> ent, ref GetExplosionResistanceEvent args)
    {
        if (_trench.TrenchOf(ent) is not { } trench)
            return;

        // Взрыв в самом окопе (эпицентр в окопе рядом с бойцом) бьёт в полную силу.
        var position = _transform.GetMapCoordinates(ent);
        var explosions = EntityQueryEnumerator<ExplosionVisualsComponent>();
        while (explosions.MoveNext(out var visuals))
        {
            var epicenter = visuals.Epicenter;
            if (epicenter.MapId != position.MapId ||
                (epicenter.Position - position.Position).Length() > trench.Comp.ExplosionInsideRange + 0.5f)
            {
                continue;
            }

            if (_trench.TrenchAt(_transform.ToCoordinates(epicenter)) != null)
                return;
        }

        args.DamageCoefficient *= trench.Comp.ExplosionCoefficient;
    }

    #endregion
}
