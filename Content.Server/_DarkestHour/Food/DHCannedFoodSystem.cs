using Content.Shared._DarkestHour.Food;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Food;

/// <summary>Консерва открывается ножом (план осады п. 27, решение автора 2026-11-20): нож по банке — 2 с.</summary>
[RegisterComponent]
public sealed partial class DHCannedFoodComponent : Component
{
    [DataField]
    public ProtoId<TagPrototype> Tool = "Knife";

    [DataField]
    public TimeSpan OpenTime = TimeSpan.FromSeconds(2);
}

public sealed partial class DHCannedFoodSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private OpenableSystem _openable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHCannedFoodComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DHCannedFoodComponent, DHCanOpenDoAfterEvent>(OnOpenDoAfter);
    }

    private void OnInteractUsing(Entity<DHCannedFoodComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tag.HasTag(args.Used, ent.Comp.Tool) ||
            !TryComp<OpenableComponent>(ent, out var openable) || openable.Opened)
        {
            return;
        }

        args.Handled = true;
        if (_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.OpenTime,
                new DHCanOpenDoAfterEvent(), ent, target: ent, used: args.Used)
            {
                BreakOnMove = true,
                NeedHand = true,
            }))
        {
            _popup.PopupEntity(Loc.GetString("dh-can-opening"), ent, args.User);
        }
    }

    private void OnOpenDoAfter(Entity<DHCannedFoodComponent> ent, ref DHCanOpenDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        _openable.TryOpen(ent);
    }
}
