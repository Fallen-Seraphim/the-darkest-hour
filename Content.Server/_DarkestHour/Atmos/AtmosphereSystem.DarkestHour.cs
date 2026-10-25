using Content.Shared.Atmos;

namespace Content.Server.Atmos.EntitySystems;

// DarkestHour: своя атмосфера «за бортом» отдельного грида (корабль в облаках газового гиганта), поверх атмосферы
// карты. Подставляется в тайлы грида, открытые наружу (AtmosphereSystem.Processing, UpdateTileData).
public sealed partial class AtmosphereSystem
{
    private (GasMixture Air, bool IsSpace)? DHGridMapAtmosphere(EntityUid grid)
    {
        if (!TryComp<DHGridMapAtmosphereComponent>(grid, out var comp) || comp.Mixture == null)
            return null;

        return (comp.Mixture, false);
    }

    /// <summary>
    /// Задаёт гриду газ «за бортом» (null — вернуть атмосферу карты) и обновляет открытые наружу тайлы.
    /// </summary>
    public void SetGridOutsideMixture(EntityUid grid, GasMixture? mixture)
    {
        if (mixture == null)
        {
            if (!RemComp<DHGridMapAtmosphereComponent>(grid))
                return;
        }
        else
        {
            if (!mixture.Immutable)
            {
                mixture = mixture.Clone();
                mixture.MarkImmutable();
            }

            EnsureComp<DHGridMapAtmosphereComponent>(grid).Mixture = mixture;
        }

        RefreshMapAtmosphereTiles(grid);
    }
}

/// <summary>Газ «за бортом» грида вместо атмосферы карты (DarkestHour).</summary>
[RegisterComponent]
public sealed partial class DHGridMapAtmosphereComponent : Component
{
    [ViewVariables]
    public GasMixture? Mixture;
}
