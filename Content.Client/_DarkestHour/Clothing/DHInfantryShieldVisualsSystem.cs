using Content.Shared._DarkestHour.Clothing.ModularArmor;
using Robust.Client.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.Clothing;

/// <summary>
/// Поле пехотного щита ЭМБ-5 (план осады п. 28): поднятое — всегда слабо мерцает голубым, при попадании вспыхивает;
/// упало — гаснет; излучатель сгорел — короткая красная вспышка и тишина.
/// </summary>
public sealed partial class DHInfantryShieldVisualsSystem : EntitySystem
{
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly Color Blue = Color.FromHex("#4fb8ff");
    private static readonly Color Red = Color.FromHex("#ff3a30");

    private const float HitFlash = 0.35f;
    private const float BreakFlash = 0.9f;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var t = (float) now.TotalSeconds;
        var query = EntityQueryEnumerator<DHInfantryShieldFieldComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var field, out var sprite))
        {
            TryComp<PointLightComponent>(uid, out var light);
            var color = Blue;
            float alpha;
            float energy;

            switch (field.State)
            {
                case DHInfantryShieldState.Up:
                    var sinceHit = (float) (now - field.LastHit).TotalSeconds;
                    // постоянное слабое мерцание с редкими «провалами»
                    alpha = 0.16f + 0.06f * MathF.Sin(t * 7f) + (_random.Prob(0.04f) ? -0.1f : 0f);
                    energy = 0.5f + 0.15f * MathF.Sin(t * 7f);
                    if (sinceHit < HitFlash)
                    {
                        var k = 1f - sinceHit / HitFlash;
                        alpha += 0.55f * k;
                        energy += 1.5f * k;
                    }
                    break;
                case DHInfantryShieldState.Broken:
                    var sinceBreak = (float) (now - field.StateChanged).TotalSeconds;
                    if (sinceBreak < BreakFlash)
                    {
                        color = Red;
                        alpha = (int) (sinceBreak * 10f) % 2 == 0 ? 0.6f : 0.15f;
                        energy = 1.2f;
                    }
                    else
                    {
                        alpha = 0f;
                        energy = 0f;
                    }
                    break;
                default:
                    alpha = 0f;
                    energy = 0f;
                    break;
            }

            alpha = Math.Clamp(alpha, 0f, 1f);
            _sprite.SetVisible((uid, sprite), alpha > 0f);
            _sprite.SetColor((uid, sprite), color.WithAlpha(alpha));

            if (light == null)
                continue;

            _lights.SetEnabled(uid, energy > 0f, light);
            if (energy > 0f)
            {
                _lights.SetColor(uid, color, light);
                _lights.SetEnergy(uid, energy, light);
            }
        }
    }
}
