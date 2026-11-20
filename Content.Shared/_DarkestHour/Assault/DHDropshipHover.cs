// Перенесено из RussianCM (CMU14, github.com/flex5hybrid/RussianCM, AGPL v3): DropshipTacticalHoverShadowComponent,
// DropshipTacticalHoverDownwashComponent, DropshipShadowTile, DropshipShadowPart. Добавлены сила эффекта по высоте
// и скрытие под крышей.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Assault;

/// <summary>
/// Тень челнока на земле (или на крыше) под ним, пока он в воздухе над полем боя (решение автора 2026-11-19).
/// Сам челнок на уровне воздуха снизу не виден — видна его тень-силуэт, собранная из тайлов и деталей грида.
/// Сущность стоит в начале координат грида челнока с его поворотом; <see cref="HullBounds"/> — в координатах грида.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHDropshipHoverShadowComponent : Component
{
    [AutoNetworkedField]
    public Box2 HullBounds;

    [AutoNetworkedField]
    public List<DHDropshipShadowTile> Tiles = new();

    [AutoNetworkedField]
    public List<DHDropshipShadowPart> Parts = new();
}

/// <summary>Струя подъёмного двигателя челнока и её пятно на земле (шейдер DHDropshipHoverExhaust).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHDropshipHoverDownwashComponent : Component
{
    /// <summary>Положение сопла в координатах грида челнока: фаза анимации у каждого своя.</summary>
    [AutoNetworkedField]
    public Vector2 Offset;
}

/// <summary>
/// Меняющаяся часть эффекта (тени и струи): отдельно от силуэта, чтобы раз в секунду не слать весь снимок грида.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHDropshipHoverEffectComponent : Component
{
    /// <summary>0–1: чем ниже челнок над этим уровнем, тем гуще тень, ярче струя и громче рёв.</summary>
    [AutoNetworkedField]
    public float Strength = 1f;

    /// <summary>Над точкой крыша (или на этом этаже нет пола): эффект виден на другом уровне.</summary>
    [AutoNetworkedField]
    public bool Hidden;
}

/// <summary>Снимок тайла для тени: снизу видно силуэт, не получая сам грид с верхнего уровня.</summary>
[Serializable, NetSerializable]
public readonly record struct DHDropshipShadowTile(Vector2 Position, string Texture, byte Variant);

[Serializable, NetSerializable]
public readonly record struct DHDropshipShadowPart(string Prototype, Vector2 Position, Angle Rotation);
