using Content.Shared._DarkestHour.Fluids;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

// ReSharper disable once CheckNamespace
namespace Content.Client.Fluids;

/// <summary>
/// DarkestHour (порт из Ephemeral Space / Goob Station): лужи из наборов <see cref="DHPuddleSpriteSetPrototype"/>
/// рисуются случайным спрайтом по объёму (капли / брызги / лужа) без сглаживания.
/// </summary>
public sealed partial class PuddleSystem
{
    [Dependency] private IPrototypeManager _dhProto = default!;

    private static readonly SpriteSpecifier.Rsi VanillaPuddle = new(new ResPath("/Textures/Fluids/puddle.rsi"), "splata");


    /// <returns>true — лужа нарисована набором, ванильную отрисовку пропустить.</returns>
    private bool DHUpdatePuddleSprite(EntityUid uid, PuddleComponent puddle, ref AppearanceChangeEvent args, float volume)
    {
        var sprite = args.Sprite!;
        if (!args.TryGetData<string>(DHPuddleVisuals.SpriteSet, out var setId) || string.IsNullOrEmpty(setId) ||
            !_dhProto.TryIndex<DHPuddleSpriteSetPrototype>(setId, out var set))
        {
            // Лужа была кровью, а стала чем-то другим — вернуть ванильный спрайт.
            if (_sprite.LayerMapTryGet((uid, sprite), puddle.PuddleKey, out var index, false) &&
                sprite[index] is SpriteComponent.Layer { ActualRsi: { } rsi } &&
                rsi.Path != VanillaPuddle.RsiPath)
            {
                _sprite.LayerSetSprite((uid, sprite), puddle.PuddleKey, VanillaPuddle);
                _sprite.LayerSetRotation((uid, sprite), puddle.PuddleKey, Angle.Zero);
            }

            return false;
        }

        var (sprites, size) = volume switch
        {
            < LowThreshold => (set.SmallSprites, 0),
            < MediumThreshold => (set.MediumSprites, 1),
            _ => (set.LargeSprites, 2),
        };

        _smooth.SetEnabled(uid, false, false);

        if (sprites.Count > 0)
        {
            // Один и тот же спрайт для лужи одного размера — иначе мигала бы при каждом обновлении.
            var seed = HashCode.Combine(GetNetEntity(uid).Id, size);
            var random = new System.Random(seed);
            _sprite.LayerSetSprite((uid, sprite), puddle.PuddleKey, sprites[random.Next(sprites.Count)]);
            _sprite.LayerSetRotation((uid, sprite), puddle.PuddleKey, Angle.FromDegrees(90 * random.Next(4)));
        }

        if (set.Recolor && args.TryGetData<Color>(PuddleVisuals.SolutionColor, out var color))
            _sprite.SetColor((uid, sprite), color.WithAlpha(1f) * set.BaseColor);
        else
            _sprite.SetColor((uid, sprite), set.BaseColor);

        return true;
    }
}
