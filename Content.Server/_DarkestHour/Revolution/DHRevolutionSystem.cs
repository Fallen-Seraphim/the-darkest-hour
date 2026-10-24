using Content.Server._DarkestHour.Republic;
using Content.Server.Chat.Systems;
using Content.Shared.Antag;
using Content.Shared.GameTicking.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Revolution;

/// <summary>
/// Революция ОФСК (вешается на правило раунда <c>Revolutionary</c>): «спонсированная революция» и ответ Республики
/// на победу Фронта.
/// </summary>
[RegisterComponent]
public sealed partial class DHRevolutionRuleComponent : Component
{
    /// <summary>Шанс «спонсированной революции»: ОНК снабжает комиссаров крадеными республиканскими стволами.</summary>
    [DataField]
    public float SponsoredChance = 0.05f;

    /// <summary>Выпало ли спонсирование (бросается один раз за раунд, при первом комиссаре или старте правила).</summary>
    [DataField]
    public bool? Sponsored;

    /// <summary>Что получает каждый комиссар при спонсировании.</summary>
    [DataField]
    public List<EntProtoId> SponsoredGear = new();

    /// <summary>Через сколько после начала раунда Республика объявляет о краже вооружения со склада.</summary>
    [DataField]
    public TimeSpan TheftAnnouncementDelay = TimeSpan.FromSeconds(90);

    [DataField]
    public TimeSpan? TheftAnnouncementAt;

    /// <summary>Через сколько после захвата станции Фронтом Республика отвечает «Деадендом».</summary>
    [DataField]
    public TimeSpan DeadendDelay = TimeSpan.FromSeconds(45);

    [DataField]
    public TimeSpan? DeadendAt;

    [DataField]
    public bool Won;
}

/// <summary>
/// Командование станции убито, арестовано или завербовано — Фронт победил. Если обработано, ванильный вызов
/// эвакуационного шаттла не делается.
/// </summary>
[ByRefEvent]
public record struct DHRevolutionWonEvent(bool Handled = false);

/// <summary>
/// Революция ОФСК. С шансом 5% революция «спонсированная»: каждому комиссару ОНК выдаёт взломщик доступа и
/// пистолет-пулемёт RPR-M-9-02 с запасным магазином, а в начале раунда Республика объявляет о краже вооружения
/// со своего склада. Победа Фронта — Республика объявляет станцию изменнической: код «Деаденд» и усиленный отряд
/// зачистки с тактической ядерной боеголовкой (вместо эвакуации).
/// </summary>
public sealed partial class DHRevolutionSystem : EntitySystem
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DHRepublicSweepSystem _republic = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private IGameTiming _timing = default!;

    public static readonly Color FrontRed = Color.FromHex("#C8322B");
    public static readonly Color RepublicGreen = Color.FromHex("#5E8C2A");
    private static readonly SoundSpecifier TheftSound = new SoundPathSpecifier("/Audio/Misc/notice1.ogg");

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHRevolutionRuleComponent, GameRuleStartedEvent>(OnRuleStarted);
        SubscribeLocalEvent<DHRevolutionRuleComponent, AfterAntagEntitySelectedEvent>(OnCommissarSelected);
        SubscribeLocalEvent<DHRevolutionRuleComponent, DHRevolutionWonEvent>(OnRevolutionWon);
    }

    private bool IsSponsored(DHRevolutionRuleComponent rule)
    {
        rule.Sponsored ??= _random.Prob(rule.SponsoredChance);
        return rule.Sponsored.Value;
    }

    private void OnRuleStarted(Entity<DHRevolutionRuleComponent> ent, ref GameRuleStartedEvent args)
    {
        if (IsSponsored(ent.Comp))
            ent.Comp.TheftAnnouncementAt = _timing.CurTime + ent.Comp.TheftAnnouncementDelay;
    }

    private void OnCommissarSelected(Entity<DHRevolutionRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (!IsSponsored(ent.Comp))
            return;

        var commissar = args.EntityUid;
        EntityUid? backpack = _inventory.TryGetSlotEntity(commissar, "back", out var back) &&
                              HasComp<StorageComponent>(back)
            ? back
            : null;

        var coords = Transform(commissar).Coordinates;
        foreach (var proto in ent.Comp.SponsoredGear)
        {
            var item = Spawn(proto, coords);
            if (backpack is { } bag && _storage.Insert(bag, item, out _, playSound: false))
                continue;

            _hands.PickupOrDrop(commissar, item, checkActionBlocker: false, animate: false);
        }

        _antag.SendBriefing(commissar, Loc.GetString("dh-revolution-sponsored-briefing"), FrontRed, null);
    }

    private void OnRevolutionWon(Entity<DHRevolutionRuleComponent> ent, ref DHRevolutionWonEvent args)
    {
        args.Handled = true;
        if (ent.Comp.Won)
            return;

        ent.Comp.Won = true;
        ent.Comp.DeadendAt = _timing.CurTime + ent.Comp.DeadendDelay;
        _chat.DispatchGlobalAnnouncement(Loc.GetString("dh-revolution-station-fallen"),
            Loc.GetString("dh-revolution-republic-sender"), true, null, RepublicGreen);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<DHRevolutionRuleComponent>();
        while (query.MoveNext(out _, out var rule))
        {
            if (rule.TheftAnnouncementAt is { } theftAt && now >= theftAt)
            {
                rule.TheftAnnouncementAt = null;
                _chat.DispatchGlobalAnnouncement(Loc.GetString("dh-revolution-theft-announcement"),
                    Loc.GetString("dh-revolution-republic-sender"), true, TheftSound, RepublicGreen);
            }

            if (rule.DeadendAt is { } deadendAt && now >= deadendAt)
            {
                rule.DeadendAt = null;
                _republic.TryCallSweep(null, out _, reinforced: true, level: DHRepublicSweepSystem.DeadendRevolution);
            }
        }
    }
}
