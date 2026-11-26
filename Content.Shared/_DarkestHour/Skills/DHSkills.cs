using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Skills;

// Навыки (план осады п. 12, решения автора 2026-11-26), по образцу RMC-14 Skills: у должности уровни 0–3 по навыкам.
// 0 — не обучен, 1 — базовая подготовка, 2 — специалист, 3 — мастер. Уровень меняет скорость действий
// (DelayMultipliers), а часть действий без нужного уровня запрещена (DHRequiresSkillComponent и проверки в системах:
// пилотирование, бронетехника, хирургия, артиллерия). У кого компонента навыков нет вовсе (не «Осада»: «Восстание»,
// гост-роли без набора) — ограничений нет.

[Prototype("dhSkill")]
public sealed partial class DHSkillPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    /// <summary>Множитель длительности действий по уровню (0, 1, 2, 3); нет значения — 1.</summary>
    [DataField]
    public float[] DelayMultipliers = Array.Empty<float>();

    [DataField]
    public int MaxLevel = 3;
}

/// <summary>Навыки бойца: навык → уровень (нет в словаре — 0).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(DHSkillsSystem))]
public sealed partial class DHSkillsComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<DHSkillPrototype>, int> Skills = new();
}

/// <summary>Пульт или устройство, которым без навыка нужного уровня не воспользоваться (интерфейс не откроется).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHRequiresSkillComponent : Component
{
    [DataField(required: true)]
    public Dictionary<ProtoId<DHSkillPrototype>, int> Skills = new();
}

public sealed partial class DHSkillsSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHRequiresSkillComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<DHSkillsComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>Осмотреть себя — список своих навыков с уровнями.</summary>
    private void OnExamined(Entity<DHSkillsComponent> ent, ref ExaminedEvent args)
    {
        if (args.Examiner != args.Examined)
            return;

        var lines = new List<string>();
        foreach (var skill in _proto.EnumeratePrototypes<DHSkillPrototype>().OrderBy(s => s.ID))
        {
            var level = ent.Comp.Skills.GetValueOrDefault(skill.ID);
            lines.Add(Loc.GetString("dh-skill-examine-line", ("skill", Loc.GetString(skill.Name)),
                ("level", LevelName(level)), ("value", level)));
        }

        using (args.PushGroup(nameof(DHSkillsComponent), -10))
        {
            args.PushMarkup(Loc.GetString("dh-skill-examine-header"));
            foreach (var line in lines)
            {
                args.PushMarkup(line);
            }
        }
    }

    private void OnUiOpenAttempt(Entity<DHRequiresSkillComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || HasSkills(args.User, ent.Comp.Skills))
            return;

        args.Cancel();
        if (!args.Silent)
            PopupMissing(args.User, ent.Comp.Skills, predicted: true);
    }

    /// <summary>Ограничен ли боец навыками вообще (есть ли у него набор).</summary>
    public bool Restricted(EntityUid uid)
    {
        return HasComp<DHSkillsComponent>(uid);
    }

    /// <summary>Уровень навыка; у кого набора нет — высший (ограничений нет).</summary>
    public int GetLevel(EntityUid uid, ProtoId<DHSkillPrototype> skill)
    {
        if (!TryComp<DHSkillsComponent>(uid, out var comp))
            return _proto.TryIndex(skill, out var proto) ? proto.MaxLevel : 3;

        return comp.Skills.GetValueOrDefault(skill);
    }

    public bool HasSkill(EntityUid uid, ProtoId<DHSkillPrototype> skill, int level)
    {
        return GetLevel(uid, skill) >= level;
    }

    public bool HasSkills(EntityUid uid, Dictionary<ProtoId<DHSkillPrototype>, int> skills)
    {
        foreach (var (skill, level) in skills)
        {
            if (!HasSkill(uid, skill, level))
                return false;
        }

        return true;
    }

    /// <summary>Во сколько раз дольше (меньше 1 — быстрее) действие по навыку; у кого набора нет — 1.</summary>
    public float DelayMultiplier(EntityUid uid, ProtoId<DHSkillPrototype> skill)
    {
        if (!HasComp<DHSkillsComponent>(uid) || !_proto.TryIndex(skill, out var proto) ||
            proto.DelayMultipliers.Length == 0)
        {
            return 1f;
        }

        var level = Math.Clamp(GetLevel(uid, skill), 0, proto.DelayMultipliers.Length - 1);
        return proto.DelayMultipliers[level];
    }

    public TimeSpan ScaleDelay(EntityUid uid, ProtoId<DHSkillPrototype> skill, TimeSpan delay)
    {
        return delay * DelayMultiplier(uid, skill);
    }

    /// <summary>
    /// Подсказка, какого навыка не хватает. predicted — проверка идёт и на клиенте (показывает клиент), иначе — только
    /// на сервере (показывает сервер).
    /// </summary>
    public void PopupMissing(EntityUid user, ProtoId<DHSkillPrototype> skill, int level, bool predicted = false)
    {
        var name = _proto.TryIndex(skill, out var proto) ? Loc.GetString(proto.Name) : skill.Id;
        var text = Loc.GetString("dh-skill-required", ("skill", name), ("level", LevelName(level)));
        if (predicted)
            _popup.PopupClient(text, user, user, PopupType.SmallCaution);
        else
            _popup.PopupEntity(text, user, user, PopupType.SmallCaution);
    }

    public void PopupMissing(EntityUid user, Dictionary<ProtoId<DHSkillPrototype>, int> skills, bool predicted = false)
    {
        foreach (var (skill, level) in skills)
        {
            if (HasSkill(user, skill, level))
                continue;

            PopupMissing(user, skill, level, predicted);
            return;
        }
    }

    public static string LevelName(int level)
    {
        return Loc.GetString($"dh-skill-level-{Math.Clamp(level, 0, 3)}");
    }
}
