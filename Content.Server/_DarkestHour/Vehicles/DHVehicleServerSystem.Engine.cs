using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.Vehicle.Components;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server._DarkestHour.Vehicles;

/// <summary>
/// Звук двигателя, как в RMC-14: когда садится водитель — запуск, на ходу каждые 2 с — рёв двигателя
/// (у машины снаружи и в отсеке, чтобы экипаж и десант тоже слышали).
/// </summary>
public sealed partial class DHVehicleServerSystem
{
    [Dependency] private AudioSystem _audio = default!;

    private static readonly SoundSpecifier EngineStartSound = new SoundPathSpecifier("/Audio/Effects/Vehicle/vehiclestartup.ogg");
    private static readonly SoundSpecifier EngineRunningSound = new SoundPathSpecifier("/Audio/Effects/Vehicle/vehicleengineidle.ogg");
    private static readonly TimeSpan EngineRunningCooldown = TimeSpan.FromSeconds(2);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHVehicleComponent, VehicleComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var dh, out var vehicle, out var physics))
        {
            var running = vehicle.Operator != null;
            if (running && !dh.EngineRunning)
                PlayEngine((uid, dh), EngineStartSound);

            dh.EngineRunning = running;
            // По клеткам машину двигает DHGridDriveSystem — скорости у тела нет, смотрим на шаг.
            var moving = physics.LinearVelocity.LengthSquared() >= 0.04f ||
                         TryComp<DHGridDriveComponent>(uid, out var drive) && drive.Moving;
            if (!running || !moving || now < dh.NextEngineSound)
                continue;

            dh.NextEngineSound = now + EngineRunningCooldown;
            PlayEngine((uid, dh), EngineRunningSound);
        }
    }

    private void PlayEngine(Entity<DHVehicleComponent> ent, SoundSpecifier sound)
    {
        _audio.PlayPvs(sound, ent, AudioParams.Default.WithMaxDistance(15f));

        if (ent.Comp.InteriorGrid is { } grid && Exists(grid))
        {
            _audio.PlayPvs(sound, new EntityCoordinates(grid, ent.Comp.InteriorEntry),
                AudioParams.Default.WithVolume(-4f).WithMaxDistance(12f));
        }
    }
}
