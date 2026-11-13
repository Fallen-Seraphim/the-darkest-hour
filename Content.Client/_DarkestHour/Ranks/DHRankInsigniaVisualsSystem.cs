using Content.Client.Clothing;
using Content.Shared._DarkestHour.Ranks;
using Content.Shared.Clothing;
using Content.Shared.Item;
using Robust.Shared.GameStates;

namespace Content.Client._DarkestHour.Ranks;

/// <summary>
/// Рисует знаки различия поверх надетой формы или брони.
/// </summary>
public sealed partial class DHRankInsigniaVisualsSystem : EntitySystem
{
    private const string LayerPrefix = "dh-rank-insignia";

    [Dependency] private SharedItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHRankInsigniaComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals,
            after: [typeof(ClientClothingSystem)]);
        SubscribeLocalEvent<DHRankInsigniaComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<DHRankInsigniaComponent, ComponentRemove>(OnRemove);
    }

    private void OnGetEquipmentVisuals(Entity<DHRankInsigniaComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (ent.Comp.Sprite is not { } sprite || ent.Comp.Hidden)
            return;

        var key = $"{LayerPrefix}-{args.Slot}";
        args.Layers.Add((key, new PrototypeLayerData
        {
            RsiPath = sprite.RsiPath.ToString(),
            State = sprite.RsiState,
            MapKeys = [key],
        }));
    }

    private void OnAfterState(Entity<DHRankInsigniaComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _item.VisualsChanged(ent);
    }

    private void OnRemove(Entity<DHRankInsigniaComponent> ent, ref ComponentRemove args)
    {
        if (!TerminatingOrDeleted(ent))
            _item.VisualsChanged(ent);
    }
}
