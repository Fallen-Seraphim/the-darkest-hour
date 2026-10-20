using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Spawners.Components;

[RegisterComponent]
public sealed partial class SpawnPointComponent : Component, ISpawnPoint
{
    /// <summary>
    /// The job this spawn point is valid for.
    /// Null will allow all jobs to spawn here.
    /// </summary>
    [DataField("job_id")]
    public ProtoId<JobPrototype>? Job;

    // DarkestHour: точка появления может обслуживать несколько должностей — наши должности
    // используют ванильные точки на картах, пока у нас нет своих карт.
    /// <summary>
    /// Additional jobs that may also spawn at this point.
    /// </summary>
    [DataField]
    public List<ProtoId<JobPrototype>> Jobs = new();

    /// <summary>
    /// The type of spawn point.
    /// </summary>
    [DataField("spawn_type"), ViewVariables(VVAccess.ReadWrite)]
    public SpawnPointType SpawnType { get; set; } = SpawnPointType.Unset;

    public override string ToString()
    {
        return $"{Job} {SpawnType}";
    }
}

public enum SpawnPointType
{
    Unset = 0,
    LateJoin,
    Job,
    Observer,
}
