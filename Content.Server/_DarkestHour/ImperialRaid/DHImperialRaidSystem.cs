using Content.Server._DarkestHour.Orbit;
using Content.Server._DarkestHour.Republic;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Shuttles.Events;
using Content.Shared.Station.Systems;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.ImperialRaid;

/// <summary>Ультиматум Имперской Морской Пехоты станции: срок, итог.</summary>
[RegisterComponent]
public sealed partial class DHImperialUltimatumComponent : Component
{
    [DataField]
    public TimeSpan Deadline;

    [DataField]
    public bool Resolved;

    /// <summary>Станция сдалась Империи: когда Республика ответит кодом «Деаденд» и усиленным отрядом зачистки.</summary>
    [DataField]
    public TimeSpan? DeadendAt;
}

/// <summary>
/// Имперский Рейд: когда шаттл Рейда выходит из прыжка у станции, Имперская Морская Пехота объявляет ультиматум
/// (жёлтым): десять минут на сдачу. Сдача — подъём станции на высокую орбиту (компьютер орбиты). Сдались — Рейд
/// принимает капитуляцию, а Республика объявляет станцию изменнической: код «Деаденд» и усиленный отряд зачистки
/// с тактической ядерной боеголовкой. Не сдались — объявляется штурм.
/// </summary>
public sealed partial class DHImperialRaidSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private DHRepublicSweepSystem _republic = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private IGameTiming _timing = default!;

    public static readonly Color ImperialYellow = Color.FromHex("#F2C230");
    private static readonly SoundSpecifier UltimatumSound = new SoundPathSpecifier("/Audio/Announcements/war.ogg");

    /// <summary>Сколько даётся на сдачу.</summary>
    public static readonly TimeSpan SurrenderTime = TimeSpan.FromMinutes(10);

    /// <summary>Через сколько после капитуляции Республика отвечает «Деадендом».</summary>
    public static readonly TimeSpan DeadendDelay = TimeSpan.FromSeconds(45);

    /// <summary>Высокая орбита: станция на этой высоте или выше считается сдавшейся.</summary>
    public const float HighOrbit = 380f;

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
    }

    private void OnFTLCompleted(ref FTLCompletedEvent ev)
    {
        if (!HasComp<NukeOpsShuttleComponent>(ev.Entity))
            return;

        // станция, у которой вышел шаттл Рейда
        var query = EntityQueryEnumerator<StationJobsComponent>();
        while (query.MoveNext(out var station, out _))
        {
            if (HasComp<DHImperialUltimatumComponent>(station) ||
                _station.GetLargestGrid(station) is not { } grid ||
                Transform(grid).MapUid != ev.MapUid)
            {
                continue;
            }

            var ultimatum = AddComp<DHImperialUltimatumComponent>(station);
            ultimatum.Deadline = _timing.CurTime + SurrenderTime;

            _chat.DispatchStationAnnouncement(station,
                Loc.GetString("dh-imperial-ultimatum", ("station", Name(station)), ("minutes", (int) SurrenderTime.TotalMinutes),
                    ("altitude", (int) HighOrbit)),
                Loc.GetString("dh-imperial-sender"),
                false,
                UltimatumSound,
                ImperialYellow);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + TimeSpan.FromSeconds(2);

        var query = EntityQueryEnumerator<DHImperialUltimatumComponent>();
        while (query.MoveNext(out var station, out var ultimatum))
        {
            if (ultimatum.DeadendAt is { } deadendAt && now >= deadendAt)
            {
                ultimatum.DeadendAt = null;
                _republic.TryCallSweep(station, out _, reinforced: true);
                continue;
            }

            if (ultimatum.Resolved)
                continue;

            var surrendered = TryComp<DHStationOrbitComponent>(station, out var orbit) && orbit.Altitude >= HighOrbit;
            if (surrendered)
            {
                ultimatum.Resolved = true;
                ultimatum.DeadendAt = now + DeadendDelay;
                _chat.DispatchStationAnnouncement(station, Loc.GetString("dh-imperial-surrender-accepted"),
                    Loc.GetString("dh-imperial-sender"), true, null, ImperialYellow);
                continue;
            }

            if (now < ultimatum.Deadline)
                continue;

            ultimatum.Resolved = true;
            _chat.DispatchStationAnnouncement(station, Loc.GetString("dh-imperial-assault"),
                Loc.GetString("dh-imperial-sender"), false, UltimatumSound, ImperialYellow);
        }
    }
}
