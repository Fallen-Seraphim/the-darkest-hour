using System.Numerics;
using Content.Shared._DarkestHour.Aviation;
using static Content.Shared._DarkestHour.Aviation.DHAircraftBallistics;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Aviation;

/// <summary>
/// ПЗРК Республики: захват и пуск по штурмовику на бреющем (в атмосфере он недосягаем). См. <see cref="DHManpadsComponent"/>.
/// </summary>
public sealed partial class DHAircraftSystem
{
    private readonly List<PendingManpads> _manpads = new();

    private sealed class PendingManpads
    {
        public EntityUid Aircraft;
        public TimeSpan Impact;
        public float HitChance;
        public float DecoyChance;
        public float Damage;
    }

    private void InitializeManpads()
    {
        SubscribeLocalEvent<DHManpadsComponent, AfterInteractEvent>(OnManpadsAfterInteract);
        SubscribeLocalEvent<DHManpadsComponent, InteractUsingEvent>(OnManpadsInteractUsing);
        SubscribeLocalEvent<DHManpadsComponent, DHManpadsLockDoAfterEvent>(OnManpadsLock);
        SubscribeLocalEvent<DHManpadsComponent, DHManpadsReloadDoAfterEvent>(OnManpadsReload);
        SubscribeLocalEvent<DHManpadsComponent, ExaminedEvent>(OnManpadsExamined);
    }

    private void OnManpadsExamined(Entity<DHManpadsComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Loaded ? "dh-manpads-examine-loaded" : "dh-manpads-examine-empty"));
    }

    /// <summary>Штурмовик на бреющем над стрелком (та же карта, в дальности захвата).</summary>
    private Entity<DHAircraftComponent>? ManpadsTarget(EntityUid user, float range)
    {
        var xform = Transform(user);
        var position = _transform.GetWorldPosition(xform);
        Entity<DHAircraftComponent>? best = null;
        var bestDistance = range;
        var query = EntityQueryEnumerator<DHAircraftComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var planeXform))
        {
            if (comp.State != DHAircraftState.Pass || GroundMapUid(comp, planeXform) != xform.MapUid)
                continue;

            var distance = Vector2.Distance(_transform.GetWorldPosition(planeXform), position);
            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            best = (uid, comp);
        }

        return best;
    }

    private void OnManpadsAfterInteract(Entity<DHManpadsComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.User;
        if (!TryComp<WieldableComponent>(ent, out var wieldable) || !wieldable.Wielded)
        {
            _popup.PopupEntity(Loc.GetString("dh-manpads-not-wielded"), ent, user);
            return;
        }

        if (!ent.Comp.Loaded)
        {
            _popup.PopupEntity(Loc.GetString("dh-manpads-empty"), ent, user);
            return;
        }

        // Только по штурмовику на бреющем: в атмосфере ПЗРК его не достаёт.
        if (ManpadsTarget(user, ent.Comp.Range) is not { } target)
        {
            _popup.PopupEntity(Loc.GetString("dh-manpads-no-target"), ent, user);
            return;
        }

        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.LockTime,
            new DHManpadsLockDoAfterEvent(), ent.Owner, used: ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = true,
            BreakOnDropItem = true,
        });

        if (!started)
            return;

        _audio.PlayEntity(ent.Comp.LockSound, user, user);
        _popup.PopupEntity(Loc.GetString("dh-manpads-locking"), ent, user);
        WarnLocked(target);
    }

    private void OnManpadsLock(Entity<DHManpadsComponent> ent, ref DHManpadsLockDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !ent.Comp.Loaded)
            return;

        args.Handled = true;
        var user = args.User;
        if (!TryComp<WieldableComponent>(ent, out var wieldable) || !wieldable.Wielded)
            return;

        // Пока держали захват, штурмовик мог уйти с бреющего или из дальности.
        if (ManpadsTarget(user, ent.Comp.Range) is not { } target)
        {
            _popup.PopupEntity(Loc.GetString("dh-manpads-lost"), ent, user);
            return;
        }

        ent.Comp.Loaded = false;
        Dirty(ent);
        _audio.PlayPvs(ent.Comp.LaunchSound, user, AudioParams.Default.WithMaxDistance(30f));
        _popup.PopupEntity(Loc.GetString("dh-manpads-launched"), ent, user);

        var distance = Vector2.Distance(_transform.GetWorldPosition(user), _transform.GetWorldPosition(target));
        _manpads.Add(new PendingManpads
        {
            Aircraft = target.Owner,
            Impact = _timing.CurTime + TimeSpan.FromSeconds(Math.Max(0.4f, distance / ent.Comp.MissileSpeed)),
            HitChance = Math.Min(0.95f, ent.Comp.HitChance * AirDefenceFactor(target.Comp.Altitude)),
            DecoyChance = ent.Comp.FlareDecoyChance,
            Damage = ent.Comp.Damage,
        });
    }

    private void UpdateManpads(TimeSpan now)
    {
        for (var i = _manpads.Count - 1; i >= 0; i--)
        {
            var missile = _manpads[i];
            if (now < missile.Impact)
                continue;

            _manpads.RemoveAt(i);
            if (!TryComp<DHAircraftComponent>(missile.Aircraft, out var comp))
                continue;

            var plane = (missile.Aircraft, comp);

            // Ушёл с бреющего — ракета не догнала.
            if (comp.State != DHAircraftState.Pass)
                continue;

            if (now < comp.FlaresUntil && _random.Prob(missile.DecoyChance))
            {
                Notify(plane, Loc.GetString("dh-aircraft-manpads-decoyed"));
                continue;
            }

            if (!_random.Prob(missile.HitChance))
            {
                Notify(plane, Loc.GetString("dh-aircraft-manpads-missed"));
                continue;
            }

            _audio.PlayPvs(new SoundPathSpecifier("/Audio/_DarkestHour/Aviation/airburst.ogg"), missile.Aircraft,
                AudioParams.Default.WithMaxDistance(40f));
            Hit(missile.Aircraft, missile.Damage);
        }
    }

    private void OnManpadsInteractUsing(Entity<DHManpadsComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<DHManpadsMissileComponent>(args.Used))
            return;

        args.Handled = true;
        if (ent.Comp.Loaded)
        {
            _popup.PopupEntity(Loc.GetString("dh-manpads-already-loaded"), ent, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.ReloadTime,
            new DHManpadsReloadDoAfterEvent(), ent.Owner, ent.Owner, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnManpadsReload(Entity<DHManpadsComponent> ent, ref DHManpadsReloadDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Loaded || args.Used is not { } missile ||
            !HasComp<DHManpadsMissileComponent>(missile))
        {
            return;
        }

        args.Handled = true;
        ent.Comp.Loaded = true;
        Dirty(ent);
        QueueDel(missile);
        _audio.PlayPvs(ent.Comp.ReloadSound, ent);
        _popup.PopupEntity(Loc.GetString("dh-manpads-reloaded"), ent, args.User);
    }
}
