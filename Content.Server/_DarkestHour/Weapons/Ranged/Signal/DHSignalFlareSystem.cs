using Content.Shared._DarkestHour.Weapons.Ranged.Signal;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Weapons.Ranged.Signal;

/// <summary>
/// Сигнальные ракеты (решение автора 2026-10-29): ракетница стреляет белой (осветительной), зелёной и красной
/// ракетой. Ракета вспыхивает над точкой прицела (или у стены, если упёрлась) и горит, видная издалека.
/// </summary>
public sealed partial class DHSignalFlareSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>Если ракету выпустили не из ракетницы (обычная сигнальная пушка) — вспыхнет через столько.</summary>
    private static readonly TimeSpan DefaultFlight = TimeSpan.FromSeconds(1);

    private const float MinRange = 3f;

    private readonly List<EntityUid> _bursting = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHSignalPistolComponent, AmmoShotEvent>(OnShot);
        SubscribeLocalEvent<DHSignalFlareComponent, MapInitEvent>(OnFlareMapInit);
        SubscribeLocalEvent<DHSignalFlareComponent, ProjectileHitEvent>(OnFlareHit);
    }

    private void OnFlareMapInit(Entity<DHSignalFlareComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.BurstAt ??= _timing.CurTime + DefaultFlight;
    }

    private void OnShot(Entity<DHSignalPistolComponent> ent, ref AmmoShotEvent args)
    {
        if (!TryComp<GunComponent>(ent, out var gun) || gun.ShootCoordinates is not { } target)
            return;

        var from = _transform.GetMapCoordinates(ent);
        var to = _transform.ToMapCoordinates(target);
        var speed = MathF.Max(1f, gun.ProjectileSpeedModified);

        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp<DHSignalFlareComponent>(projectile, out var flare))
                continue;

            var distance = from.MapId == to.MapId ? (to.Position - from.Position).Length() : flare.MaxRange;
            distance = Math.Clamp(distance, MinRange, flare.MaxRange);
            flare.BurstAt = _timing.CurTime + TimeSpan.FromSeconds(distance / speed);
        }
    }

    private void OnFlareHit(Entity<DHSignalFlareComponent> ent, ref ProjectileHitEvent args)
    {
        Burst(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _bursting.Clear();
        var query = EntityQueryEnumerator<DHSignalFlareComponent>();
        while (query.MoveNext(out var uid, out var flare))
        {
            if (flare.BurstAt is { } at && now >= at)
                _bursting.Add(uid);
        }

        foreach (var uid in _bursting)
        {
            if (TryComp<DHSignalFlareComponent>(uid, out var flare))
                Burst((uid, flare));
        }
    }

    private void Burst(Entity<DHSignalFlareComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

        var coordinates = _transform.GetMapCoordinates(ent);
        RemCompDeferred<DHSignalFlareComponent>(ent);
        QueueDel(ent);
        if (coordinates.MapId == MapId.Nullspace)
            return;

        Spawn(ent.Comp.Burst, coordinates);
        _popup.PopupCoordinates(Loc.GetString("dh-signal-flare-burst", ("color", Loc.GetString(ent.Comp.ColorName))),
            _transform.ToCoordinates(coordinates), PopupType.Medium);
    }
}
