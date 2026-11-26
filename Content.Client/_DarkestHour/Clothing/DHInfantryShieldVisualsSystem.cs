using Content.Client.Graphics;
using Content.Client.Viewport;
using Content.Shared._DarkestHour.Clothing.ModularArmor;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.Clothing;

/// <summary>
/// Пехотный щит ЭМБ-5 (план осады п. 28) — как старый щит Monolith (решение автора 2026-11-26): голубой контур по
/// силуэту носителя (шейдер обводки поверх спрайта) и пульсирующий свет. Поднят — контур слабо мерцает, при
/// попадании ярко вспыхивает и толстеет на секунду; упал — красный контур мигает 1,5 с и гаснет; излучатель сгорел —
/// красный контур мигает 2,5 с и пропадает (решение автора 2026-11-26).
/// </summary>
public sealed partial class DHInfantryShieldVisualsSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const string PostShaderId = "dh-infantry-shield";
    private static readonly ProtoId<ShaderPrototype> ShaderProto = "DHInfantryShieldOutline";

    private static readonly Color Blue = Color.FromHex("#00AAFF");
    private static readonly Color HitBlue = Color.FromHex("#E6FAFF");
    private static readonly Color Red = Color.FromHex("#FF3A30");

    // Вспышки заметнее и дольше (решение автора 2026-11-26): попадание — 1 с, отключение — красный контур 1,5 с,
    // сгорание излучателя — 2,5 с.
    private const float HitFlash = 1f;
    private const float DownFlash = 1.5f;
    private const float BreakFlash = 2.5f;

    /// <summary>Носитель → его экземпляр шейдера (у каждого свой цвет и прозрачность).</summary>
    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();
    private readonly HashSet<EntityUid> _seen = new();
    private readonly List<EntityUid> _stale = new();

    public override void Shutdown()
    {
        base.Shutdown();
        foreach (var (wearer, shader) in _shaders)
        {
            RemoveOutline(wearer);
            shader.Dispose();
        }

        _shaders.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var t = (float) now.TotalSeconds;
        var width = (float) _eye.MainViewport.GetRenderScale();
        _seen.Clear();

        var query = EntityQueryEnumerator<DHInfantryShieldFieldComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var field, out var xform))
        {
            var color = Blue;
            var thickness = 1f; // множитель толщины контура: на вспышках контур толще
            float alpha;
            float energy;

            switch (field.State)
            {
                case DHInfantryShieldState.Up:
                    var sinceHit = (float) (now - field.LastHit).TotalSeconds;
                    // ровный контур с лёгким мерцанием и редкими «провалами», как у энергощита
                    alpha = 0.7f + 0.1f * MathF.Sin(t * 7f) - (_random.Prob(0.03f) ? 0.35f : 0f);
                    energy = 0.6f + 0.2f * MathF.Sin(t * 3f);
                    if (sinceHit < HitFlash)
                    {
                        var k = 1f - sinceHit / HitFlash;
                        k *= k; // яркий удар и плавное затухание
                        color = Color.InterpolateBetween(Blue, HitBlue, k);
                        alpha = MathF.Max(alpha, 0.7f) + 0.5f * k;
                        energy += 3f * k;
                        thickness = 1f + k;
                    }
                    break;
                case DHInfantryShieldState.Down:
                    // Отключился (кончились заряды): красный контур мигает и гаснет.
                    var sinceDown = (float) (now - field.StateChanged).TotalSeconds;
                    if (sinceDown < DownFlash)
                    {
                        var downFade = 1f - sinceDown / DownFlash;
                        color = Red;
                        alpha = ((int) (sinceDown * 8f) % 2 == 0 ? 1f : 0.55f) * (0.4f + 0.6f * downFade);
                        energy = 2f * downFade + 0.5f;
                        thickness = 2f;
                    }
                    else
                    {
                        alpha = 0f;
                        energy = 0f;
                    }
                    break;
                case DHInfantryShieldState.Broken:
                    var sinceBreak = (float) (now - field.StateChanged).TotalSeconds;
                    if (sinceBreak < BreakFlash)
                    {
                        var breakFade = 1f - sinceBreak / BreakFlash;
                        color = Red;
                        alpha = ((int) (sinceBreak * 10f) % 2 == 0 ? 1f : 0.45f) * (0.4f + 0.6f * breakFade);
                        energy = 2.5f * breakFade + 0.5f;
                        thickness = 2f;
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
            var wearer = xform.ParentUid;
            if (alpha > 0f && wearer.IsValid())
            {
                _seen.Add(wearer);
                SetOutline(wearer, color.WithAlpha(alpha), width * thickness);
            }

            if (!TryComp<PointLightComponent>(uid, out var light))
                continue;

            _lights.SetEnabled(uid, energy > 0f, light);
            if (energy > 0f)
            {
                _lights.SetColor(uid, color, light);
                _lights.SetEnergy(uid, energy, light);
            }
        }

        // Щит упал, сгорел или броню сняли — убрать контур.
        foreach (var wearer in _shaders.Keys)
        {
            if (!_seen.Contains(wearer))
                _stale.Add(wearer);
        }

        foreach (var wearer in _stale)
        {
            RemoveOutline(wearer);
            _shaders.Remove(wearer, out var shader);
            shader?.Dispose();
        }

        _stale.Clear();
    }

    private void SetOutline(EntityUid wearer, Color color, float width)
    {
        if (!TryComp<SpriteComponent>(wearer, out var sprite))
            return;

        if (!_shaders.TryGetValue(wearer, out var shader))
        {
            shader = _prototypes.Index(ShaderProto).InstanceUnique();
            shader.SetParameter("outline_fullbright", true);
            _shaders[wearer] = shader;
        }

        shader.SetParameter("outline_color", color);
        shader.SetParameter("outline_width", width);

        if (!_sprite.HasPostShader((wearer, sprite), PostShaderId))
        {
            _sprite.SetPostShader((wearer, sprite), new SpriteComponent.PostShaderArgs(PostShaderId, shader)
            {
                After = ContentPostShaderIds.AfterBaseEffects,
                Before = ContentPostShaderIds.BeforeOutlines,
            });
        }
    }

    private void RemoveOutline(EntityUid wearer)
    {
        if (!TerminatingOrDeleted(wearer) && TryComp<SpriteComponent>(wearer, out var sprite))
            _sprite.RemovePostShader((wearer, sprite), PostShaderId);
    }
}
