using System.Numerics;
using Content.Shared._DarkestHour.AntiTank;
using Content.Shared.Buckle.Components;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.AntiTank;

/// <summary>
/// Наводчик ПТУР (план осады п. 13): пока сидишь за ПТУР, клиент шлёт серверу, куда смотрит курсор (не чаще 20 раз
/// в секунду и только если сдвинулся), — ракета в полёте доворачивает туда.
/// </summary>
public sealed partial class DHATGMAimSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(0.05);
    private const float MinShift = 0.25f;

    private TimeSpan _nextSend;
    private Vector2? _lastSent;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } user ||
            !TryComp<BuckleComponent>(user, out var buckle) ||
            buckle.BuckledTo is not { } launcher ||
            !HasComp<DHATGMComponent>(launcher))
        {
            _lastSent = null;
            return;
        }

        var now = _timing.RealTime;
        if (now < _nextSend)
            return;

        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mouse.MapId != _transform.GetMapId(launcher))
            return;

        if (_lastSent is { } last && Vector2.DistanceSquared(last, mouse.Position) < MinShift * MinShift)
            return;

        _nextSend = now + SendInterval;
        _lastSent = mouse.Position;
        RaiseNetworkEvent(new DHATGMAimEvent(GetNetEntity(launcher), mouse.Position));
    }
}
