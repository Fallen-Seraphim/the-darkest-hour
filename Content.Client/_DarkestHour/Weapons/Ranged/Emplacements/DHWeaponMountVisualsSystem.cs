using Content.Shared._DarkestHour.Weapons.Ranged.Emplacements;
using Robust.Client.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._DarkestHour.Weapons.Ranged.Emplacements;

/// <summary>
/// Спрайт станкового пулемёта: сложенный/развёрнутый, лента, раскалённый ствол (прозрачность по теплу).
/// </summary>
public sealed partial class DHWeaponMountVisualsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private DHOverheatSystem _overheat = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHWeaponMountComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<DHWeaponMountComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnAppearanceChange(Entity<DHWeaponMountComponent> ent, ref AppearanceChangeEvent args)
    {
        UpdateLayers(ent);
    }

    private void OnHandleState(Entity<DHWeaponMountComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateLayers(ent);
    }

    private void UpdateLayers(Entity<DHWeaponMountComponent> ent)
    {
        if (!TryComp(ent, out SpriteComponent? spriteComp))
            return;

        Entity<SpriteComponent?> sprite = (ent.Owner, spriteComp);
        var deployed = ent.Comp.Deployed;
        _appearance.TryGetData<bool>(ent, DHWeaponMountVisuals.Ammo, out var ammo);

        _sprite.LayerSetVisible(sprite, DHWeaponMountLayers.Folded, !deployed);
        _sprite.LayerSetVisible(sprite, DHWeaponMountLayers.Deployed, deployed);
        _sprite.LayerSetVisible(sprite, DHWeaponMountLayers.Ammo, deployed && ammo);
        if (!deployed)
            _sprite.LayerSetVisible(sprite, DHWeaponMountLayers.Heat, false);

        _sprite.SetDrawDepth(sprite, (int) (deployed ? DrawDepth.Mobs : DrawDepth.Items));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // Только развёрнутые пулемёты в поле зрения клиента; их немного.
        var query = EntityQueryEnumerator<DHWeaponMountComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var mount, out var sprite))
        {
            if (!mount.Deployed ||
                mount.MountedWeapon is not { } weapon ||
                !TryComp(weapon, out DHOverheatComponent? overheat))
            {
                continue;
            }

            var alpha = Math.Clamp(_overheat.GetHeat(overheat) / overheat.MaxHeat, 0f, 1f);
            Entity<SpriteComponent?> ent = (uid, sprite);
            _sprite.LayerSetVisible(ent, DHWeaponMountLayers.Heat, alpha > 0.02f);
            _sprite.LayerSetColor(ent, DHWeaponMountLayers.Heat, Color.White.WithAlpha(alpha));
        }
    }
}
