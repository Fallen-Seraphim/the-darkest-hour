using Content.Shared._DarkestHour.Aviation;
using Robust.Client.GameObjects;
using DrawDepthEnum = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._DarkestHour.Aviation;

/// <summary>
/// На бреющем самолёт летит над полем боя — рисуется поверх стен, дверей и людей; на палубе — на полу ВПП.
/// </summary>
public sealed partial class DHAircraftVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHAircraftComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(Entity<DHAircraftComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var flying = ent.Comp.State == DHAircraftState.Pass;
        var hull = flying ? (int) DrawDepthEnum.Overdoors : (int) DrawDepthEnum.FloorObjects;
        var seat = flying ? (int) DrawDepthEnum.Overdoors + 1 : (int) DrawDepthEnum.Objects;
        var canopy = flying ? (int) DrawDepthEnum.Overdoors + 2 : (int) DrawDepthEnum.OverMobs;

        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetDrawDepth((ent.Owner, sprite), hull);
        if (ent.Comp.Seat is { } seatUid && TryComp<SpriteComponent>(seatUid, out var seatSprite))
            _sprite.SetDrawDepth((seatUid, seatSprite), seat);
        if (ent.Comp.Canopy is { } canopyUid && TryComp<SpriteComponent>(canopyUid, out var canopySprite))
            _sprite.SetDrawDepth((canopyUid, canopySprite), canopy);
    }
}
