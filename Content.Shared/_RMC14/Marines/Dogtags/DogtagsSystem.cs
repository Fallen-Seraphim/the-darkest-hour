// Ported from RMC-14 (MIT): https://github.com/RMC-14/RMC-14/blob/7fca80389aa705890666b9476051fcca2970e6fc/Content.Shared/_RMC14/Marines/Dogtags/DogtagsSystem.cs
// DarkestHour: RMC-only dependencies (xenos, skills, RMC unrevivable/defibrillator, IdCardOwner) removed.
// Tags can be taken from any dead body.
using Content.Shared.Access.Components;
using Content.Shared.Coordinates;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Marines.Dogtags;

public sealed partial class DogtagsSystem : EntitySystem // DarkestHour: partial (RA0049 — ошибка в Release)
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TakeableTagsComponent, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>>>(GetRelayedTags);
        SubscribeLocalEvent<TakeableTagsComponent, GetVerbsEvent<EquipmentVerb>>(OnGetVerbTags);
        SubscribeLocalEvent<TakeableTagsComponent, ExaminedEvent>(OnTagsExamine);

        SubscribeLocalEvent<InformationTagsComponent, ExaminedEvent>(OnInfoTagsExamine);
        SubscribeLocalEvent<InformationTagsComponent, AfterInteractEvent>(OnInfoTagsUse);

        SubscribeLocalEvent<RMCMemorialComponent, ExaminedEvent>(OnMemorialExamined);
    }

    private void OnTagsExamine(Entity<TakeableTagsComponent> tags, ref ExaminedEvent args)
    {
        GetTagInformation(tags, out var name, out var job, out var blood);
        args.PushMarkup(Loc.GetString("rmc-dogtags-read", ("name", name), ("assignment", job), ("bloodtype", blood)));
    }

    private void OnInfoTagsExamine(Entity<InformationTagsComponent> tags, ref ExaminedEvent args)
    {
        // Priorities to make sure they appear in about the right spots
        args.PushMarkup(Loc.GetString("rmc-dogtags-info-read-start", ("tags", tags.Comp.Tags.Count)), -19);
        var count = 1;
        foreach (var tag in tags.Comp.Tags)
        {
            args.PushMarkup(Loc.GetString("rmc-dogtags-info-read", ("number", count++), ("name", tag.Name), ("assignment", tag.Assignment), ("bloodtype", tag.BloodType)), -19 - count);
        }
    }

    private void OnMemorialExamined(Entity<RMCMemorialComponent> memorial, ref ExaminedEvent args)
    {
        if (memorial.Comp.Names.Count == 0)
            return;

        var text = Loc.GetString("rmc-memorial-start") + " " + MemorialNamesFormat(memorial.Comp.Names);
        args.PushMarkup(text, -5);
    }

    public string MemorialNamesFormat(List<string> memorialNames)
    {
        return string.Join(", ", memorialNames) + ".";
    }

    private void GetRelayedTags(Entity<TakeableTagsComponent> tags, ref InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>> args)
    {
        OnGetVerbTags(tags, ref args.Args);
    }

    private bool CanTakeTags(Entity<TakeableTagsComponent> tags, EntityUid wearer, EntityUid taker, out bool equipped, out string reason)
    {
        equipped = true;
        reason = "";

        if (wearer == taker)
        {
            // DarkestHour: RMC checks IdCardOwner here; we have no owner tracking,
            // so loose tags held in hand (e.g. picked off a body) can always be processed.
            if (_hands.IsHolding(taker, tags))
            {
                equipped = false;
                return true;
            }

            return false;
        }

        // DarkestHour: tags can be taken from any dead body, no need to wait for rotting.
        if (!_mob.IsDead(wearer))
        {
            reason = Loc.GetString("rmc-dogtags-still-alive");
            return false;
        }

        return true;
    }

    private void OnGetVerbTags(Entity<TakeableTagsComponent> tags, ref GetVerbsEvent<EquipmentVerb> args)
    {
        if (!args.CanInteract ||
            !args.CanAccess ||
            args.Hands == null ||
            tags.Comp.TagsTaken)
            return;

        var wearer = Transform(tags).ParentUid;
        var user = args.User;

        if (!CanTakeTags(tags, wearer, user, out _, out _))
            return;

        var verb = new EquipmentVerb()
        {
            Icon = new SpriteSpecifier.Texture(new("/Textures/_RMC14/Interface/VerbIcons/dogtag.png")),
            Text = Loc.GetString("rmc-dogtags-take"),
        };

        verb.Act = () => TakeTags(tags, user, wearer);

        args.Verbs.Add(verb);
    }

    private void TakeTags(Entity<TakeableTagsComponent> tags, EntityUid user, EntityUid wearer)
    {
        if (tags.Comp.TagsTaken)
        {
            _popup.PopupEntity(Loc.GetString("rmc-dogtags-already-taken", ("target", wearer)), user, user);
            return;
        }

        if (!CanTakeTags(tags, wearer, user, out var equipped, out var reason))
        {
            _popup.PopupEntity(reason, user, user);
            return;
        }

        if (!_interaction.InRangeAndAccessible(user, wearer))
            return;

        tags.Comp.TagsTaken = true;
        _appearance.SetData(tags, DogtagVisuals.Taken, true);

        if (_net.IsClient)
            return;

        // Read the information before the held tags may be deleted below.
        GetTagInformation(tags, out var name, out var job, out var blood);

        if (!equipped)
        {
            var prop = SpawnAtPosition(tags.Comp.FallenTag, user.ToCoordinates());
            if (TryComp<IdCardComponent>(tags, out var id))
            {
                CopyComp(tags.Owner, prop, tags.Comp);
                CopyComp(tags.Owner, prop, id);
            }
            QueueDel(tags);
        }

        var tag = SpawnNextToOrDrop(tags.Comp.InfoTag, wearer);
        Dirty(tags);

        var comp = EnsureComp<InformationTagsComponent>(tag);
        comp.Tags.Add(new InfoTagInfo
        {
            Name = name,
            Assignment = job,
            BloodType = blood,
        });
        Dirty(tag, comp);
        _hands.TryPickupAnyHand(user, tag);
    }

    private void OnInfoTagsUse(Entity<InformationTagsComponent> tags, ref AfterInteractEvent args)
    {
        if (args.Target == null || args.Target == tags.Owner)
            return;

        if (TryComp<InformationTagsComponent>(args.Target, out var targTags))
        {
            args.Handled = true;
            _meta.SetEntityName(args.Target.Value, Loc.GetString("rmc-dogtags-info-joined-name"));
            _meta.SetEntityDescription(args.Target.Value, Loc.GetString("rmc-dogtags-info-joined-desc"));

            if (_net.IsClient)
                return;

            var tagsJoinedString = tags.Comp.Tags.Count == 1 && targTags.Tags.Count == 1 ? "rmc-dogtags-single-join" : "rmc-dogtags-join";

            _popup.PopupEntity(Loc.GetString(tagsJoinedString), args.User, args.User);

            targTags.Tags.AddRange(tags.Comp.Tags);
            Dirty(args.Target.Value, targTags);
            _appearance.SetData(args.Target.Value, InfoTagVisuals.Number, Math.Min(targTags.Tags.Count, targTags.MaxDisplayTags));
            QueueDel(tags);
        }
        else if (TryComp<RMCMemorialComponent>(args.Target, out var memorial))
        {
            args.Handled = true;

            if (_net.IsClient)
                return;

            _popup.PopupEntity(Loc.GetString("rmc-memorial-add", ("tags", tags), ("slab", args.Target.Value)), args.User, args.User);

            foreach (var tag in tags.Comp.Tags)
            {
                memorial.Names.Add(tag.Name);
            }

            Dirty(args.Target.Value, memorial);
            QueueDel(tags);
        }
    }

    private void GetTagInformation(EntityUid dogtag, out string name, out string job, out string bloodtype)
    {
        name = Loc.GetString("rmc-dogtags-unknown");
        job = Loc.GetString("rmc-dogtags-unknown");
        // TODO DarkestHour: real blood types (RMC also has none yet)
        bloodtype = "O-";

        if (!TryComp<IdCardComponent>(dogtag, out var id))
            return;

        if (!string.IsNullOrEmpty(id.FullName))
            name = id.FullName;

        if (!string.IsNullOrEmpty(id.LocalizedJobTitle))
            job = id.LocalizedJobTitle;
    }
}
