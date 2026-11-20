using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Vendors;

namespace Content.Server._DarkestHour.Vendors;

/// <summary>
/// Каргоавтомат (план осады п. 27, решение автора 2026-11-20): бесплатный автомат снаряжения с общим ограниченным
/// запасом, который восстанавливается, когда снабжение пополняет за очки свой раздел карго.
/// </summary>
[RegisterComponent]
public sealed partial class DHCargoVendorComponent : Component
{
    [DataField(required: true)]
    public DHAssaultFaction Faction;

    /// <summary>Раздел каталога снабжения (его название), с которым пополняется автомат.</summary>
    [DataField(required: true)]
    public string Category = string.Empty;
}

/// <summary>Снабжение стороны пополнило раздел карго за очки.</summary>
public sealed class DHSupplyCategoryRestockedEvent(DHAssaultFaction faction, string category) : EntityEventArgs
{
    public readonly DHAssaultFaction Faction = faction;
    public readonly string Category = category;
}

public sealed class DHCargoVendorSystem : EntitySystem
{
    [Dependency] private DHGearVendorSystem _vendor = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHSupplyCategoryRestockedEvent>(OnRestocked);
    }

    private void OnRestocked(DHSupplyCategoryRestockedEvent ev)
    {
        var query = EntityQueryEnumerator<DHCargoVendorComponent, DHGearVendorComponent>();
        while (query.MoveNext(out var uid, out var cargo, out var vendor))
        {
            if (cargo.Faction == ev.Faction && cargo.Category == ev.Category)
                _vendor.Restock((uid, vendor));
        }
    }
}
