using Content.Shared._DarkestHour.Fortifications;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using static Robust.Client.Graphics.RSI;

namespace Content.Client._DarkestHour.Fortifications;

/// <summary>
/// Стыки сцепленных складных ворот (порт RMCFoldingBarricadeLinkingVisualizerSystem RMC-14, MIT). Данные о соседях
/// хранятся по сторонам света, а состояния спрайта — по сторонам экрана, поэтому учитывается поворот камеры.
/// </summary>
public sealed partial class DHFoldingGateVisualizerSystem : EntitySystem
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly (DHFoldingGateLinkVisuals Visual, DHFoldingGateLinkLayers Layer, Direction Direction)[] Layers =
    {
        (DHFoldingGateLinkVisuals.North, DHFoldingGateLinkLayers.North, Direction.North),
        (DHFoldingGateLinkVisuals.South, DHFoldingGateLinkLayers.South, Direction.South),
        (DHFoldingGateLinkVisuals.East, DHFoldingGateLinkLayers.East, Direction.East),
        (DHFoldingGateLinkVisuals.West, DHFoldingGateLinkLayers.West, Direction.West),
    };

    private Direction _lastEye = Direction.Invalid;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHFoldingGateComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    public override void FrameUpdate(float frameTime)
    {
        var eye = _eye.CurrentEye.Rotation.GetCardinalDir();
        if (eye == _lastEye)
            return;

        _lastEye = eye;
        var query = EntityQueryEnumerator<DHFoldingGateComponent, AppearanceComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var gate, out var appearance, out var sprite))
        {
            Update((uid, sprite), gate, appearance);
        }
    }

    private void OnAppearanceChange(Entity<DHFoldingGateComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite != null)
            Update((ent.Owner, args.Sprite), ent.Comp, args.Component);
    }

    private void Update(Entity<SpriteComponent> ent, DHFoldingGateComponent gate, AppearanceComponent? appearance)
    {
        var eyeRotation = _eye.CurrentEye.Rotation;
        var sprite = ent.AsNullable();
        foreach (var (visual, layer, direction) in Layers)
        {
            if (!_sprite.LayerMapTryGet(sprite, layer, out var index, false))
                continue;

            if (!_appearance.TryGetData<DHFoldingGateLinkState>(ent.Owner, visual, out var state, appearance) ||
                state == DHFoldingGateLinkState.None)
            {
                _sprite.LayerSetVisible(sprite, index, false);
                continue;
            }

            var screen = (direction.ToAngle() + eyeRotation).GetCardinalDir();
            if (!gate.ConnectionStates.TryGetValue(screen, out var states) || !states.TryGetValue(state, out var id))
            {
                _sprite.LayerSetVisible(sprite, index, false);
                continue;
            }

            _sprite.LayerSetRsiState(sprite, index, new StateId(id));
            _sprite.LayerSetVisible(sprite, index, true);
        }
    }
}
