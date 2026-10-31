using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Shuttles.Rift;

namespace Content.Server.Explosion.EntitySystems;

/// <summary>DarkestHour: поверхность планеты — под землёй не космос, тайлы взрывом до него не разбиваются.</summary>
public sealed partial class ExplosionSystem
{
    private bool DHIsPlanetSurface(EntityUid grid)
    {
        if (HasComp<DHMappedSurfaceComponent>(grid))
            return true;

        return Transform(grid).MapUid is { } map && HasComp<DHPlanetSurfaceMapComponent>(map);
    }
}
