using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.Projectiles;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Vehicles;

/// <summary>Звук попадания тяжёлого снаряда по технике — снаружи и в отсеке (<see cref="DHVehicleShellHitComponent"/>).</summary>
public sealed partial class DHVehicleShellHitSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>Не чаще раза в полсекунды на машину: очередь не сливается в кашу.</summary>
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(0.5);

    private readonly Dictionary<EntityUid, TimeSpan> _next = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHVehicleShellHitComponent, ProjectileHitEvent>(OnHit);
    }

    private void OnHit(Entity<DHVehicleShellHitComponent> ent, ref ProjectileHitEvent args)
    {
        if (!TryComp<DHVehicleComponent>(args.Target, out var vehicle))
            return;

        var now = _timing.CurTime;
        if (_next.TryGetValue(args.Target, out var next) && now < next)
            return;

        if (_next.Count > 64) // старые машины не копим
            _next.Clear();

        _next[args.Target] = now + Cooldown;
        _audio.PlayPvs(ent.Comp.Sound, args.Target, AudioParams.Default.WithMaxDistance(40f).WithVolume(4f));

        if (vehicle.InteriorGrid is { } grid && Exists(grid))
        {
            _audio.PlayPvs(ent.Comp.Sound, new EntityCoordinates(grid, vehicle.InteriorEntry),
                AudioParams.Default.WithMaxDistance(20f).WithVolume(4f));
        }
    }
}
