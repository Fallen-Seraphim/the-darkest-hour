// Перенесено из RMC-14 (github.com/RMC-14/RMC-14, MIT): SharedRankSystem (без ксеноморфов и иерархии званий).
using Content.Shared._DarkestHour.Ranks;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Marines.Roles.Ranks;

public abstract partial class SharedRankSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RankComponent, ExaminedEvent>(OnRankExamined);
    }

    private void OnRankExamined(Entity<RankComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(SharedRankSystem), 1))
        {
            var rank = GetRankString(ent, hasPaygrade: true);
            if (rank != null)
                args.PushMarkup(Loc.GetString("rmc-rank-component-examine", ("user", ent.Owner), ("rank", rank)));
        }
    }

    /// <summary>
    ///     Sets a mob's rank from the given RankPrototype.
    /// </summary>
    public void SetRank(EntityUid uid, ProtoId<RankPrototype> from)
    {
        var comp = EnsureComp<RankComponent>(uid);
        comp.Rank = from;
        Dirty(uid, comp);
        RankChanged(uid);
    }

    /// <summary>
    ///     Called after the rank changed (the server puts the insignia on the worn clothing).
    /// </summary>
    protected virtual void RankChanged(EntityUid uid)
    {
    }

    public RankPrototype? GetRank(EntityUid uid)
    {
        return TryComp<RankComponent>(uid, out var component) ? GetRank(component) : null;
    }

    public RankPrototype? GetRank(RankComponent component)
    {
        return component.Rank is { } id && _prototypes.TryIndex(id, out var rank) ? rank : null;
    }

    /// <summary>
    ///     The rank name, the short form (prefix) or the name with the pay grade: «(E3) Младший капрал».
    /// </summary>
    public string? GetRankString(EntityUid uid, bool isShort = false, bool hasPaygrade = false)
    {
        var rank = GetRank(uid);
        if (rank == null)
            return null;

        if (isShort)
            return Loc.GetString(rank.Prefix);

        if (hasPaygrade && rank.Paygrade != null)
            return $"({rank.Paygrade}) {Loc.GetString(rank.Name)}";

        return Loc.GetString(rank.Name);
    }

    /// <summary>
    ///     The name with the rank prefix: «Серж. Джон Доу».
    /// </summary>
    public string? GetSpeakerRankName(EntityUid uid, string name)
    {
        var rank = GetRankString(uid, true);
        return rank == null ? null : $"{rank} {name}";
    }
}
