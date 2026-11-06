using Content.Shared.Clothing.EntitySystems;
using Content.Shared._DarkestHour.City;
using Content.Shared.Clothing.Components;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client._DarkestHour.City;

/// <summary>Заготовка случайной одежды рисуется спрайтами выбранной вещи (на полу, в руках и на теле).</summary>
public sealed partial class DHRandomOutfitVisualsSystem : EntitySystem
{
    [Dependency] private ClothingSystem _clothing = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHRandomOutfitComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<DHRandomOutfitComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<DHRandomOutfitComponent> ent, ref ComponentStartup args)
    {
        Apply(ent);
    }

    private void OnState(Entity<DHRandomOutfitComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Apply(ent);
    }

    private void Apply(Entity<DHRandomOutfitComponent> ent)
    {
        if (ent.Comp.Chosen is not { } chosen || !_proto.TryIndex(chosen, out var proto))
            return;

        // У всех вариантов спрайт предмета и одежды — один RSI (Clothing.sprite).
        if (!proto.TryGetComponent<ClothingComponent>(out var protoClothing, _factory) ||
            protoClothing.RsiPath is not { } path)
        {
            return;
        }

        // RsiPath пишет только ClothingSystem (RA0002) — копируем вид одежды варианта через неё.
        if (TryComp<ClothingComponent>(ent, out var clothing))
            _clothing.CopyVisuals(ent, protoClothing, clothing);

        if (TryComp<SpriteComponent>(ent, out var sprite) &&
            _resources.TryGetResource(SpriteSpecifierSerializer.TextureRoot / path, out RSIResource? rsi))
        {
            _sprite.SetBaseRsi((ent, sprite), rsi.RSI);
            _sprite.LayerSetRsi((ent, sprite), 0, rsi.RSI, "icon");
        }

        _item.VisualsChanged(ent);
    }
}
