using Content.Shared._DarkestHour.Fortifications;
using Content.Shared.Construction;
using JetBrains.Annotations;

namespace Content.Server._DarkestHour.Fortifications;

/// <summary>Действие графа строительства: при разборке конструкции с колючкой моток падает рядом.</summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class DHDropWire : IGraphAction
{
    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
        if (!entityManager.TryGetComponent(uid, out DHWireableComponent? wireable) || !wireable.Wired)
            return;

        var coordinates = entityManager.GetComponent<TransformComponent>(uid).Coordinates;
        entityManager.SpawnEntity(wireable.Wire, coordinates);
        wireable.Wired = false;
    }
}
