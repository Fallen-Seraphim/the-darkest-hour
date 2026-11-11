using Content.Shared._DarkestHour.Medical.Trauma;
using Robust.Client.GameObjects;

namespace Content.Client._DarkestHour.Medical;

/// <summary>Иссохшая отделённая часть тела темнеет и буреет.</summary>
public sealed partial class DHLimbDecayVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly Color WitheredTint = Color.FromHex("#8a7560");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHLimbDecayComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(Entity<DHLimbDecayComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetColor((ent.Owner, sprite), ent.Comp.Withered ? WitheredTint : Color.White);
    }
}
