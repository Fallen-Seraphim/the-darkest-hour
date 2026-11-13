namespace Content.Shared.Roles;

public sealed partial class JobPrototype
{
    // DarkestHour: опоздавшие ОФСК появляются в трущобах, на своих точках, а не на площади (решение автора 2026-11-13).
    /// <summary>
    ///     Late joiners of this job spawn at the job's own spawn points (if the station has any) instead of the late-join points.
    /// </summary>
    [DataField("dhLateJoinAtJobSpawn")]
    public bool DHLateJoinAtJobSpawn;
}
