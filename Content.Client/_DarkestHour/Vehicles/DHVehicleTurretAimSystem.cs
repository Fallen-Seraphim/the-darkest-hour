using Content.Shared._DarkestHour.Vehicles;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.Vehicles;

/// <summary>
/// Башня следует за курсором стрелка постоянно, а не только в момент выстрела: клиент считает угол
/// и шлёт серверу при его смене (не чаще 20 раз в секунду).
/// </summary>
public sealed partial class DHVehicleTurretAimSystem : EntitySystem
{
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DHVehicleSystem _vehicle = default!;

    private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(0.05);

    private TimeSpan _nextSend;
    private Angle _lastSent = new(double.NaN);

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } user || !_vehicle.TryGetTurretGunner(user, out var vehicle))
        {
            _lastSent = new Angle(double.NaN);
            return;
        }

        var now = _timing.RealTime;
        if (now < _nextSend)
            return;

        var mouse = _eyeManager.PixelToMap(_input.MouseScreenPosition);
        if (!_vehicle.TryGetTurretAngle(vehicle, mouse, out var angle))
            return;

        if (Math.Abs((angle - _lastSent).Reduced().Theta) < 0.03 &&
            Math.Abs((angle - vehicle.Comp.TurretAngle).Reduced().Theta) < 0.03)
        {
            return;
        }

        _nextSend = now + SendInterval;
        _lastSent = angle;
        // Свою башню видим сразу, не дожидаясь сервера.
        vehicle.Comp.TurretAngle = angle;
        RaiseNetworkEvent(new DHVehicleTurretAimEvent(GetNetEntity(vehicle.Owner), angle));
    }
}
