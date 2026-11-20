using Content.Shared._DarkestHour.Aviation;
using Robust.Client.GameObjects;
using DrawDepthEnum = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._DarkestHour.Aviation;

/// <summary>
/// На бреющем самолёт летит над полем боя — рисуется поверх стен, дверей и людей; на палубе — на полу ВПП.
/// Фонарь открыт, пока кабина пуста, и закрыт матовым стеклом с пилотом внутри; пилота внутри не видно. Подвесы на узлах
/// видны из-под крыла (план осады п. 16).
/// </summary>
public sealed partial class DHAircraftVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHAircraftComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<DHAircraftPilotComponent, ComponentStartup>(OnPilotStartup);
        SubscribeLocalEvent<DHAircraftPilotComponent, ComponentShutdown>(OnPilotShutdown);
    }

    private void OnState(Entity<DHAircraftComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var flying = ent.Comp.State == DHAircraftState.Pass;
        var hull = flying ? (int) DrawDepthEnum.Overdoors : (int) DrawDepthEnum.FloorObjects;
        var seat = flying ? (int) DrawDepthEnum.Overdoors + 1 : (int) DrawDepthEnum.Objects;
        var canopy = flying ? (int) DrawDepthEnum.Overdoors + 2 : (int) DrawDepthEnum.OverMobs;

        if (TryComp<SpriteComponent>(ent, out var sprite))
        {
            _sprite.SetDrawDepth((ent.Owner, sprite), hull);
            UpdateHardpoints((ent.Owner, sprite), ent.Comp);
        }

        if (ent.Comp.Seat is { } seatUid && TryComp<SpriteComponent>(seatUid, out var seatSprite))
            _sprite.SetDrawDepth((seatUid, seatSprite), seat);
        if (ent.Comp.Canopy is { } canopyUid && TryComp<SpriteComponent>(canopyUid, out var canopySprite))
        {
            _sprite.SetDrawDepth((canopyUid, canopySprite), canopy);
            if (_sprite.LayerMapTryGet((canopyUid, canopySprite), "canopy", out var layer, false))
                _sprite.LayerSetRsiState((canopyUid, canopySprite), layer, ent.Comp.Occupied ? "canopy_closed" : "canopy_open");
        }
    }

    /// <summary>Слой «hpN» — то, что висит на узле N (или ничего).</summary>
    private void UpdateHardpoints(Entity<SpriteComponent> sprite, DHAircraftComponent comp)
    {
        for (var i = 0; i < 4; i++)
        {
            if (!_sprite.LayerMapTryGet(sprite.AsNullable(), $"hp{i}", out var layer, false))
                continue;

            var munition = i < comp.Hardpoints.Count ? comp.Hardpoints[i] : DHAircraftMunition.None;
            if (StoreState(munition) is not { } state)
            {
                _sprite.LayerSetVisible(sprite.AsNullable(), layer, false);
                continue;
            }

            _sprite.LayerSetRsiState(sprite.AsNullable(), layer, $"hp{i}_{state}");
            _sprite.LayerSetVisible(sprite.AsNullable(), layer, true);
        }
    }

    private static string? StoreState(DHAircraftMunition munition)
    {
        return munition switch
        {
            DHAircraftMunition.BombHE => "bomb_he",
            DHAircraftMunition.BombCluster => "bomb_cluster",
            DHAircraftMunition.BombNapalm => "bomb_napalm",
            DHAircraftMunition.BombMagnesium => "bomb_magnesium",
            DHAircraftMunition.GuidedBomb => "bomb_guided",
            DHAircraftMunition.MissileAT => "missile_at",
            DHAircraftMunition.MissileHE => "missile_he",
            DHAircraftMunition.CannonPod => "cannon_pod",
            _ => null,
        };
    }

    // Пилот внутри кабины снаружи не виден (как в криокапсуле).
    private void OnPilotStartup(Entity<DHAircraftPilotComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetVisible((ent.Owner, sprite), false);
    }

    private void OnPilotShutdown(Entity<DHAircraftPilotComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetVisible((ent.Owner, sprite), true);
    }
}
