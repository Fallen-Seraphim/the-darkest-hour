using Content.Shared._DarkestHour.Vendors;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Thief;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Vendors;

/// <summary>
/// Серверная часть вендомата снаряжения: считает лимиты бойца по разделам, проверяет должность, выдаёт комплект
/// и рассылает каждому открывшему окно его личное состояние.
/// </summary>
public sealed partial class DHGearVendorSystem : SharedDHGearVendorSystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHGearVendorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DHGearVendorComponent, DHGearVendorRefreshMessage>(OnRefresh);
        SubscribeLocalEvent<DHGearVendorComponent, DHGearVendorVendMessage>(OnVend);
    }

    private void OnMapInit(Entity<DHGearVendorComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.Stock.Clear();
        for (var s = 0; s < ent.Comp.Sections.Count; s++)
        {
            var section = ent.Comp.Sections[s];
            for (var e = 0; e < section.Entries.Count; e++)
            {
                if (section.Entries[e].Stock >= 0)
                    ent.Comp.Stock[(s, e)] = section.Entries[e].Stock;
            }
        }
    }

    private void OnRefresh(Entity<DHGearVendorComponent> ent, ref DHGearVendorRefreshMessage args)
    {
        SendState(ent, args.Actor);
    }

    private void OnVend(Entity<DHGearVendorComponent> ent, ref DHGearVendorVendMessage args)
    {
        var user = args.Actor;
        var comp = ent.Comp;

        if (args.Section < 0 || args.Section >= comp.Sections.Count)
            return;

        var section = comp.Sections[args.Section];
        if (args.Entry < 0 || args.Entry >= section.Entries.Count)
            return;

        var entry = section.Entries[args.Entry];
        var job = GetJob(user);
        var taken = GetTaken(user, comp, section);

        if (!Access.IsAllowed(user, ent) ||
            !JobAllowed(comp.Jobs, job) ||
            !JobAllowed(section.Jobs, job) ||
            !JobAllowed(entry.Jobs, job))
        {
            Deny(ent, user, "dh-gear-vendor-wrong-job");
            return;
        }

        if (section.Choices > 0 && taken >= section.Choices)
        {
            Deny(ent, user, "dh-gear-vendor-limit");
            return;
        }

        if (comp.Stock.TryGetValue((args.Section, args.Entry), out var stock) && stock <= 0)
        {
            Deny(ent, user, "dh-gear-vendor-empty");
            return;
        }

        var items = new List<EntProtoId>();
        if (entry.Set is { } setId && _proto.TryIndex(setId, out ThiefBackpackSetPrototype? set))
            items.AddRange(set.Content);
        else if (entry.Proto is { } proto)
        {
            for (var i = 0; i < Math.Max(1, entry.Count); i++)
                items.Add(proto);
        }

        if (items.Count == 0)
            return;

        if (comp.Stock.ContainsKey((args.Section, args.Entry)))
            comp.Stock[(args.Section, args.Entry)] = stock - 1;

        AddTaken(user, comp, section);

        var coords = Transform(user).Coordinates;
        foreach (var item in items)
        {
            var spawned = Spawn(item, coords);
            _hands.PickupOrDrop(user, spawned, checkActionBlocker: false);
        }

        _audio.PlayPvs(comp.VendSound, ent);

        // Лимиты у каждого свои, запас общий — всем открывшим окно шлём их личное состояние.
        foreach (var actor in _ui.GetActors(ent.Owner, DHGearVendorUiKey.Key))
            SendState(ent, actor);
    }

    private void Deny(Entity<DHGearVendorComponent> ent, EntityUid user, string message)
    {
        _audio.PlayPvs(ent.Comp.DenySound, ent);
        Popup.PopupEntity(Loc.GetString(message), ent, user);
        SendState(ent, user);
    }

    private void SendState(Entity<DHGearVendorComponent> ent, EntityUid user)
    {
        var comp = ent.Comp;
        var job = GetJob(user);
        var vendorAllowed = JobAllowed(comp.Jobs, job);
        var sections = new List<DHGearVendorSectionState>(comp.Sections.Count);

        for (var s = 0; s < comp.Sections.Count; s++)
        {
            var section = comp.Sections[s];
            var state = new DHGearVendorSectionState
            {
                Allowed = vendorAllowed && JobAllowed(section.Jobs, job),
                Taken = GetTaken(user, comp, section),
                Choices = section.Choices,
            };

            for (var e = 0; e < section.Entries.Count; e++)
            {
                var entry = section.Entries[e];
                state.Entries.Add(new DHGearVendorEntryState
                {
                    Allowed = JobAllowed(entry.Jobs, job),
                    Stock = comp.Stock.TryGetValue((s, e), out var left) ? left : -1,
                });
            }

            sections.Add(state);
        }

        _ui.ServerSendUiMessage(ent.Owner, DHGearVendorUiKey.Key, new DHGearVendorStateMessage(sections), user);
    }

    private ProtoId<JobPrototype>? GetJob(EntityUid user)
    {
        if (!_mind.TryGetMind(user, out var mindId, out _))
            return null;

        return _jobs.MindTryGetJobId(mindId, out var job) ? job : null;
    }

    private static bool JobAllowed(List<ProtoId<JobPrototype>> jobs, ProtoId<JobPrototype>? job)
    {
        return jobs.Count == 0 || job is { } id && jobs.Contains(id);
    }

    /// <summary>Учёт ведётся на разуме (если он есть), чтобы лимит не обнулялся при смене тела.</summary>
    private EntityUid Holder(EntityUid user)
    {
        return _mind.TryGetMind(user, out var mindId, out _) ? mindId : user;
    }

    private static string Key(DHGearVendorComponent comp, DHGearVendorSection section)
    {
        return comp.Group + ":" + section.Id;
    }

    private int GetTaken(EntityUid user, DHGearVendorComponent comp, DHGearVendorSection section)
    {
        return TryComp(Holder(user), out DHGearVendorUserComponent? taken) &&
               taken.Taken.TryGetValue(Key(comp, section), out var count)
            ? count
            : 0;
    }

    private void AddTaken(EntityUid user, DHGearVendorComponent comp, DHGearVendorSection section)
    {
        var taken = EnsureComp<DHGearVendorUserComponent>(Holder(user));
        var key = Key(comp, section);
        taken.Taken[key] = taken.Taken.GetValueOrDefault(key) + 1;
    }
}
