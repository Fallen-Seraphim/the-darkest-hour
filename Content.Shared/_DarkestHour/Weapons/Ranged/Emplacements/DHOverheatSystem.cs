using Content.Shared.Examine;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._DarkestHour.Weapons.Ranged.Emplacements;

/// <summary>
/// Перегрев ствола: каждый выстрел греет, ствол остывает со временем; при перегреве оружие замолкает на
/// несколько секунд, а станок получает урон. По образцу OverheatSystem из RMC-14 (MIT), но без постоянного тика:
/// текущее тепло вычисляется по времени последнего изменения.
/// </summary>
public sealed partial class DHOverheatSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHOverheatComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<DHOverheatComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<DHOverheatComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>Текущее тепло ствола с учётом остывания.</summary>
    public float GetHeat(DHOverheatComponent comp)
    {
        var cooled = comp.CooldownRate * (float) (_timing.CurTime - comp.HeatUpdated).TotalSeconds;
        return MathF.Max(0f, comp.Heat - cooled);
    }

    public bool IsOverheated(DHOverheatComponent comp)
    {
        return _timing.CurTime < comp.OverheatedUntil;
    }

    private void OnAttemptShoot(Entity<DHOverheatComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled || !IsOverheated(ent.Comp))
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString("dh-overheat-locked");
    }

    private void OnGunShot(Entity<DHOverheatComponent> ent, ref GunShotEvent args)
    {
        var heat = GetHeat(ent.Comp) + ent.Comp.HeatPerShot * Math.Max(1, args.Ammo.Count);
        if (heat >= ent.Comp.MaxHeat)
        {
            heat *= ent.Comp.OverheatHeatMultiplier;
            ent.Comp.OverheatedUntil = _timing.CurTime + ent.Comp.OverheatLockout;
            _audio.PlayPredicted(ent.Comp.OverheatSound, ent, args.User);

            var ev = new DHWeaponOverheatedEvent(ent.Comp.Damage);
            RaiseLocalEvent(ent, ref ev);
        }

        ent.Comp.Heat = heat;
        ent.Comp.HeatUpdated = _timing.CurTime;
        Dirty(ent);
    }

    private void OnExamined(Entity<DHOverheatComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var percent = (int) MathF.Round(GetHeat(ent.Comp) / ent.Comp.MaxHeat * 100f);
        args.PushMarkup(Loc.GetString(IsOverheated(ent.Comp) ? "dh-overheat-examine-locked" : "dh-overheat-examine",
            ("percent", percent)));
    }
}
