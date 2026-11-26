using Content.Server.Explosion.EntitySystems;
using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.Body;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Medical.Trauma;

/// <summary>
/// Протезы и неразорвавшиеся боеприпасы (план осады п. 18, решения автора 2026-11-20):
/// <list type="bullet">
/// <item>протез — конечность-предмет, приживляется обычной операцией «приживление конечности»: не кровит, не ломается,
/// нервов нет, боль из него не идёт;</item>
/// <item>неразорвавшийся боеприпас — изредка застревает в теле от взрыва. Больно; сильный удар или новый взрыв может
/// его подорвать. Извлекается операцией: без сапёра рядом — 25% подрыв, с сапёром — 5%.</item>
/// </list>
/// </summary>
public sealed partial class DHTraumaSystem
{
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    private const float UnexplodedChance = 0.04f;
    private const float UnexplodedPain = 10f;
    private const float ExtractRisk = 0.25f;
    private const float ExtractRiskWithSapper = 0.05f;
    private const float SapperRange = 3f;

    /// <summary>Сапёры: их присутствие рядом снижает риск при извлечении.</summary>
    private static readonly HashSet<ProtoId<JobPrototype>> Sappers = new() { "DHGarrisonSapper", "DHMarineWeaponsSpecialist" };

    private void InitializeProsthetics()
    {
        SubscribeLocalEvent<DHTraumaComponent, OrganInsertedIntoEvent>(OnOrganInserted);
    }

    #region Протезы

    private void OnOrganInserted(Entity<DHTraumaComponent> ent, ref OrganInsertedIntoEvent args)
    {
        if (!HasComp<DHProstheticComponent>(args.Organ) || !_organQuery.TryComp(args.Organ, out var organ) ||
            organ.Category is not { } category)
        {
            return;
        }

        // Новая конечность — без старых травм.
        ent.Comp.Parts.Remove(category);
    }

    private bool IsProsthetic(Dictionary<string, EntityUid> organs, string category)
    {
        return organs.TryGetValue(category, out var organ) && HasComp<DHProstheticComponent>(organ);
    }

    #endregion

    #region Неразорвавшиеся боеприпасы

    /// <summary>Взрыв: изредка боеприпас (граната, мина, снаряд подствольника) застревает, не взорвавшись.</summary>
    private void TryUnexploded(Entity<DHTraumaComponent> ent, string category, DHPartTrauma part, float brute,
        bool explosion)
    {
        if (!explosion || brute < 10f || part.Unexploded || !_random.Prob(UnexplodedChance))
            return;

        foreach (var other in ent.Comp.Parts.Values)
        {
            if (other.Unexploded)
                return;
        }

        part.Unexploded = true;
        _popup.PopupEntity(Loc.GetString("dh-trauma-unexploded-self", ("part", PartName(category))), ent, ent,
            PopupType.LargeCaution);
    }

    /// <summary>Сильный удар или новый взрыв по части с боеприпасом — может подорвать его.</summary>
    private bool TryJoltUnexploded(Entity<DHTraumaComponent> ent, string category, DHPartTrauma part, float blunt,
        bool explosion)
    {
        if (!part.Unexploded || blunt < 20f && !explosion || !_random.Prob(0.3f))
            return false;

        Detonate(ent, category);
        return true;
    }

    private void Detonate(Entity<DHTraumaComponent> ent, string category)
    {
        if (ent.Comp.Parts.TryGetValue(category, out var part))
            part.Unexploded = false;

        _popup.PopupEntity(Loc.GetString("dh-trauma-unexploded-detonate", ("target", ent.Owner)), ent,
            PopupType.LargeCaution);
        _explosion.QueueExplosion(ent.Owner, "Default", 20f, 4f, 6f, canCreateVacuum: false);
    }

    public bool HasUnexploded(EntityUid uid, string category) =>
        TryComp<DHTraumaComponent>(uid, out var comp) && comp.Parts.TryGetValue(category, out var p) && p.Unexploded;

    /// <summary>Извлечение: может подорваться, сапёр рядом (или сам хирург-сапёр) снижает риск.</summary>
    public void ExtractUnexploded(EntityUid uid, string category, EntityUid surgeon)
    {
        if (!TryComp<DHTraumaComponent>(uid, out var comp) || !comp.Parts.TryGetValue(category, out var part) ||
            !part.Unexploded)
        {
            return;
        }

        var risk = SapperNearby(uid, surgeon) ? ExtractRiskWithSapper : ExtractRisk;
        if (_random.Prob(risk))
        {
            Detonate((uid, comp), category);
            return;
        }

        part.Unexploded = false;
        _popup.PopupEntity(Loc.GetString("dh-trauma-unexploded-extracted", ("user", surgeon), ("target", uid)), uid,
            PopupType.Medium);
    }

    private bool SapperNearby(EntityUid patient, EntityUid surgeon)
    {
        if (IsSapper(surgeon))
            return true;

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(patient).Coordinates, SapperRange))
        {
            if (mob.Owner != patient && IsSapper(mob) && !_mobState.IsIncapacitated(mob))
                return true;
        }

        return false;
    }

    /// <summary>Сапёр — навык «Сапёрное дело» от специалиста (план осады п. 12); без наборов навыков — по должности.</summary>
    private bool IsSapper(EntityUid uid)
    {
        if (_skills.Restricted(uid))
            return _skills.HasSkill(uid, DemolitionsSkill, 2);

        return _mind.TryGetMind(uid, out var mindId, out _) &&
               _jobs.MindTryGetJobId(mindId, out var job) && job is { } id && Sappers.Contains(id);
    }

    private static readonly ProtoId<Content.Shared._DarkestHour.Skills.DHSkillPrototype> DemolitionsSkill = "DHSkillDemolitions";

    #endregion
}
