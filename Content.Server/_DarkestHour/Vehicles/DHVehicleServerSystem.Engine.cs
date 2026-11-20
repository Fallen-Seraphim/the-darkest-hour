using Content.Shared._DarkestHour.Vehicles;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Server._DarkestHour.Vehicles;

/// <summary>
/// Двигатель (план осады п. 5, решение автора 2026-11-19): механик-водитель заводит и глушит его кнопкой «Двигатель».
/// Заглушённая машина не едет и не даёт сигнатуры на РЛС (п. 9) — так устраивают засады. Звук, как в RMC-14: запуск,
/// на ходу каждые 2 с — рёв (у машины снаружи и в отсеке).
/// </summary>
public sealed partial class DHVehicleServerSystem
{
    [Dependency] private AudioSystem _audio = default!;

    private static readonly SoundSpecifier EngineStartSound = new SoundPathSpecifier("/Audio/Effects/Vehicle/vehiclestartup.ogg");
    private static readonly SoundSpecifier EngineRunningSound = new SoundPathSpecifier("/Audio/Effects/Vehicle/vehicleengineidle.ogg");
    private static readonly TimeSpan EngineRunningCooldown = TimeSpan.FromSeconds(2);

    private void InitializeEngine()
    {
        SubscribeLocalEvent<DHVehicleComponent, DHVehicleEngineActionEvent>(OnEngineAction);
        SubscribeLocalEvent<DHVehicleComponent, DHVehicleLockActionEvent>(OnLockAction);
    }

    private void OnEngineAction(Entity<DHVehicleComponent> ent, ref DHVehicleEngineActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        SetEngine(ent, !ent.Comp.EngineRunning);
        _popup.PopupEntity(Loc.GetString(ent.Comp.EngineRunning ? "dh-vehicle-engine-on" : "dh-vehicle-engine-off"),
            args.Performer, args.Performer);
    }

    public void SetEngine(Entity<DHVehicleComponent> ent, bool running)
    {
        if (ent.Comp.EngineRunning == running)
            return;

        ent.Comp.EngineRunning = running;
        Dirty(ent);
        if (running)
            PlayEngine(ent, EngineStartSound);
    }

    private void OnLockAction(Entity<DHVehicleComponent> ent, ref DHVehicleLockActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Locked = !ent.Comp.Locked;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Locked ? "dh-vehicle-locked-now" : "dh-vehicle-unlocked-now"),
            args.Performer, args.Performer);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHVehicleComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var dh, out var physics))
        {
            UpdateNeutralFaction(uid, dh);
            // По клеткам машину двигает DHGridDriveSystem — скорости у тела нет, смотрим на шаг.
            var moving = physics.LinearVelocity.LengthSquared() >= 0.04f ||
                         TryComp<DHGridDriveComponent>(uid, out var drive) && drive.IsMoving;
            if (!dh.EngineRunning || !moving || now < dh.NextEngineSound)
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
