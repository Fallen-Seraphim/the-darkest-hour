using System.Numerics;
using Content.Shared._DarkestHour.Levels;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Hearing;

/// <summary>
/// Дальние звуки боя (план осады п. 20, решение автора 2026-11-20): громкие выстрелы и разрывы слышны далеко за
/// обычной слышимостью — с задержкой по «игровой» скорости звука (<see cref="SoundSpeed"/> клеток/с), тише с
/// расстоянием и с направлением (звук ставится со стороны источника). Совсем крупные разрывы (ОБ) слышны всей карте
/// вместе с её этажами и подземельем.
/// </summary>
public sealed class DHDistantSoundSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private Content.Server._DarkestHour.Medical.Implants.DHImplantSystem _implants = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>Игровая скорость звука, клеток в секунду.</summary>
    public const float SoundSpeed = 120f;

    /// <summary>Дольше звук не идёт, даже через всю карту.</summary>
    private const float MaxDelay = 4f;

    /// <summary>На каком расстоянии от слушателя ставится звук (для направления).</summary>
    private const float PlaceDistance = 8f;

    /// <summary>Насколько тише на краю слышимости, дБ.</summary>
    private const float Falloff = 12f;

    private static readonly SoundSpecifier CannonSound = new SoundCollectionSpecifier("DHDistantCannon");
    private static readonly SoundSpecifier ExplosionSound = new SoundPathSpecifier("/Audio/Effects/explosionfar.ogg");
    private static readonly SoundSpecifier SmallExplosionSound = new SoundPathSpecifier("/Audio/Effects/explosionsmallfar.ogg");

    private static readonly TimeSpan GunshotInterval = TimeSpan.FromSeconds(0.3);

    private readonly List<Pending> _pending = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextGunshot = new();

    private record struct Pending(TimeSpan At, ICommonSession Session, SoundSpecifier Sound, MapCoordinates Origin,
        float Volume, float Pitch);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHExplosionSpawnedEvent>(OnExplosion);
    }

    /// <summary>Громкий выстрел (пулемёт, снайперка, пушка техники): тот же звук, ниже и глуше, не чаще 3 раз в секунду.</summary>
    public void Gunshot(Entity<GunComponent> gun, MapCoordinates origin, float noise)
    {
        var now = _timing.CurTime;
        if (_nextGunshot.Count > 1000)
            _nextGunshot.Clear();

        if (_nextGunshot.TryGetValue(gun, out var next) && now < next)
            return;

        _nextGunshot[gun] = now + GunshotInterval;
        if (gun.Comp.SoundGunshotModified is not { } sound)
            return;

        var max = Math.Clamp(noise * 6f, 60f, 160f);
        Emit(origin, sound, 15f, max, -4f, 0.8f);
    }

    /// <summary>Выстрел орудия: пушка, ПКО/ОБ, миномёт.</summary>
    public void Cannon(MapCoordinates origin, float maxRange, float volume = 0f, float pitch = 1f)
    {
        Emit(origin, CannonSound, 18f, maxRange, volume, pitch);
    }

    private void OnExplosion(ref DHExplosionSpawnedEvent args)
    {
        var intensity = args.TotalIntensity;
        if (intensity < 15f)
            return;

        if (intensity < 60f)
            Emit(args.Epicenter, SmallExplosionSound, 16f, 100f, -2f, 1f);
        else if (intensity < 1000f)
            Emit(args.Epicenter, ExplosionSound, 20f, 220f, 0f, 1f);
        else
            Emit(args.Epicenter, ExplosionSound, 24f, float.PositiveInfinity, 4f, 0.85f); // ОБ, тяжёлые бомбы — вся карта
    }

    /// <summary>Разослать дальний звук всем, кто дальше обычной слышимости <paramref name="near"/>.</summary>
    public void Emit(MapCoordinates origin, SoundSpecifier sound, float near, float max, float volume, float pitch)
    {
        if (origin.MapId == MapId.Nullspace)
            return;

        var now = _timing.CurTime;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } listener || TerminatingOrDeleted(listener))
                continue;

            var at = _transform.GetMapCoordinates(listener);
            if (!SameStack(at.MapId, origin.MapId))
                continue;

            var distance = Vector2.Distance(at.Position, origin.Position);
            if (distance <= near || distance > max)
                continue;

            var span = float.IsFinite(max) ? max - near : 300f;
            var k = Math.Clamp((distance - near) / MathF.Max(span, 1f), 0f, 1f);
            var delay = MathF.Min(distance / SoundSpeed, MaxDelay);
            // Имплант «Эхо» — дальние звуки слышны чётче (план осады п. 29).
            var echo = _implants.HasImplant(listener, Content.Shared._DarkestHour.Medical.Implants.DHImplantKind.Echo) ? 4f : 0f;
            _pending.Add(new Pending(now + TimeSpan.FromSeconds(delay), session, sound, origin, volume - Falloff * k + echo, pitch));
        }
    }

    /// <summary>Одна карта или этажи/подземелье одной стопки уровней.</summary>
    private bool SameStack(MapId a, MapId b)
    {
        if (a == b)
            return true;

        return TryComp<DHZLevelComponent>(_map.GetMapOrInvalid(a), out var levelA) &&
               TryComp<DHZLevelComponent>(_map.GetMapOrInvalid(b), out var levelB) &&
               levelA.Stack == levelB.Stack;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pending.Count == 0)
            return;

        var now = _timing.CurTime;
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var pending = _pending[i];
            if (now < pending.At)
                continue;

            _pending.RemoveAt(i);
            Play(pending);
        }
    }

    private void Play(Pending pending)
    {
        if (pending.Session.AttachedEntity is not { } listener || TerminatingOrDeleted(listener))
            return;

        var at = _transform.GetMapCoordinates(listener);
        if (!SameStack(at.MapId, pending.Origin.MapId))
            return;

        // Направление — со стороны источника, на небольшом расстоянии от слушателя (иначе звук отсечётся дальностью).
        var offset = pending.Origin.Position - at.Position;
        var direction = offset.LengthSquared() > 0.01f ? Vector2.Normalize(offset) : Vector2.Zero;
        var place = new MapCoordinates(at.Position + direction * PlaceDistance, at.MapId);

        var audio = AudioParams.Default
            .WithVolume(pending.Volume)
            .WithPitchScale(pending.Pitch)
            .WithMaxDistance(PlaceDistance * 3f)
            .WithVariation(0.05f);

        _audio.PlayStatic(pending.Sound, Filter.SinglePlayer(pending.Session), _transform.ToCoordinates(place), true, audio);
    }
}
