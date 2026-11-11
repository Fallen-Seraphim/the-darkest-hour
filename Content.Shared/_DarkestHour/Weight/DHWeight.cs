using Content.Shared._DarkestHour.Clothing.ModularArmor;
using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Movement.Systems;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Weight;

// Вес и нагрузка (решение автора 2026-11-11): каждый предмет весит (кг). Без явного веса — по размеру предмета.
// Рюкзак и РПС (пояс-разгрузка) снижают вес того, что в них лежит. Нагрузка носителя: до 25 кг без штрафа, дальше —
// замедление (до ×0,6 к 50 кг), рывок тратит больше выносливости, при сильном перегрузе рывка нет. Экзоскелет носит
// себя и свои пластины сам и снижает нагрузку от остального (DHExoskeletonComponent.LoadFactor).

/// <summary>Явный вес предмета, кг. Без компонента (или с пустым весом) — по размеру предмета.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHWeightComponent : Component
{
    [DataField]
    public float? Weight;
}

/// <summary>
/// Разгрузочная снаряжка: надетая, снижает вес содержимого. Без компонента надетые хранилища в слоте спины
/// (рюкзак) и пояса (РПС) получают множитель по умолчанию.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHLoadBearingComponent : Component
{
    /// <summary>Доля веса содержимого, которая ложится на носителя.</summary>
    [DataField]
    public float Factor = 0.6f;
}

/// <summary>Нагрузка существа. Считает сервер раз в секунду; скорость и рывок предсказываются клиентом.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class DHEncumbranceComponent : Component
{
    /// <summary>До этой нагрузки штрафа нет, кг.</summary>
    [DataField]
    public float Free = 25f;

    /// <summary>Полный перегруз, кг: замедление максимальное.</summary>
    [DataField]
    public float Max = 50f;

    /// <summary>Множитель скорости при полном перегрузе.</summary>
    [DataField]
    public float MinSpeed = 0.6f;

    /// <summary>С какой доли перегруза (от Free до Max) рывка нет.</summary>
    [DataField]
    public float NoSprintFraction = 0.6f;

    /// <summary>Насколько дороже рывок при полном перегрузе (1,5 — в 2,5 раза).</summary>
    [DataField]
    public float StaminaPerExcess = 1.5f;

    /// <summary>Нагрузка с учётом разгрузки и экзоскелета, кг.</summary>
    [DataField, AutoNetworkedField]
    public float Load;

    /// <summary>Вес всего надетого и взятого без поправок, кг (для осмотра).</summary>
    [DataField, AutoNetworkedField]
    public float Carried;

    /// <summary>Доля перегруза: 0 — до порога, 1 — полный.</summary>
    public float Excess => Max <= Free ? 0f : Math.Clamp((Load - Free) / (Max - Free), 0f, 1f);

    public bool NoSprint => Load > Free && Excess >= NoSprintFraction;
}

