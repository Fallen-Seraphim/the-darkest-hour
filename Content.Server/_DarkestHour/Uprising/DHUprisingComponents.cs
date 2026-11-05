using Content.Shared._DarkestHour.Assault;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._DarkestHour.Uprising;

/// <summary>Сторона «Восстания».</summary>
public enum DHUprisingSide : byte
{
    Civilian,
    Vanguard,
    Police,
    Front,
}

/// <summary>
/// Режим «Восстание» (решение автора 2026-11-05): квартал Лост-Энджелса. Военная Полиция ищет лидеров ОФСК среди
/// жителей, ОФСК держит квартал, LA-V торгует и должна вывезти представителя через ЗП-1 «Аэронавтика». Борт на ЗП-1
/// прилетает по расписанию: забирает представителя (эвакуация), с ним приходят подкрепления военпола.
/// </summary>
[RegisterComponent]
public sealed partial class DHUprisingRuleComponent : Component
{
    [DataField]
    public TimeSpan TimeLimit = TimeSpan.FromMinutes(70);

    /// <summary>Когда (от начала раунда) борт садится на ЗП-1.</summary>
    [DataField]
    public List<TimeSpan> Flights = new() { TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(50) };

    /// <summary>За сколько до посадки объявляют о борте.</summary>
    [DataField]
    public TimeSpan FlightWarning = TimeSpan.FromMinutes(3);

    /// <summary>Сколько борт стоит на ЗП-1.</summary>
    [DataField]
    public TimeSpan FlightStay = TimeSpan.FromMinutes(4);

    [DataField]
    public ResPath FlightPath = new("/Maps/Shuttles/arrivals.yml"); // гражданский борт «Аэронавтики», не ОБР (решение автора 2026-11-05)

    [DataField]
    public LocId FlightName = "dh-uprising-flight-name";

    [DataField]
    public HashSet<ProtoId<JobPrototype>> PoliceJobs = new();

    [DataField]
    public HashSet<ProtoId<JobPrototype>> FrontJobs = new();

    [DataField]
    public HashSet<ProtoId<JobPrototype>> VanguardJobs = new();

    /// <summary>Кого нужно вывезти (представитель LA-V).</summary>
    [DataField]
    public HashSet<ProtoId<JobPrototype>> VipJobs = new();

    /// <summary>Лидеры ОФСК: их арест или смерть — цель военпола.</summary>
    [DataField]
    public HashSet<ProtoId<JobPrototype>> LeaderJobs = new();

    // --- состояние раунда ---

    [ViewVariables]
    public TimeSpan Start;

    [ViewVariables]
    public int NextFlight;

    [ViewVariables]
    public bool FlightWarned;

    [ViewVariables]
    public EntityUid? Flight;

    [ViewVariables]
    public TimeSpan FlightDeparts;

    [ViewVariables]
    public int VipsEvacuated;

    [ViewVariables]
    public int CiviliansKilled;

    [ViewVariables]
    public int CiviliansKilledByPolice;

    [ViewVariables]
    public int CiviliansKilledByFront;

    [ViewVariables]
    public bool Ended;
}

/// <summary>Игрок «Восстания»: его сторона (по должности) и важная ли он персона.</summary>
[RegisterComponent]
public sealed partial class DHUprisingMemberComponent : Component
{
    [ViewVariables]
    public DHUprisingSide Side;

    [ViewVariables]
    public bool Vip;

    [ViewVariables]
    public bool Leader;
}

/// <summary>Метка посадочной площадки ЗП-1 «Аэронавтика»: сюда садится борт «Восстания».</summary>
[RegisterComponent]
public sealed partial class DHUprisingFlightPadComponent : Component;
