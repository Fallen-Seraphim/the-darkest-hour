using Content.Client.IconSmoothing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._DarkestHour.IconSmoothing;

/// <summary>
/// DarkestHour: сглаживание по сторонам в старой нумерации состояний (как IconSmooth mode: CardinalFlags у RMC-14 и
/// старого SS14): север = 1, юг = 2, восток = 4, запад = 8. Окна, рамы и заборы RMC нарисованы под неё — ванильный
/// <c>DirectionSpriteSmoothing</c> считает биты в другом порядке.
/// </summary>
public sealed partial class DHCardinalFlagsSpriteSmoothing : ISpriteSmoothState
{
    [DataField(required: true)]
    public string Base { get; set; } = string.Empty;

    [DataField(required: true)]
    public HashSet<string> Mask { get; set; } = new();

    [DataField]
    public string LayerKey { get; set; } = "cardinal";

    [DataField]
    public int? Index { get; set; }

    [DataField]
    public ProtoId<ShaderPrototype>? Shader { get; set; }

    // Индексы соседей в массиве IconSmoothSystem (по Direction8Flag): юг 0, восток 2, север 4, запад 6.
    private static readonly (int Neighbour, byte Flag)[] Sides = [(4, 1), (0, 2), (2, 4), (6, 8)];

    public void InitializeStates(Entity<SpriteComponent> entity, SpriteSystem sprite)
    {
        sprite.LayerMapSet(entity.AsNullable(), LayerKey, sprite.AddRsiLayer(entity.AsNullable(), Base + 0, index: Index));
    }

    public IEnumerable<SpriteLayer> EnumerateStates(HashSet<string>?[] layers, Entity<SpriteComponent> entity, SpriteSystem sprite)
    {
        byte match = 0;
        foreach (var (neighbour, flag) in Sides)
        {
            if (layers[neighbour] is { } keys && keys.Overlaps(Mask))
                match |= flag;
        }

        yield return new SpriteLayer(LayerKey, Base + match);
    }
}
