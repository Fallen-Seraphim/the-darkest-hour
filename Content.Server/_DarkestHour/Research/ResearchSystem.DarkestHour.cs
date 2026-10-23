using Content.Shared._DarkestHour.Research.Materials;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Research.Systems;

/// <summary>
/// DarkestHour: цена технологии с учётом скидок реверс-инжиниринга (<see cref="DHResearchDiscountComponent"/> на
/// сервере РнД).
/// </summary>
public sealed partial class ResearchSystem
{
    public int GetTechnologyCost(EntityUid? server, TechnologyPrototype technology)
    {
        if (server == null || !TryComp<DHResearchDiscountComponent>(server, out var discounts) ||
            !discounts.Discounts.TryGetValue(technology.ID, out var discount))
        {
            return technology.Cost;
        }

        return (int) MathF.Round(technology.Cost * (1f - Math.Clamp(discount, 0f, discounts.MaxDiscount)));
    }

    /// <summary>Опытные образцы: пробный период для рецептов изученной технологии.</summary>
    private void DHStartPrototypeTrial(EntityUid server, TechnologyPrototype technology)
    {
        EntityManager.System<Content.Server._DarkestHour.Research.Prototypes.DHPrototypeSystem>().StartTrial(server, technology);
    }

    /// <summary>Добавить скидку на технологию. Возвращает итоговую скидку (с учётом предела).</summary>
    public float AddTechnologyDiscount(EntityUid server, ProtoId<TechnologyPrototype> technology, float discount)
    {
        var comp = EnsureComp<DHResearchDiscountComponent>(server);
        var total = Math.Min(comp.MaxDiscount, comp.Discounts.GetValueOrDefault(technology) + discount);
        comp.Discounts[technology] = total;
        return total;
    }
}
