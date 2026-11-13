using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Roles;

/// <summary>
/// Выдаёт появившемуся игроку компоненты из <see cref="DepartmentPrototype.DHComponents"/>
/// всех отделов, в которые входит его должность (метки фракционных эмоций и т.п.).
/// </summary>
public sealed partial class DHDepartmentComponentsSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId is not { } jobId || TerminatingOrDeleted(ev.Mob))
            return;

        foreach (var department in _prototypes.EnumeratePrototypes<DepartmentPrototype>())
        {
            if (department.DHComponents is not { Count: > 0 } components)
                continue;

            if (!department.Roles.Contains(jobId))
                continue;

            EntityManager.AddComponents(ev.Mob, components, removeExisting: false);
        }
    }
}
