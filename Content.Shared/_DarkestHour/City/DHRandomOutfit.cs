using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._DarkestHour.City;

/// <summary>
/// «Случайная одежда» (решение автора 2026-11-06): предмет-заготовка при появлении становится одной из вещей списка —
/// берёт её имя, описание и спрайты (все варианты — чисто внешние, без своих компонентов). С шансом
/// <see cref="EmptyChance"/> вещи не будет совсем: заготовка удаляется, когда игрок появился (сервер).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class DHRandomOutfitComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Choices = new();

    [DataField]
    public float EmptyChance;

    [DataField, AutoNetworkedField]
    public EntProtoId? Chosen;

    [DataField, AutoNetworkedField]
    public bool Empty;
}

public sealed partial class DHRandomOutfitSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHRandomOutfitComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<DHRandomOutfitComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || ent.Comp.Chosen != null || ent.Comp.Choices.Count == 0)
            return;

        if (ent.Comp.EmptyChance > 0 && _random.Prob(ent.Comp.EmptyChance))
        {
            ent.Comp.Empty = true;
            Dirty(ent);
            return;
        }

        var chosen = _random.Pick(ent.Comp.Choices);
        ent.Comp.Chosen = chosen;
        if (_proto.TryIndex(chosen, out var proto))
        {
            _meta.SetEntityName(ent, proto.Name);
            _meta.SetEntityDescription(ent, proto.Description);
        }

        Dirty(ent);
    }
}
