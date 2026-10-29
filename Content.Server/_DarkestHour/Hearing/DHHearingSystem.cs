using System.Numerics;
using Content.Shared._DarkestHour.Hearing;
using Content.Shared.Camera;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Hearing;

/// <summary>Накопленный шум у бойца (только сервер): спадает со временем, выше порога — звон в ушах.</summary>
[RegisterComponent]
public sealed partial class DHNoiseExposureComponent : Component
{
    [ViewVariables]
    public float Level;

    [ViewVariables]
    public TimeSpan LastUpdate;
}

/// <summary>
/// Слух: выстрелы копят шум у стрелка (и вполсилы — у стоящих рядом), разрывы и орудия — у всех вокруг.
/// Выше порога — звон в ушах на 6–20 с. Пулемёты, снайперки и орудия трясут экран окружающим.
/// </summary>
public sealed partial class DHHearingSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>Порог звона в ушах.</summary>
    private const float Threshold = 150f;

    /// <summary>Спад шума в секунду.</summary>
    private const float Decay = 12f;

    /// <summary>Сколько шума остаётся после того, как зазвенело.</summary>
    private const float AfterRinging = 90f;

    /// <summary>Доля шума соседям по стрельбе и радиус «рядом».</summary>
    private const float BystanderShare = 0.5f;

    private const float BystanderRange = 3f;

    private static readonly TimeSpan MinRinging = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan MaxRinging = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(0.5);

    private TimeSpan _nextCleanup;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GunComponent, GunShotEvent>(OnGunShot);
    }

    #region Выстрелы

    /// <summary>Громкость по классу оружия (id генератора оружия): шум стрелку, тряска окружающим, радиус тряски.</summary>
    private static (float Noise, float Shake, float Radius) DefaultNoise(string? id)
    {
        if (id == null)
            return (3f, 0f, 0f);

        if (id.Contains("MountedGun") || id.Contains("HMG"))
            return (6f, 0.35f, 4f);
        if (id.Contains("AMR"))
            return (20f, 0.6f, 4f);
        if (id.Contains("Sniper"))
            return (12f, 0.3f, 3f);
        if (id.Contains("LMG"))
            return (4f, 0.15f, 3f);
        if (id.Contains("Shotgun"))
            return (6f, 0f, 0f);
        if (id.Contains("Energy"))
            return (1f, 0f, 0f);
        if (id.Contains("SMG") || id.Contains("Pistol"))
            return (2.5f, 0f, 0f);
        return (4f, 0f, 0f);
    }

    private void OnGunShot(Entity<GunComponent> ent, ref GunShotEvent args)
    {
        var (noise, shake, radius) = TryComp<DHGunNoiseComponent>(ent, out var custom)
            ? (custom.Noise, custom.Shake, custom.ShakeRadius)
            : DefaultNoise(MetaData(ent).EntityPrototype?.ID);

        var shooter = args.User;
        AddNoise(shooter, noise);

        var center = _transform.GetMapCoordinates(shooter);
        var range = MathF.Max(BystanderRange, shake > 0f ? radius : 0f);
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(center, range))
        {
            if (mob.Owner == shooter)
                continue;

            var distance = Vector2.Distance(_transform.GetWorldPosition(mob), center.Position);
            if (distance <= BystanderRange)
                AddNoise(mob, noise * BystanderShare);

            if (shake > 0f && distance <= radius)
                _recoil.KickCamera(mob, _random.NextAngle().ToVec() * shake * (1f - distance / (radius + 1f)));
        }
    }

    #endregion

    /// <summary>
    /// Громкое событие (выстрел орудия, разрыв): шум всем в радиусе — полный в центре, спадает к краю; тряска
    /// экрана, если <paramref name="shake"/> больше нуля.
    /// </summary>
    public void Blast(MapCoordinates at, float noise, float radius, float shake = 0f)
    {
        if (at.MapId == MapId.Nullspace || radius <= 0f)
            return;

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(at, radius))
        {
            var distance = Vector2.Distance(_transform.GetWorldPosition(mob), at.Position);
            var k = 1f - distance / radius;
            if (k <= 0f)
                continue;

            AddNoise(mob, noise * k);
            if (shake > 0f)
                _recoil.KickCamera(mob, _random.NextAngle().ToVec() * shake * k);
        }
    }

    /// <summary>Добавить шум бойцу с учётом защиты слуха; выше порога — звон.</summary>
    public void AddNoise(EntityUid uid, float amount)
    {
        if (amount <= 0f || !HasComp<MobStateComponent>(uid))
            return;

        var now = _timing.CurTime;
        var exposure = EnsureComp<DHNoiseExposureComponent>(uid);
        var elapsed = (float) (now - exposure.LastUpdate).TotalSeconds;
        exposure.Level = MathF.Max(0f, exposure.Level - elapsed * Decay);
        exposure.LastUpdate = now;
        exposure.Level += amount * ProtectionFactor(uid);

        if (exposure.Level < Threshold)
            return;

        var seconds = Math.Clamp(MinRinging.TotalSeconds + (exposure.Level - Threshold) / 10f,
            MinRinging.TotalSeconds, MaxRinging.TotalSeconds);
        exposure.Level = AfterRinging;
        Ring(uid, TimeSpan.FromSeconds(seconds));
    }

    private float ProtectionFactor(EntityUid uid)
    {
        var factor = 1f;
        foreach (var slot in new[] { "ears", "head" })
        {
            if (!_inventory.TryGetSlotEntity(uid, slot, out var item))
                continue;

            if (TryComp<DHHearingProtectionComponent>(item, out var protection))
                factor = MathF.Min(factor, protection.Factor);
            // Закрытые «альтернативные» гарнитуры (ванильные ClothingHeadsetAlt* и их наследники) тоже глушат.
            else if (slot == "ears" && MetaData(item.Value).EntityPrototype?.ID.Contains("HeadsetAlt") == true)
                factor = MathF.Min(factor, 0.5f);
        }

        return factor;
    }

    private void Ring(EntityUid uid, TimeSpan duration)
    {
        var now = _timing.CurTime;
        var tinnitus = EnsureComp<DHTinnitusComponent>(uid);
        var fresh = tinnitus.EndTime <= now;
        if (fresh)
            tinnitus.StartTime = now;

        if (now + duration > tinnitus.EndTime)
            tinnitus.EndTime = now + duration;
        Dirty(uid, tinnitus);

        if (fresh)
            _popup.PopupEntity(Loc.GetString("dh-tinnitus-start"), uid, uid, PopupType.MediumCaution);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextCleanup)
            return;

        _nextCleanup = now + CleanupInterval;
        var query = EntityQueryEnumerator<DHTinnitusComponent>();
        var done = new List<EntityUid>();
        while (query.MoveNext(out var uid, out var tinnitus))
        {
            if (now >= tinnitus.EndTime)
                done.Add(uid);
        }

        foreach (var uid in done)
        {
            RemComp<DHTinnitusComponent>(uid);
        }
    }
}
