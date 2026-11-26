using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Assault;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Announcements;

/// <summary>
/// Прибытие командования (решение автора 2026-11-26): когда появляется кто-то из командования стороны, своей стороне —
/// боцманская дудка и «На палубе — …» у Империи (авианосец), «В расположении — …» у гарнизона Республики.
/// </summary>
public sealed partial class DHCommandArrivalSystem : EntitySystem
{
    [Dependency] private DHAssaultRuleSystem _assault = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly SoundSpecifier Sound =
        new SoundPathSpecifier("/Audio/_DarkestHour/Announcements/command_arrival.ogg");

    /// <summary>Командование сторон: о нём объявляют при появлении.</summary>
    private static readonly HashSet<string> CommandJobs = new()
    {
        "DHGarrisonCommander",
        "DHGarrisonArtilleryOfficer",
        "DHCarrierOperationCommander",
        "DHCarrierCaptain",
        "DHCarrierSupportOfficer",
        "DHMarineCommander",
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId is not { } jobId || !CommandJobs.Contains(jobId) ||
            !TryComp<DHAssaultFactionStationComponent>(ev.Station, out var station) ||
            !_proto.TryIndex<JobPrototype>(jobId, out var job))
        {
            return;
        }

        var faction = station.Faction;
        var empire = faction == DHAssaultFaction.Empire;
        var text = Loc.GetString(empire ? "dh-command-arrival-empire" : "dh-command-arrival-republic",
            ("job", job.LocalizedName), ("name", Name(ev.Mob)));
        var sender = Loc.GetString(empire ? "dh-command-arrival-sender-empire" : "dh-command-arrival-sender-republic");
        _assault.AnnounceToFaction(faction, sender, text, Sound);
    }
}
