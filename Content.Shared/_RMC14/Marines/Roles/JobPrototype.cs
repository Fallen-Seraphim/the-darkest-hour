// Перенесено из RMC-14 (github.com/RMC-14/RMC-14, MIT): звания должности.
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Robust.Shared.Prototypes;

namespace Content.Shared.Roles;

public sealed partial class JobPrototype
{
    /// <summary>
    ///     Ranks of the job, highest first: the first one whose requirements (usually playtime on the job) pass is given.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<RankPrototype>, HashSet<JobRequirement>?>? Ranks;
}
