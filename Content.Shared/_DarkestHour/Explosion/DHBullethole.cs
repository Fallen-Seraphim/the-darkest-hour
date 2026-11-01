using Content.Shared.Explosion.Components;
using Content.Shared.Projectiles;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Explosion;

// DarkestHour (решение автора 2026-11-01): следы от пуль на стенах, как в RMC-14 (BulletholeSystem, MIT; спрайты
// CM-SS13, CC-BY-SA-3.0). У нас пули стену не ранят (DHFortified), поэтому считаем попадания, а не урон.

/// <summary>Стена, на которой остаются следы пуль.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHBulletholeComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Count;

    /// <summary>Узор следов (1–10), выбирается при первом попадании.</summary>
    [DataField, AutoNetworkedField]
    public int Pattern;
}

[Serializable, NetSerializable]
public enum DHBulletholeVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum DHBulletholeLayers : byte
{
    Bullethole,
}

public sealed partial class DHBulletholeSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const int MaxPattern = 10;
    private const int MaxCount = 24;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    /// <summary>Пуля (колющий урон, не взрывчатка) попала в стену — ещё одна дырка.</summary>
    private void OnProjectileHit(Entity<ProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (_net.IsClient || !TryComp<DHBulletholeComponent>(args.Target, out var hole) ||
            HasComp<ExplosiveComponent>(ent) ||
            !ent.Comp.Damage.DamageDict.TryGetValue("Piercing", out var piercing) || piercing <= 0)
        {
            return;
        }

        if (hole.Pattern < 1 || hole.Pattern > MaxPattern)
            hole.Pattern = _random.Next(1, MaxPattern + 1);

        hole.Count = Math.Min(hole.Count + 1, MaxCount);
        Dirty(args.Target, hole);
        _appearance.SetData(args.Target, DHBulletholeVisuals.State, $"bhole_{hole.Pattern}_{hole.Count}");
    }
}
