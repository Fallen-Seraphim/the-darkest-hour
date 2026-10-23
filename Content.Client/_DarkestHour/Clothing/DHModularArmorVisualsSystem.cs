using Content.Client.Clothing;
using Content.Shared._DarkestHour.Clothing.ModularArmor;
using Content.Shared.Clothing;
using Content.Shared.Item;
using Robust.Shared.Containers;

namespace Content.Client._DarkestHour.Clothing;

/// <summary>
/// Модули брони рисуются отдельными слоями поверх корпуса на персонаже: видно, что надето.
/// Разбитый модуль затемнён. Смену модулей (вставка/извлечение) отслеживает общая система брони — повторная
/// подписка на те же события здесь уронила бы клиент.
/// </summary>
public sealed partial class DHModularArmorVisualsSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedItemSystem _item = default!;

    private static readonly Color BrokenTint = Color.FromHex("#7a6f66");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHModularArmorComponent, GetEquipmentVisualsEvent>(OnGetVisuals,
            after: [typeof(ClientClothingSystem)]);
        SubscribeLocalEvent<DHArmorModuleComponent, AfterAutoHandleStateEvent>(OnModuleState);
    }

    private void OnModuleState(Entity<DHArmorModuleComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_container.TryGetContainingContainer((ent, null, null), out var container) &&
            HasComp<DHModularArmorComponent>(container.Owner))
        {
            _item.VisualsChanged(container.Owner);
        }
    }

    private void OnGetVisuals(Entity<DHModularArmorComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        foreach (var slot in DHModularArmor.Slots)
        {
            if (!_container.TryGetContainer(ent, DHModularArmor.ContainerId(slot), out var container) ||
                container.ContainedEntities.Count == 0 ||
                !TryComp<DHArmorModuleComponent>(container.ContainedEntities[0], out var module) ||
                module.EquippedRsi == null || module.EquippedState == null)
            {
                continue;
            }

            var layer = new PrototypeLayerData
            {
                RsiPath = module.EquippedRsi,
                State = module.EquippedState,
            };
            if (module.Broken)
                layer.Color = BrokenTint;

            args.Layers.Add(($"{args.Slot}-dh-armor-{slot}", layer));
        }
    }
}
