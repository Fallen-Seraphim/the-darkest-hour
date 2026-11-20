// Перенесено из RussianCM (CMU14, github.com/flex5hybrid/RussianCM, AGPL v3): DropshipTacticalHoverOverlay.
// Убраны манёвровое отклонение струй и огни сопел; добавлены сила эффекта по высоте и скрытие под крышей.
using System.Numerics;
using Content.Shared._DarkestHour.Assault;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._DarkestHour.Assault;

/// <summary>Тень-силуэт челнока (один кэшированный рендер-таргет на тень) и струи подъёмных двигателей на земле.</summary>
public sealed class DHDropshipHoverOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    private const float PixelsPerMeter = 32f;
    private static readonly ProtoId<ShaderPrototype> ExhaustShader = "DHDropshipHoverExhaust";

    private readonly IEntityManager _entities;
    private readonly IClyde _clyde;
    private readonly IResourceCache _resources;
    private readonly IPrototypeManager _prototypes;
    private readonly IComponentFactory _factory;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprites;
    private readonly Dictionary<EntityUid, (IRenderTexture Texture, Box2 Bounds, int Tiles, int Parts)> _silhouettes = new();
    private readonly ShaderInstance _exhaust;

    public DHDropshipHoverOverlay(IEntityManager entities)
    {
        _entities = entities;
        _clyde = IoCManager.Resolve<IClyde>();
        _resources = IoCManager.Resolve<IResourceCache>();
        _prototypes = IoCManager.Resolve<IPrototypeManager>();
        _factory = IoCManager.Resolve<IComponentFactory>();
        _timing = IoCManager.Resolve<IGameTiming>();
        _transform = entities.System<SharedTransformSystem>();
        _sprites = entities.System<SpriteSystem>();
        _exhaust = _prototypes.Index(ExhaustShader).InstanceUnique();
        // Поверх покрытия пола, но под стенами и людьми.
        ZIndex = (int) Content.Shared.DrawDepth.DrawDepth.HighFloorObjects + 1;
    }

    public void Invalidate(EntityUid uid)
    {
        if (_silhouettes.Remove(uid, out var cached))
            cached.Texture.Dispose();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var h = args.WorldHandle;
        var shadows = _entities
            .EntityQueryEnumerator<DHDropshipHoverShadowComponent, DHDropshipHoverEffectComponent, TransformComponent>();
        while (shadows.MoveNext(out var uid, out var shadow, out var effect, out var xform))
        {
            if (effect.Hidden || effect.Strength <= 0f || xform.MapID != args.MapId || shadow.HullBounds.IsEmpty())
                continue;

            var matrix = _transform.GetWorldMatrix(uid);
            var bounds = shadow.HullBounds;
            if (!args.WorldAABB.Enlarged(bounds.Size.Length()).Contains(matrix.Translation))
                continue;

            // Силуэт пересобирается, только если поменялся сам грид (сила и скрытие меняются часто).
            if (!_silhouettes.TryGetValue(uid, out var cached) || cached.Bounds != bounds ||
                cached.Tiles != shadow.Tiles.Count || cached.Parts != shadow.Parts.Count)
            {
                Invalidate(uid);
                // Мировой проход: рендер-таргет в пикселях рисуется отдельной экранной ручкой.
                cached = (CreateSilhouette(args.RenderHandle.DrawingHandleScreen, shadow), bounds, shadow.Tiles.Count,
                    shadow.Parts.Count);
                _silhouettes[uid] = cached;
            }

            var target = cached.Texture;

            h.SetTransform(matrix);
            // Размываем собранную маску целиком, а не каждый тайл: корпус ровный, без тёмных швов.
            var alpha = effect.Strength;
            h.DrawTextureRect(target.Texture, bounds, Color.Black.WithAlpha(.28f * alpha));
            for (var i = 0; i < 8; i++)
            {
                var angle = i * MathF.Tau / 8;
                // Чем выше челнок, тем шире размытие тени.
                var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (.075f + (1f - alpha) * .5f);
                h.DrawTextureRect(target.Texture, bounds.Translated(offset), Color.Black.WithAlpha(.045f * alpha));
            }
        }

        _exhaust.SetParameter("clock", (float) _timing.CurTime.TotalSeconds);
        // Подъёмная тяга вертикальна: струя всегда стоит «вниз» в кадре, даже если камера повёрнута.
        var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var exhaustRotation = Matrix3Helpers.CreateRotation(-eyeRotation);
        var washes = _entities
            .EntityQueryEnumerator<DHDropshipHoverDownwashComponent, DHDropshipHoverEffectComponent, TransformComponent>();
        while (washes.MoveNext(out _, out var wash, out var effect, out var xform))
        {
            if (effect.Hidden || effect.Strength <= 0f || xform.MapID != args.MapId)
                continue;

            var position = _transform.GetWorldPosition(xform);
            if (!args.WorldAABB.Enlarged(4f).Contains(position))
                continue;

            // У каждого сопла своя фаза: сетевые частицы не нужны.
            _exhaust.SetParameter("seed", wash.Offset.X * 3.17f + wash.Offset.Y * 7.31f);
            _exhaust.SetParameter("jetOffset", Vector2.Zero);
            _exhaust.SetParameter("strength", effect.Strength);
            h.SetTransform(exhaustRotation * Matrix3x2.CreateTranslation(position));
            h.UseShader(_exhaust);
            h.DrawTextureRect(Texture.White, new Box2(-3f, -4f, 3f, 3f));
            h.UseShader(null);
        }

        h.SetTransform(Matrix3x2.Identity);
    }

    private IRenderTexture CreateSilhouette(DrawingHandleScreen screen, DHDropshipHoverShadowComponent shadow)
    {
        var bounds = shadow.HullBounds;
        var size = bounds.Size * PixelsPerMeter;
        var target = _clyde.CreateRenderTarget(
            new Vector2i((int) MathF.Ceiling(size.X), (int) MathF.Ceiling(size.Y)),
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
            name: "dh-dropship-hover-silhouette");
        var previous = screen.GetTransform();
        screen.RenderInRenderTarget(target, () =>
        {
            screen.SetTransform(Matrix3x2.Identity);
            foreach (var tile in shadow.Tiles)
            {
                if (!_resources.TryGetResource<TextureResource>(new ResPath(tile.Texture), out var resource))
                    continue;

                var position = ToPixel(tile.Position, bounds);
                var source = UIBox2.FromDimensions(new Vector2(tile.Variant * PixelsPerMeter, 0),
                    new Vector2(PixelsPerMeter));
                screen.DrawTextureRectRegion(resource.Texture,
                    UIBox2.FromDimensions(position - new Vector2(16), new Vector2(32)), source);
            }

            foreach (var part in shadow.Parts)
            {
                if (!_prototypes.TryIndex<EntityPrototype>(part.Prototype, out var prototype) ||
                    !prototype.TryComp<SpriteComponent>(out var sprite, _factory))
                {
                    continue;
                }

                var texture = _sprites.GetPrototypeIcon(prototype).GetFrame(RsiDirection.South, 0);
                var position = ToPixel(part.Position + part.Rotation.RotateVec(sprite.Offset), bounds);
                var rotation = sprite.NoRotation ? 0f : -(float) (part.Rotation + sprite.Rotation).Theta;
                screen.SetTransform(Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(position));
                var half = (Vector2) texture.Size * sprite.Scale * .5f;
                screen.DrawTextureRect(texture, new UIBox2(-half, half));
            }
        }, Color.Transparent);
        screen.SetTransform(previous);
        return target;
    }

    private static Vector2 ToPixel(Vector2 position, Box2 bounds)
    {
        return new Vector2((position.X - bounds.Left) * PixelsPerMeter, (bounds.Top - position.Y) * PixelsPerMeter);
    }

    protected override void DisposeBehavior()
    {
        foreach (var cached in _silhouettes.Values)
        {
            cached.Texture.Dispose();
        }

        _silhouettes.Clear();
        _exhaust.Dispose();
        base.DisposeBehavior();
    }
}
