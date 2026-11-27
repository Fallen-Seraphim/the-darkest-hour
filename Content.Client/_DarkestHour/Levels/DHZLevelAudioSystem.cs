using System.Numerics;
using Content.Shared._DarkestHour.Levels;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Client._DarkestHour.Levels;

/// <summary>
/// Звук при взгляде на другой уровень (баг 2026-11-27: посмотрел вверх и стреляешь — своих выстрелов не слышно).
/// Движок слушает там, где глаз, а глаз при взгляде — камера на карте другого уровня, поэтому всё со своей карты
/// глушилось. Пока боец смотрит на другой уровень, слушатель остаётся на его собственной карте (координаты XY у
/// уровней стопки общие, так что позиция та же). Остальное — как в движке (<c>AudioSystem.ProcessStream</c>).
/// </summary>
public sealed partial class DHZLevelAudioSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;

    /// <summary>Карта самого бойца, пока он смотрит на другой уровень; иначе — Nullspace (обычное поведение).</summary>
    private MapId _ownMap = MapId.Nullspace;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesBefore.Add(typeof(AudioSystem));
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _audio.ProcessStreamOverride += ProcessStream;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _audio.ProcessStreamOverride -= ProcessStream;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _ownMap = MapId.Nullspace;
        if (_player.LocalEntity is { } local &&
            TryComp<DHZViewerComponent>(local, out var viewer) && viewer.Camera != null)
        {
            _ownMap = Transform(local).MapID;
        }
    }

    // Вызывается из параллельной задачи движка: только чтение чужих компонентов и запись в сам звук.
    private void ProcessStream(EntityUid entity, AudioComponent component, TransformComponent xform,
        MapCoordinates listener)
    {
        var own = _ownMap;
        if (own != MapId.Nullspace && listener.MapId != own)
            listener = new MapCoordinates(listener.Position, own);

        if (!component.Started)
        {
            component.Started = true;
            component.StartPlaying();
        }

        if (component.Global)
        {
            if (xform.MapID != MapId.Nullspace && listener.MapId != xform.MapID)
            {
                component.Gain = 0f;
                return;
            }

            component.Volume = component.Params.Volume;
            return;
        }

        if (listener.MapId != xform.MapID)
        {
            component.Gain = 0f;
            return;
        }

        var parentUid = xform.ParentUid;
        component.Volume = component.Params.Volume;

        var worldPos = (component.Flags & AudioFlags.GridAudio) != 0x0
            ? _maps.GetGridPosition(parentUid)
            : _xform.GetWorldPosition(entity);

        var delta = worldPos - listener.Position;
        var distance = delta.Length();

        if (_audio.GetAudioDistance(distance) > component.MaxDistance)
        {
            component.Gain = 0f;
            return;
        }

        if (distance > 0f && distance < 0.01f)
        {
            worldPos = listener.Position;
            delta = Vector2.Zero;
            distance = 0f;
        }

        component.Occlusion = (component.Flags & AudioFlags.NoOcclusion) == AudioFlags.NoOcclusion
            ? 0f
            : _audio.GetOcclusion(listener, delta, distance, parentUid);

        component.Position = worldPos;

        if (_physicsQuery.TryGetComponent(parentUid, out var physics))
            component.Velocity = _physics.GetMapLinearVelocity(parentUid, physics);
    }
}
