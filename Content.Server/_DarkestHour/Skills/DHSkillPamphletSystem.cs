using Content.Shared._DarkestHour.Skills;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Skills;

/// <summary>
/// Брошюры навыков (план осады п. 12, решение автора 2026-11-26), как в RMC-14: прочитать в руке — навык +1, не выше
/// уровня брошюры (обычно «специалист»); одна брошюра на бойца за раунд. Брошюра расходуется.
/// </summary>
public sealed partial class DHSkillPamphletSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private DHSkillsSystem _skills = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHSkillPamphletComponent, UseInHandEvent>(OnUse);
        SubscribeLocalEvent<DHSkillPamphletComponent, DHSkillPamphletDoAfterEvent>(OnRead);
    }

    private bool CanRead(Entity<DHSkillPamphletComponent> ent, EntityUid user)
    {
        string? fail = null;
        if (!TryComp<DHSkillsComponent>(user, out var skills))
            fail = "dh-skill-pamphlet-useless";
        else if (HasComp<DHUsedSkillPamphletComponent>(user))
            fail = "dh-skill-pamphlet-used";
        else if (skills.Skills.GetValueOrDefault(ent.Comp.Skill) >= ent.Comp.MaxLevel)
            fail = "dh-skill-pamphlet-known";

        if (fail == null)
            return true;

        _popup.PopupEntity(Loc.GetString(fail), user, user, PopupType.SmallCaution);
        return false;
    }

    private void OnUse(Entity<DHSkillPamphletComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!CanRead(ent, args.User))
            return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.ReadTime,
            new DHSkillPamphletDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnRead(Entity<DHSkillPamphletComponent> ent, ref DHSkillPamphletDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var user = args.User;
        if (!CanRead(ent, user) || !TryComp<DHSkillsComponent>(user, out var skills))
            return;

        var level = skills.Skills.GetValueOrDefault(ent.Comp.Skill) + 1;
        _skills.SetLevel((user, skills), ent.Comp.Skill, level);
        EnsureComp<DHUsedSkillPamphletComponent>(user);

        var name = _proto.TryIndex(ent.Comp.Skill, out var proto) ? Loc.GetString(proto.Name) : ent.Comp.Skill.Id;
        _popup.PopupEntity(Loc.GetString("dh-skill-pamphlet-learned", ("skill", name),
            ("level", DHSkillsSystem.LevelName(level))), user, user, PopupType.Medium);
        QueueDel(ent);
    }
}
