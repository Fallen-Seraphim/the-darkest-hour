using Content.Shared._DarkestHour.Fluids;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;

namespace Content.Server._DarkestHour.Fluids;

/// <summary>Набрать воды из реки: ёмкостью по клетке воды. И проверка «на этой клетке вода» для огня.</summary>
public sealed partial class DHWaterSourceSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    private static readonly SoundSpecifier FillSound = new SoundPathSpecifier("/Audio/Effects/Fluids/watersplash.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHWaterSourceComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<DHWaterSourceComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_solution.TryGetRefillableSolution(args.Used, out var soln, out var solution))
            return;

        args.Handled = true;
        var space = solution.AvailableVolume;
        if (space <= 0)
        {
            _popup.PopupEntity(Loc.GetString("dh-water-source-full", ("container", args.Used)), args.User, args.User);
            return;
        }

        _solution.TryAddReagent(soln.Value, ent.Comp.Reagent, space, out var accepted);
        _audio.PlayPvs(FillSound, ent);
        _popup.PopupEntity(Loc.GetString("dh-water-source-filled", ("container", args.Used), ("amount", accepted)),
            args.User, args.User);
    }

    /// <summary>На клетке грида — природная вода.</summary>
    public bool IsWaterTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<DHWaterSourceComponent>(uid))
                return true;
        }

        return false;
    }
}
