// Перенесено из RussianCM (CMU14, github.com/flex5hybrid/RussianCM, AGPL v3): CMUItemStainComponent.
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Stains;

/// <summary>
/// Пятно на предмете (кровь на одежде, броне, оружии). Решение автора 2026-11-11: кровь на снаряжении, как в CMU.
/// Рисуется на иконке предмета (маска по силуэту) и на надетой вещи (маска слота из item_stains.rsi).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(DHItemStainSystem))]
public sealed partial class DHItemStainComponent : Component
{
    /// <summary>Можно ли запачкать (false — как флаг NOBLOODY в CMSS13).</summary>
    [DataField]
    public bool CanStain = true;

    /// <summary>Своя маска пятна на теле по слоту (например, outerClothing: armor_blood).</summary>
    [DataField]
    [Access(typeof(DHItemStainSystem), Other = AccessPermissions.ReadExecute)]
    public Dictionary<string, string> WornStates = new();

    [DataField, AutoNetworkedField]
    public DHItemStainKind Kind = DHItemStainKind.Blood;

    /// <summary>null — чистый.</summary>
    [DataField, AutoNetworkedField]
    public Color? Color;
}

[Serializable, NetSerializable]
public enum DHItemStainKind : byte
{
    Blood,
    Oil,
}
