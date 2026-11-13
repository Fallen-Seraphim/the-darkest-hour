using Content.Shared._DarkestHour.HeavyGuns;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._DarkestHour.HeavyGuns;

/// <summary>
/// Анимации орудия (<see cref="DHHeavyGunAnimationComponent"/>): снаряд заложен — анимация зарядки, затем «заряжено»;
/// выстрел (счётчик <see cref="DHHeavyGunVisuals.Shots"/> вырос) — анимация выстрела, затем «пусто» или «заряжено».
/// </summary>
public sealed partial class DHHeavyGunAnimationSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const string AnimationKey = "dh-heavy-gun";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHHeavyGunAnimationComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<DHHeavyGunAnimationComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        _appearance.TryGetData<bool>(ent, DHHeavyGunVisuals.Loaded, out var loaded, args.Component);
        _appearance.TryGetData<int>(ent, DHHeavyGunVisuals.Shots, out var shots, args.Component);

        var comp = ent.Comp;
        var first = comp.ShownLoaded == null;
        var fired = !first && shots > comp.ShownShots;
        var gotLoaded = !first && loaded && comp.ShownLoaded == false;
        comp.ShownLoaded = loaded;
        comp.ShownShots = shots;

        var rest = loaded ? comp.Loaded : comp.Unloaded;
        if (fired)
        {
            Play(ent, comp.Firing, comp.FiringTime, rest);
            return;
        }

        if (gotLoaded)
        {
            Play(ent, comp.Loading, comp.LoadingTime, rest);
            return;
        }

        if (_animation.HasRunningAnimation(ent, AnimationKey))
            return;

        if (_sprite.LayerMapTryGet((ent, sprite), comp.Layer, out var layer, false))
            _sprite.LayerSetRsiState((ent, sprite), layer, rest);
    }

    private void Play(Entity<DHHeavyGunAnimationComponent> ent, string state, TimeSpan length, string rest)
    {
        _animation.Stop(ent.Owner, AnimationKey);
        var seconds = (float) length.TotalSeconds;
        var animation = new Animation
        {
            Length = length + TimeSpan.FromSeconds(0.05),
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = ent.Comp.Layer,
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame(new RSI.StateId(state), 0f),
                        new AnimationTrackSpriteFlick.KeyFrame(new RSI.StateId(rest), seconds),
                    },
                },
            },
        };

        _animation.Play(ent.Owner, animation, AnimationKey);
    }
}
