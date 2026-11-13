using Content.Shared.Item;
using Content.Shared.Verbs;

namespace Content.Shared._DarkestHour.Ranks;

/// <summary>
/// Владелец формы может спрятать знаки различия и показать их снова (как в CMU).
/// </summary>
public sealed partial class DHRankInsigniaSystem : EntitySystem
{
    [Dependency] private SharedItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHRankInsigniaComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<DHRankInsigniaComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Sprite == null || Transform(ent).ParentUid != args.User)
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(ent.Comp.Hidden ? "dh-rank-insignia-show-verb" : "dh-rank-insignia-hide-verb"),
            Act = () =>
            {
                ent.Comp.Hidden = !ent.Comp.Hidden;
                Dirty(ent);
                _item.VisualsChanged(ent);
            },
        });
    }
}
