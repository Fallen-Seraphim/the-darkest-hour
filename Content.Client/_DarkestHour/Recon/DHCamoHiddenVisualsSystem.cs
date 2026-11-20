using Content.Shared._DarkestHour.Levels;
using Content.Shared._DarkestHour.Recon;
using Robust.Client.GameObjects;
using Robust.Client.Player;

namespace Content.Client._DarkestHour.Recon;

/// <summary>
/// Маскировочная сеть сверху (план осады п. 9, решение автора 2026-11-19): кто смотрит с уровня воздуха (пилот на
/// бреющем, челнок над полем боя), не видит стоящую под сетью технику и орудия — только саму сеть.
/// </summary>
public sealed class DHCamoHiddenVisualsSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private DHZLevelSystem _zLevels = default!;

    /// <summary>Что спрятали мы (чтобы вернуть только своё).</summary>
    private readonly HashSet<EntityUid> _hidden = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHCamoHiddenComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<DHCamoHiddenComponent> ent, ref ComponentShutdown args)
    {
        if (_hidden.Remove(ent.Owner) && TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetVisible((ent.Owner, sprite), true);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var fromAbove = _player.LocalEntity is { } local && _zLevels.IsAirLevel(Transform(local).MapID);
        if (!fromAbove && _hidden.Count == 0)
            return;

        var query = EntityQueryEnumerator<DHCamoHiddenComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
        {
            if (fromAbove)
            {
                if (!sprite.Visible)
                    continue;

                _sprite.SetVisible((uid, sprite), false);
                _hidden.Add(uid);
            }
            else if (_hidden.Remove(uid))
            {
                _sprite.SetVisible((uid, sprite), true);
            }
        }

        if (!fromAbove)
            _hidden.Clear();
    }
}
