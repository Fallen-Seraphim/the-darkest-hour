// Перенесено из RMC-14 (github.com/RMC-14/RMC-14, MIT): RankSystem. DarkestHour: без выбора звания в профиле
// (даётся высшее заслуженное), повтор, если наигранное время ещё не загрузилось (как в CMU), знаки различия на форме.
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._DarkestHour.Ranks;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Marines.Roles.Ranks;

public sealed partial class RankSystem : SharedRankSystem
{
    private static readonly TimeSpan PlaytimeRetryDelay = TimeSpan.FromSeconds(10);
    private const SlotFlags InsigniaSlots = SlotFlags.INNERCLOTHING | SlotFlags.OUTERCLOTHING;

    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private PlayTimeTrackingManager _tracking = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RankComponent, TransformSpeakerNameEvent>(OnSpeakerNameTransform);
        SubscribeLocalEvent<RankComponent, DidEquipEvent>(OnDidEquip);
        SubscribeLocalEvent<RankComponent, DidUnequipEvent>(OnDidUnequip);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnSpeakerNameTransform(Entity<RankComponent> ent, ref TransformSpeakerNameEvent args)
    {
        // Голос под маской (изменитель голоса) не выдаёт звание.
        if (args.VoiceName != Name(ent))
            return;

        if (GetSpeakerRankName(ent, args.VoiceName) is { } name)
            args.VoiceName = name;
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null || !_prototypes.TryIndex<JobPrototype>(ev.JobId, out var job) || job.Ranks == null)
            return;

        if (_tracking.TryGetTrackerTimes(ev.Player, out var playTimes))
        {
            AssignRank(ev.Mob, job, ev.Profile, playTimes);
            return;
        }

        // Наигранное время ещё не загрузилось: пока низшее звание, через 10 с — заслуженное.
        AssignRank(ev.Mob, job, ev.Profile, new Dictionary<string, TimeSpan>());
        var mob = ev.Mob;
        var player = ev.Player;
        var profile = ev.Profile;
        Timer.Spawn(PlaytimeRetryDelay, () =>
        {
            if (Deleted(mob) || player.Status != SessionStatus.InGame)
                return;

            if (_tracking.TryGetTrackerTimes(player, out var retryTimes))
                AssignRank(mob, job, profile, retryTimes);
            else
                Log.Error($"Playtimes weren't ready for {player} after retry!");
        });
    }

    /// <summary>
    ///     Звания в прототипе должности — от высшего к низшему: даётся первое, чьи требования выполнены.
    /// </summary>
    private void AssignRank(EntityUid mob, JobPrototype job, HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes)
    {
        if (job.Ranks == null)
            return;

        foreach (var (rankId, requirements) in job.Ranks)
        {
            if (!_prototypes.HasIndex(rankId))
                continue;

            var passed = true;
            if (requirements != null)
            {
                foreach (var requirement in requirements)
                {
                    if (!requirement.Check(EntityManager, _prototypes, profile, playTimes, out _))
                    {
                        passed = false;
                        break;
                    }
                }
            }

            if (passed)
            {
                SetRank(mob, rankId);
                return;
            }
        }
    }

    protected override void RankChanged(EntityUid uid)
    {
        var slots = _inventory.GetSlotEnumerator(uid, InsigniaSlots);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is { } worn)
                PutInsignia(uid, worn);
        }
    }

    private void OnDidEquip(Entity<RankComponent> ent, ref DidEquipEvent args)
    {
        if ((args.SlotFlags & InsigniaSlots) != 0)
            PutInsignia(ent, args.Equipment);
    }

    private void OnDidUnequip(Entity<RankComponent> ent, ref DidUnequipEvent args)
    {
        // Знаки различия — владельца, с чужого плеча они не переходят.
        if ((args.SlotFlags & InsigniaSlots) != 0 && HasComp<DHRankInsigniaComponent>(args.Equipment))
        {
            RemComp<DHRankInsigniaComponent>(args.Equipment);
            _item.VisualsChanged(args.Equipment);
        }
    }

    private void PutInsignia(EntityUid wearer, EntityUid clothing)
    {
        if (GetRank(wearer)?.Insignia is not { } sprite)
            return;

        var insignia = EnsureComp<DHRankInsigniaComponent>(clothing);
        insignia.Sprite = sprite;
        Dirty(clothing, insignia);
        _item.VisualsChanged(clothing);
    }
}
