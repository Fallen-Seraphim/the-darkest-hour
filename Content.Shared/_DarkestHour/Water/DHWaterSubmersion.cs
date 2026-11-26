// Перенесено из RMC-14 (через RussianCM, github.com/flex5hybrid/RussianCM): RMCWaterComponent и
// RMCWaterSystem.Submersion — погружение в воду по cmss13#12918 (решение автора 2026-11-26: эффекты входа в воду как в
// RuCM). Ксено-части убраны, поиск воды — по закреплённым сущностям клетки (наши DHWaterShallow/DHWaterDeep).
using Content.Shared.Buckle.Components;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Water;

/// <summary>Вода, в которую погружаются: глубина — на сколько пикселей уходит спрайт.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHWaterDepthComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Depth = 8;
}

public sealed partial class DHWaterSubmersionSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private INetManager _net = default!;

    private static readonly EntProtoId WaterImpact = "RMCWaterImpact";
    private static readonly SoundSpecifier ImpactSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/Water/splash.ogg",
        AudioParams.Default.WithVolume(-6));

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ThrownItemComponent, LandEvent>(OnLanding);
    }

    private void OnLanding(Entity<ThrownItemComponent> ent, ref LandEvent args)
    {
        Splash(ent, args.User);
    }

    /// <summary>Всплеск: звук и брызги, если упало в воду.</summary>
    public void Splash(EntityUid uid, EntityUid? user = null)
    {
        if (!TryGetWaterSurface(uid, out _, out var depth) || depth < 2)
            return;

        _audio.PlayPredicted(ImpactSound, uid, user);
        if (_net.IsServer)
            Spawn(WaterImpact, Transform(uid).Coordinates);
    }

    /// <summary>Вода под центром сущности (самая глубокая на клетке).</summary>
    public bool TryGetWaterSurface(EntityUid user, out EntityUid? water, out float depth)
    {
        water = null;
        depth = 0;
        var xform = Transform(user);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        var anchored = _map.GetAnchoredEntitiesEnumerator(grid, gridComp, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (!TryComp<DHWaterDepthComponent>(uid, out var component) || TerminatingOrDeleted(uid.Value))
                continue;

            if (water != null && component.Depth <= depth)
                continue;

            water = uid;
            depth = component.Depth;
        }

        return water != null;
    }

    /// <summary>Погружается ли: не в контейнере, не пристёгнут (лодка, кресло), не летит брошенным.</summary>
    public bool CanSubmerge(EntityUid user)
    {
        return !_container.IsEntityInContainer(user) &&
               !(TryComp<BuckleComponent>(user, out var buckle) && buckle.Buckled) &&
               !(TryComp<ThrownItemComponent>(user, out var thrown) && !thrown.Landed);
    }
}
