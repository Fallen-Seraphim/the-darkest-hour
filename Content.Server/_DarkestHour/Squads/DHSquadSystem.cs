using System.Linq;
using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Squads;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Content.Shared.Roles.Jobs;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Squads;

/// <summary>
/// Отряды: консоль командования показывает бойцов своей стороны и назначает их в отряды. При назначении в гарнитуре
/// бойца отрядный ключ меняется на ключ нового отряда; если отрядного ключа нет — новый добавляется.
/// </summary>
public sealed partial class DHSquadSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(2);
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<DHSquadConsoleComponent>(DHSquadConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<DHSquadAssignMessage>(OnAssign);
        });

        SubscribeLocalEvent<DHSquadMemberComponent, ExaminedEvent>(OnMemberExamined);
    }

    private IEnumerable<DHSquadPrototype> Squads(Content.Shared._DarkestHour.Assault.DHAssaultFaction faction)
    {
        return _proto.EnumeratePrototypes<DHSquadPrototype>().Where(s => s.Faction == faction).OrderBy(s => s.Order);
    }

    private void OnOpened(Entity<DHSquadConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<DHSquadConsoleComponent> ent)
    {
        var squads = Squads(ent.Comp.Faction)
            .Select(s => new DHSquadInfo(s.ID, Loc.GetString(s.Name), s.Color))
            .ToList();

        var fighters = new List<DHSquadFighterEntry>();
        var query = EntityQueryEnumerator<DHAssaultFighterComponent>();
        while (query.MoveNext(out var uid, out var fighter))
        {
            if (fighter.Faction != ent.Comp.Faction)
                continue;

            var job = _mind.TryGetMind(uid, out var mindId, out _) && _jobs.MindTryGetJobName(mindId, out var jobName)
                ? jobName
                : string.Empty;
            string? squad = TryComp<DHSquadMemberComponent>(uid, out var member) ? member.Squad.Id : null;
            fighters.Add(new DHSquadFighterEntry(GetNetEntity(uid), Name(uid), job, squad, !_mobState.IsDead(uid)));
        }

        fighters.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        _ui.SetUiState(ent.Owner, DHSquadConsoleUiKey.Key, new DHSquadConsoleState(squads, fighters));
    }

    private void OnAssign(Entity<DHSquadConsoleComponent> ent, ref DHSquadAssignMessage args)
    {
        if (!TryGetEntity(args.Fighter, out var target) ||
            !TryComp<DHAssaultFighterComponent>(target, out var fighter) || fighter.Faction != ent.Comp.Faction)
        {
            return;
        }

        DHSquadPrototype? squad = null;
        if (args.Squad != null &&
            (!_proto.TryIndex<DHSquadPrototype>(args.Squad, out squad) || squad.Faction != ent.Comp.Faction))
        {
            return;
        }

        Assign(target.Value, squad);
        _popup.PopupEntity(squad == null
                ? Loc.GetString("dh-squad-console-removed", ("fighter", target.Value))
                : Loc.GetString("dh-squad-console-assigned", ("fighter", target.Value), ("squad", Loc.GetString(squad.Name))),
            ent, args.Actor);
        UpdateUi(ent);
    }

    /// <summary>Назначить бойца в отряд (null — вывести) и переставить отрядный ключ в его гарнитуре.</summary>
    public void Assign(EntityUid fighter, DHSquadPrototype? squad)
    {
        if (squad == null)
            RemComp<DHSquadMemberComponent>(fighter);
        else
            EnsureComp<DHSquadMemberComponent>(fighter).Squad = squad.ID;

        if (!_inventory.TryGetSlotEntity(fighter, "ears", out var headset) ||
            !TryComp<EncryptionKeyHolderComponent>(headset, out var holder))
        {
            if (squad != null)
                _popup.PopupEntity(Loc.GetString("dh-squad-no-headset"), fighter, fighter);
            return;
        }

        // Убрать старые отрядные ключи (любого отряда).
        var squadKeys = _proto.EnumeratePrototypes<DHSquadPrototype>().Select(s => s.Key.Id).ToHashSet();
        foreach (var key in holder.KeyContainer.ContainedEntities.ToList())
        {
            if (MetaData(key).EntityPrototype?.ID is { } id && squadKeys.Contains(id))
            {
                _container.Remove(key, holder.KeyContainer);
                QueueDel(key);
            }
        }

        if (squad == null)
        {
            _popup.PopupEntity(Loc.GetString("dh-squad-left"), fighter, fighter);
            return;
        }

        // Новый ключ; места нет — гарнитура получает ещё одно гнездо.
        if (holder.KeyContainer.ContainedEntities.Count >= holder.KeySlots)
            holder.KeySlots = holder.KeyContainer.ContainedEntities.Count + 1;

        var newKey = Spawn(squad.Key, Transform(headset.Value).Coordinates);
        if (!_container.Insert(newKey, holder.KeyContainer))
        {
            QueueDel(newKey);
            return;
        }

        _popup.PopupEntity(Loc.GetString("dh-squad-joined", ("squad", Loc.GetString(squad.Name))), fighter, fighter,
            PopupType.Medium);
    }

    private void OnMemberExamined(Entity<DHSquadMemberComponent> ent, ref ExaminedEvent args)
    {
        if (_proto.TryIndex(ent.Comp.Squad, out var squad))
            args.PushMarkup(Loc.GetString("dh-squad-examine", ("squad", Loc.GetString(squad.Name)),
                ("color", squad.Color.ToHex())));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;
        var consoles = EntityQueryEnumerator<DHSquadConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            if (_ui.IsUiOpen(uid, DHSquadConsoleUiKey.Key))
                UpdateUi((uid, console));
        }
    }
}
