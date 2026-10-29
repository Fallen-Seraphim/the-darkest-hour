using Content.Shared._DarkestHour.Hearing;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._DarkestHour.Hearing;

/// <summary>Звон в ушах у своего персонажа: высокий писк, громкий в начале и затухающий к концу.</summary>
public sealed partial class DHTinnitusSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly SoundSpecifier Ringing = new SoundPathSpecifier("/Audio/_DarkestHour/Effects/tinnitus.ogg",
        AudioParams.Default.WithLoop(true).WithVolume(-2f));

    /// <summary>Громкость звона в начале и в конце, дБ.</summary>
    private const float StartVolume = -2f;
    private const float EndVolume = -24f;

    private EntityUid? _stream;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        if (_player.LocalEntity is not { } local ||
            !TryComp<DHTinnitusComponent>(local, out var tinnitus) || now >= tinnitus.EndTime)
        {
            StopRinging();
            return;
        }

        if (_stream == null || !Exists(_stream))
            _stream = _audio.PlayGlobal(Ringing, Filter.Local(), false)?.Entity;

        var total = (tinnitus.EndTime - tinnitus.StartTime).TotalSeconds;
        var progress = total > 0 ? Math.Clamp((now - tinnitus.StartTime).TotalSeconds / total, 0, 1) : 1;
        _audio.SetVolume(_stream, (float) (StartVolume + (EndVolume - StartVolume) * progress));
    }

    private void StopRinging()
    {
        if (_stream == null)
            return;

        _audio.Stop(_stream);
        _stream = null;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        StopRinging();
    }
}
