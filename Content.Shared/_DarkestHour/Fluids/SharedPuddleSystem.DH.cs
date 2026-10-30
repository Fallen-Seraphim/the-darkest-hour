using Content.Shared._DarkestHour.Fluids;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Fluids;

/// <summary>DarkestHour: выбор набора спрайтов лужи (<see cref="DHPuddleSpriteSetPrototype"/>) по составу.</summary>
public abstract partial class SharedPuddleSystem
{
    private void DHUpdateSpriteSet(EntityUid uid, Solution? solution, AppearanceComponent appearance)
    {
        var best = string.Empty;
        if (solution != null && solution.Volume > FixedPoint2.Zero)
        {
            var bestQuantity = FixedPoint2.Zero;
            var other = solution.Volume;
            foreach (var set in ProtoMan.EnumeratePrototypes<DHPuddleSpriteSetPrototype>())
            {
                var quantity = FixedPoint2.Zero;
                foreach (var reagent in set.Reagents)
                {
                    quantity += solution.GetTotalPrototypeQuantity(reagent);
                }

                other -= quantity;
                if (quantity > bestQuantity)
                {
                    best = set.ID;
                    bestQuantity = quantity;
                }
            }

            // Набор — только если его реагентов больше, чем всего остального.
            if (bestQuantity <= other)
                best = string.Empty;
        }

        _appearance.SetData(uid, DHPuddleVisuals.SpriteSet, best, appearance);
    }
}