public sealed partial class DHWeightSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    public const string BackSlot = "back";
    public const string BeltSlot = "belt";

    /// <summary>Рюкзак без своего множителя.</summary>
    public const float BackpackFactor = 0.6f;

    /// <summary>РПС (поясная разгрузка) без своего множителя.</summary>
    public const float WebbingFactor = 0.5f;

    private static readonly Dictionary<string, float> SizeWeights = new()
    {
        ["Tiny"] = 0.1f,
        ["Small"] = 0.3f,
        ["Normal"] = 1f,
        ["Large"] = 3f,
        ["Huge"] = 6f,
        ["Ginormous"] = 12f,
    };

    private EntityQuery<DHWeightComponent> _weightQuery;
    private EntityQuery<ItemComponent> _itemQuery;
    private EntityQuery<ContainerManagerComponent> _containersQuery;
    private EntityQuery<DHExoskeletonComponent> _exoQuery;

    public override void Initialize()
    {
        base.Initialize();

        _weightQuery = GetEntityQuery<DHWeightComponent>();
        _itemQuery = GetEntityQuery<ItemComponent>();
        _containersQuery = GetEntityQuery<ContainerManagerComponent>();
        _exoQuery = GetEntityQuery<DHExoskeletonComponent>();

        SubscribeLocalEvent<DHWeightComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DHEncumbranceComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<DHEncumbranceComponent, AfterAutoHandleStateEvent>(OnEncumbranceState);
        SubscribeLocalEvent<DHEncumbranceComponent, ExaminedEvent>(OnSelfExamined);
    }

    /// <summary>Вес самого предмета без содержимого, кг.</summary>
    public float OwnWeight(EntityUid uid)
    {
        if (_weightQuery.TryComp(uid, out var weight) && weight.Weight is { } explicitWeight)
            return explicitWeight;

        if (!_itemQuery.TryComp(uid, out var item))
            return 0f;

        return SizeWeights.TryGetValue(item.Size.Id, out var bySize) ? bySize : 1f;
    }

    /// <summary>Вес содержимого предмета (всех его контейнеров), кг, без поправок на разгрузку.</summary>
    public float ContentsWeight(EntityUid uid, int depth = 0)
    {
        if (depth > 8 || !_containersQuery.TryComp(uid, out var manager))
            return 0f;

        var total = 0f;
        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                total += TotalWeight(contained, depth + 1);
            }
        }

        return total;
    }

    /// <summary>Вес предмета вместе с содержимым, кг.</summary>
    public float TotalWeight(EntityUid uid, int depth = 0)
    {
        if (!_itemQuery.HasComp(uid) && !_weightQuery.HasComp(uid) || HasComp<VirtualItemComponent>(uid))
            return 0f;

        return OwnWeight(uid) + ContentsWeight(uid, depth);
    }

    /// <summary>Множитель веса содержимого надетой вещи (рюкзак, РПС); 1 — без разгрузки.</summary>
    public float LoadBearingFactor(EntityUid item, string slot)
    {
        if (TryComp<DHLoadBearingComponent>(item, out var bearing))
            return bearing.Factor;

        if (!HasComp<StorageComponent>(item))
            return 1f;

        return slot switch
        {
            BackSlot => BackpackFactor,
            BeltSlot => WebbingFactor,
            _ => 1f,
        };
    }

    /// <summary>
    /// Нагрузка существа: всё надетое и взятое в руки. Надетый экзоскелет носит себя, свои пластины и шлем сам —
    /// считается только то, что лежит в его хранилищах; остальная нагрузка умножается на его разгрузку.
    /// </summary>
    public (float Load, float Carried) ComputeLoad(EntityUid mob)
    {
        var carried = 0f;
        var load = 0f;
        DHExoskeletonComponent? exo = null;
        EntityUid? exoUid = null;

        if (_inventory.TryGetSlotEntity(mob, "outerClothing", out var outer) && _exoQuery.TryComp(outer, out var exoComp))
        {
            exo = exoComp;
            exoUid = outer;
        }

        var slots = _inventory.GetSlotEnumerator(mob);
        while (slots.NextItem(out var item, out var slot))
        {
            var own = OwnWeight(item);
            var contents = ContentsWeight(item);
            carried += own + contents;

            if (item == exoUid)
            {
                // Пластины, шлем и спецсистема — часть экзоскелета; вещи внутри спецсистемы — груз.
                if (_containersQuery.TryComp(item, out var manager))
                {
                    foreach (var container in manager.Containers.Values)
                    {
                        foreach (var part in container.ContainedEntities)
                        {
                            load += ContentsWeight(part);
                        }
                    }
                }

                continue;
            }

            if (exoUid != null && TryComp<AttachedClothingComponent>(item, out var attached) &&
                attached.AttachedUid == exoUid)
            {
                continue;
            }

            if (HasComp<DHArmorLimbCoverComponent>(item))
                continue;

            load += own + contents * LoadBearingFactor(item, slot.Name);
        }

        foreach (var held in _hands.EnumerateHeld(mob))
        {
            var weight = TotalWeight(held);
            carried += weight;
            load += weight;
        }

        if (exo != null)
            load *= exo.LoadFactor;

        return (load, carried);
    }

    private void OnEncumbranceState(Entity<DHEncumbranceComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnRefreshSpeed(Entity<DHEncumbranceComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var excess = ent.Comp.Excess;
        if (excess <= 0f)
            return;

        var speed = 1f - (1f - ent.Comp.MinSpeed) * excess;
        args.ModifySpeed(speed, speed);
    }

    private void OnExamined(Entity<DHWeightComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var own = OwnWeight(ent);
        var contents = ContentsWeight(ent);
        args.PushMarkup(contents > 0.05f
            ? Loc.GetString("dh-weight-examine-with-contents", ("weight", Format(own + contents)), ("own", Format(own)))
            : Loc.GetString("dh-weight-examine", ("weight", Format(own))), -5);
    }

    private void OnSelfExamined(Entity<DHEncumbranceComponent> ent, ref ExaminedEvent args)
    {
        if (args.Examiner != args.Examined || ent.Comp.Carried <= 0.05f)
            return;

        args.PushMarkup(Loc.GetString("dh-weight-examine-self", ("load", Format(ent.Comp.Load)),
            ("carried", Format(ent.Comp.Carried)), ("free", Format(ent.Comp.Free))), -5);
    }

    public static string Format(float kg)
    {
        return kg >= 10f ? MathF.Round(kg).ToString("0") : kg.ToString("0.0");
    }

    /// <summary>Новая нагрузка (сервер): обновляет скорость, если она поменялась заметно.</summary>
    public void SetLoad(Entity<DHEncumbranceComponent> ent, float load, float carried)
    {
        if (MathF.Abs(ent.Comp.Load - load) < 0.05f && MathF.Abs(ent.Comp.Carried - carried) < 0.05f)
            return;

        var oldExcess = ent.Comp.Excess;
        ent.Comp.Load = load;
        ent.Comp.Carried = carried;
        Dirty(ent);

        if (MathF.Abs(oldExcess - ent.Comp.Excess) > 0.001f)
            _movement.RefreshMovementSpeedModifiers(ent);
    }
}
