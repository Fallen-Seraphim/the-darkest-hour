using Content.Server.Audio;
using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.Audio;
using Content.Shared.Vehicle.Components;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Physics.Components;

namespace Content.Server._DarkestHour.Vehicles;

/// <summary>
/// Звук двигателя: заглушён без водителя; с водителем — холостой ход, на ходу громче. Когда водитель садится —
/// звук запуска двигателя.
/// </summary>
public sealed partial class DHVehicleServerSystem
{
    [Dependency] private AmbientSoundSystem _ambient = default!;
    [Dependency] private AudioSystem _audio = default!;

    private static readonly SoundSpecifier EngineStartSound = new SoundPathSpecifier("/Audio/Effects/Vehicle/vehiclestartup.ogg");
    private static readonly TimeSpan EngineUpdateInterval = TimeSpan.FromSeconds(0.25);

    private const float EngineIdleVolume = -2f;
    private const float EngineMoveVolume = 5f;

    private TimeSpan _nextEngineUpdate;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextEngineUpdate)
            return;

        _nextEngineUpdate = now + EngineUpdateInterval;

        var query = EntityQueryEnumerator<DHVehicleComponent, VehicleComponent, AmbientSoundComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var dh, out var vehicle, out var ambient, out var physics))
        {
            var running = vehicle.Operator != null;
            if (running && !dh.EngineRunning)
                _audio.PlayPvs(EngineStartSound, uid, AudioParams.Default.WithVolume(2f).WithMaxDistance(15f));

            dh.EngineRunning = running;
            if (ambient.Enabled != running)
                _ambient.SetAmbience(uid, running, ambient);

            if (!running)
                continue;

            var volume = physics.LinearVelocity.LengthSquared() > 0.25f ? EngineMoveVolume : EngineIdleVolume;
            if (!MathHelper.CloseTo(ambient.Volume, volume))
                _ambient.SetVolume(uid, volume, ambient);
        }
    }
}
