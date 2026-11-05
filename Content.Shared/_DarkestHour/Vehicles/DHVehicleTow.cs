using System.Numerics;

namespace Content.Shared._DarkestHour.Vehicles;

// Буксировка грузовиком (решение автора 2026-11-04): пехотные пушки и зенитки цепляют к грузовику, и они едут за ним.

/// <summary>Машина с фаркопом: к ней можно прицепить орудие на колёсах.</summary>
[RegisterComponent]
public sealed partial class DHVehicleTowComponent : Component
{
    /// <summary>Расстояние от центра машины до прицепа.</summary>
    [DataField]
    public float Distance = 2f;

    /// <summary>Как близко к машине должно стоять орудие, чтобы его прицепить.</summary>
    [DataField]
    public float HitchRange = 3.5f;

    /// <summary>Что прицеплено (сервер).</summary>
    [ViewVariables]
    public EntityUid? Towed;
}

/// <summary>Орудие на колёсах, которое можно прицепить к машине с фаркопом (не закреплённое ключом).</summary>
[RegisterComponent]
public sealed partial class DHHitchableComponent : Component
{
    /// <summary>К какой машине прицеплено (сервер).</summary>
    [ViewVariables]
    public EntityUid? Vehicle;
}
