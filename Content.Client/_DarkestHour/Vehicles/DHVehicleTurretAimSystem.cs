using Content.Shared._DarkestHour.Vehicles;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.Vehicles;

/// <summary>
/// Башня следует за курсором стрелка постоянно, а не только в момент выстрела: клиент считает четверть
/// и шлёт серверу при её смене (не чаще раза в 0,1 с).
/// </summary>
public sealed partial class DHVehicleTurretAimSystem : EntitySystem
{
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DHVehicleSystem _vehicle = default!;

    private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(0.1);

    private TimeSpan _nextSend;
    private int _lastSent = -1;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } user || !_vehicle.TryGetTurretGunner(user, out var vehicle))
        {
            _lastSent = -1;
            return;
        }

        var now = _timing.RealTime;
        if (now < _nextSend)
            return;

        var mouse = _eyeManager.PixelToMap(_input.MouseScreenPosition);
        if (!_vehicle.TryGetTurretQuarter(vehicle, mouse, out var quarter))
            return;

        if (quarter == _lastSent && quarter == vehicle.Comp.TurretQuarter)
            return;

        _nextSend = now + SendInterval;
        _lastSent = quarter;
        RaiseNetworkEvent(new DHVehicleTurretAimEvent(GetNetEntity(vehicle.Owner), quarter));
    }
}
