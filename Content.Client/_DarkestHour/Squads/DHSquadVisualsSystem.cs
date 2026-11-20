using Content.Client.Overlays;
using Content.Shared._DarkestHour.Squads;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._DarkestHour.Squads;

/// <summary>
/// Цвет отряда виден (решение автора 2026-11-19): значок отряда в худе (у кого HUD с должностями; у командира —
/// с латунной рамкой) и нашивка цвета отряда на левом плече.
/// </summary>
public sealed partial class DHSquadVisualsSystem : EntitySystem
{
    [Dependency] private ShowJobIconsSystem _jobIcons = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly SpriteSpecifier.Rsi Patch =
        new(new ResPath("/Textures/_DarkestHour/Mobs/squad_patch.rsi"), "patch");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHSquadMemberComponent, GetStatusIconsEvent>(OnGetStatusIcons);
        SubscribeLocalEvent<DHSquadMemberComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DHSquadMemberComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<DHSquadMemberComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnGetStatusIcons(Entity<DHSquadMemberComponent> ent, ref GetStatusIconsEvent args)
    {
        if (!_jobIcons.IsActive || !_proto.TryIndex(ent.Comp.Squad, out var squad))
            return;

        var iconId = ent.Comp.Leader ? squad.LeaderIcon ?? squad.Icon : squad.Icon;
        if (iconId is { } id && _proto.TryIndex(id, out var icon))
            args.StatusIcons.Add(icon);
    }

    private void OnStartup(Entity<DHSquadMemberComponent> ent, ref ComponentStartup args)
    {
        UpdatePatch(ent);
    }

    private void OnState(Entity<DHSquadMemberComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdatePatch(ent);
    }

    private void OnShutdown(Entity<DHSquadMemberComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite) &&
            _sprite.LayerMapTryGet((ent.Owner, sprite), DHSquadPatchLayer.Patch, out var index, false))
        {
            _sprite.LayerSetVisible((ent.Owner, sprite), index, false);
        }
    }

    private void UpdatePatch(Entity<DHSquadMemberComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) || !_proto.TryIndex(ent.Comp.Squad, out var squad))
            return;

        var target = (ent.Owner, sprite);
        var index = _sprite.LayerMapReserve(target, DHSquadPatchLayer.Patch);
        _sprite.LayerSetSprite(target, index, Patch);
        _sprite.LayerSetColor(target, index, squad.Color);
        _sprite.LayerSetVisible(target, index, true);
    }

    private enum DHSquadPatchLayer : byte
    {
        Patch,
    }
}
