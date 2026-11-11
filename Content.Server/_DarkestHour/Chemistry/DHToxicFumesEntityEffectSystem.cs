using Content.Server.Fluids.EntitySystems;
using Content.Server.Spreader;
using Content.Shared._DarkestHour.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Chemistry;

/// <inheritdoc cref="DHToxicFumes"/>
public sealed partial class DHToxicFumesEntityEffectSystem : EntityEffectSystem<SolutionComponent, DHToxicFumes>
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SmokeSystem _smoke = default!;
    [Dependency] private SpreaderSystem _spreader = default!;
    [Dependency] private TurfSystem _turf = default!;

    private static readonly EntProtoId SmokePrototype = "Smoke";

    protected override void Effect(Entity<SolutionComponent> entity, ref EntityEffectEvent<DHToxicFumes> args)
    {
        var effect = args.Effect;
        if (args.Scale < effect.MinScale)
            return;

        var xform = Transform(entity);
        var mapCoords = _xform.GetMapCoordinates(entity, xform);
        if (!_map.TryFindGridAt(mapCoords, out var gridUid, out var grid) ||
            !_map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tileRef) ||
            _spreader.RequiresFloorToSpread(SmokePrototype.ToString()) && _turf.IsSpace(tileRef))
        {
            return;
        }

        var fumes = new Solution();
        fumes.AddReagent(effect.Reagent, FixedPoint2.New(args.Scale * effect.Ratio));

        var spread = (int) Math.Max(0, Math.Ceiling(args.Scale / effect.OverflowThreshold));
        var smoke = Spawn(SmokePrototype, _map.MapToGrid(gridUid, mapCoords).SnapToGrid());
        _smoke.StartSmoke(smoke, fumes, effect.Duration, spread);

        _audio.PlayPvs(effect.Sound, entity, AudioParams.Default.WithVariation(0.25f));
        _popup.PopupCoordinates(Loc.GetString("dh-chemistry-toxic-fumes"), xform.Coordinates, PopupType.MediumCaution);
    }
}
