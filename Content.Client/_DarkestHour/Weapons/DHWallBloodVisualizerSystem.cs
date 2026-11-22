using Content.Shared._DarkestHour.Weapons.Impacts;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._DarkestHour.Weapons;

/// <summary>
/// Брызги крови поверх стены, двери, шлюза (план осады п. 15): до трёх слоёв из белых спрайтов CMU, перекрашенных в цвет
/// крови. Эффект брызг при попадании тоже красится в цвет крови.
/// </summary>
public sealed partial class DHWallBloodVisualizerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly ResPath RsiPath = new("/Textures/_DarkestHour/Effects/blood_splatter.rsi");
    private const int Patterns = 6;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHWallBloodComponent, AfterAutoHandleStateEvent>(OnWallState);
        SubscribeLocalEvent<DHWallBloodComponent, ComponentStartup>(OnWallStartup);
        SubscribeLocalEvent<DHBloodTintComponent, AfterAutoHandleStateEvent>(OnTintState);
        SubscribeLocalEvent<DHBloodTintComponent, ComponentStartup>(OnTintStartup);
    }

    private void OnWallStartup(Entity<DHWallBloodComponent> ent, ref ComponentStartup args)
    {
        Update(ent);
    }

    private void OnWallState(Entity<DHWallBloodComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Update(ent);
    }

    private void Update(Entity<DHWallBloodComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var spriteComp))
            return;

        var sprite = (ent.Owner, spriteComp);
        for (var i = 0; i < ent.Comp.Max; i++)
        {
            var key = $"dh_wall_blood_{i}";
            var visible = i < ent.Comp.Count;
            if (!visible)
            {
                if (_sprite.LayerMapTryGet(sprite, key, out var hidden, false))
                    _sprite.LayerSetVisible(sprite, hidden, false);
                continue;
            }

            var layer = _sprite.LayerMapReserve(sprite, key);
            var pattern = (ent.Comp.Pattern - 1 + i * 2) % Patterns + 1;
            _sprite.LayerSetSprite(sprite, layer, new SpriteSpecifier.Rsi(RsiPath, $"splatter{pattern}"));
            _sprite.LayerSetColor(sprite, layer, ent.Comp.Color.WithAlpha(0.85f));
            _sprite.LayerSetVisible(sprite, layer, true);
        }
    }

    private void OnTintStartup(Entity<DHBloodTintComponent> ent, ref ComponentStartup args)
    {
        Tint(ent);
    }

    private void OnTintState(Entity<DHBloodTintComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Tint(ent);
    }

    private void Tint(Entity<DHBloodTintComponent> ent)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetColor((ent.Owner, sprite), ent.Comp.Color);
    }
}
