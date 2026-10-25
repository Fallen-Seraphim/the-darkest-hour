using System.Numerics;
using Content.Server.Shuttles.Events;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Mobs.Components;
using Content.Shared.Parallax;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Server._DarkestHour.Shuttles.Rift;

/// <summary>
/// Разрывное пространство: на время прыжка корабль уходит не в ванильный гиперкосмос, а на свою карту —
/// фиолетово-чёрная туманность, полосы мимо борта, разряды молний вокруг корпуса, тень по краям экрана.
/// </summary>
public sealed partial class DHRiftSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    /// <summary>Расстояние между кораблями в разрыве, чтобы не видеть друг друга.</summary>
    private const float RiftSpacing = 600f;

    private static readonly Color RiftAmbient = Color.FromHex("#1a0b29");
    private static readonly SoundSpecifier ThunderSound = new SoundPathSpecifier("/Audio/Effects/Lightning/lightningbolt.ogg",
        AudioParams.Default.WithVolume(-4f).WithVariation(0.15f));

    private const string RiftLightning = "DHRiftLightning";
    private const string RiftLightningAnchor = "DHRiftLightningAnchor";

    private float _riftIndex;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateSpace();
    }

    private void InitializeSpace()
    {
        SubscribeLocalEvent<DHRiftTransitComponent, FTLCompletedEvent>(OnTransitCompleted);
    }

    private EntityUid EnsureRiftSpace()
    {
        var query = AllEntityQuery<DHRiftSpaceMapComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            return uid;
        }

        var map = _map.CreateMap(out _);
        _meta.SetEntityName(map, Loc.GetString("dh-rift-space-name"));
        EnsureComp<DHRiftSpaceMapComponent>(map);

        var parallax = EnsureComp<ParallaxComponent>(map);
        parallax.Parallax = "DHRiftSpace";
        Dirty(map, parallax);

        var light = EnsureComp<MapLightComponent>(map);
        light.AmbientLightColor = RiftAmbient;
        Dirty(map, light);

        return map;
    }

    /// <summary>
    /// Корабль только что ушёл в ванильный гиперкосмос: переносим его в разрыв. Там он стоит на месте (движение
    /// рисует прокрутка фона), так что молнии у корпуса не отстают; дальше ванильный FTL сам выводит его к цели.
    /// </summary>
    private void EnterRiftSpace(EntityUid shuttle)
    {
        if (!TryComp<MapGridComponent>(shuttle, out var grid))
            return;

        var space = EnsureRiftSpace();
        var aabb = grid.LocalAABB;
        var coords = new Robust.Shared.Map.EntityCoordinates(space, new Vector2(_riftIndex, 0f) - aabb.Center);
        _riftIndex += RiftSpacing + MathF.Max(aabb.Width, aabb.Height);

        _transform.SetCoordinates(shuttle, coords);
        _transform.SetLocalRotation(shuttle, Angle.Zero);
        if (TryComp<PhysicsComponent>(shuttle, out var body))
        {
            _physics.SetLinearVelocity(shuttle, Vector2.Zero, body: body);
            _physics.SetAngularVelocity(shuttle, 0f, body: body);
        }

        var transit = EnsureComp<DHRiftTransitComponent>(shuttle);
        transit.Since = _timing.CurTime;
        transit.NextThunder = _timing.CurTime + TimeSpan.FromSeconds(1.5);
        Dirty(shuttle, transit);
    }

    private void OnTransitCompleted(Entity<DHRiftTransitComponent> ent, ref FTLCompletedEvent args)
    {
        RemCompDeferred<DHRiftTransitComponent>(ent);
    }

    private void UpdateSpace()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHRiftTransitComponent, MapGridComponent>();
        while (query.MoveNext(out var uid, out var transit, out var grid))
        {
            // Корабль покинул разрыв не через FTL (удалён, перенесён админом) — снимаем эффект.
            if (!HasComp<DHRiftSpaceMapComponent>(Transform(uid).MapUid))
            {
                RemCompDeferred<DHRiftTransitComponent>(uid);
                continue;
            }

            if (now < transit.NextThunder)
                continue;

            transit.NextThunder = now + TimeSpan.FromSeconds(_random.NextFloat(2.5f, 6f));
            Thunder(uid, grid);
        }
    }

    /// <summary>Разряд у корпуса: 1–3 молнии между точками снаружи борта, гром и вспышка у тех, кто на борту.</summary>
    private void Thunder(EntityUid shuttle, MapGridComponent grid)
    {
        var aabb = grid.LocalAABB;
        var radius = MathF.Max(aabb.Width, aabb.Height) / 2f + 2f;
        var bolts = _random.Next(1, 4);

        for (var i = 0; i < bolts; i++)
        {
            var angle = _random.NextAngle();
            var start = aabb.Center + angle.ToVec() * radius;
            var end = aabb.Center + (angle + Angle.FromDegrees(_random.NextFloat(-50f, 50f))).ToVec() *
                (radius + _random.NextFloat(4f, 12f));

            var a = Spawn(RiftLightningAnchor, new Robust.Shared.Map.EntityCoordinates(shuttle, start));
            var b = Spawn(RiftLightningAnchor, new Robust.Shared.Map.EntityCoordinates(shuttle, end));
            _lightning.ShootLightning(a, b, RiftLightning, triggerLightningEvents: false);
        }

        var strength = 0.4f + 0.2f * bolts;
        var filter = Filter.Empty();
        var mobs = EntityQueryEnumerator<MobStateComponent, ActorComponent, TransformComponent>();
        while (mobs.MoveNext(out _, out _, out var actor, out var xform))
        {
            if (xform.GridUid == shuttle)
                filter.AddPlayer(actor.PlayerSession);
        }

        if (filter.Count == 0)
            return;

        RaiseNetworkEvent(new DHRiftThunderEvent(strength), filter);
        _audio.PlayGlobal(ThunderSound, filter, false);
    }
}

/// <summary>Карта разрывного пространства (одна на раунд, создаётся при первом прыжке).</summary>
[RegisterComponent]
public sealed partial class DHRiftSpaceMapComponent : Component
{
}
