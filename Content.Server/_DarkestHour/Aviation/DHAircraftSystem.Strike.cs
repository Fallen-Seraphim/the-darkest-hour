using System.Linq;
using System.Numerics;
using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Aviation;
using Content.Shared._DarkestHour.HeavyGuns;
using Content.Shared._DarkestHour.Recon;
using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Aviation;

/// <summary>
/// Удары из атмосферы и остальные бомбы (этап 3, решения автора 2026-11-12).
/// <list type="bullet">
/// <item>РЛС штурмовика раз в несколько секунд снимает сигнатуры противника на поле боя — бронетехнику, грузовики и
/// скопления пехоты — с разбросом (запаздывают и неточны).</item>
/// <item>Цель выбирается правым кликом по карте в режиме «Цель»: метка ЛЦУ, сигнатура или просто координаты.</item>
/// <item>Управляемая бомба — по любой точке; ракеты — только по сигнатуре техники (наводятся на саму машину) или по
/// метке; по пехоте ракету навести только через метку с земли. Подлетающие боеприпасы может перехватить ПВО.</item>
/// <item>Свободнопадающие на бреющем: фугасная, кассетная (россыпь вдоль курса), напалм (сплошная полоса огня),
/// магниевая (химический огонь очагами, прожигает броню).</item>
/// </list>
/// </summary>
public sealed partial class DHAircraftSystem
{
    private const string FireExplosion = "DHArtilleryFire";

    private static readonly ProtoId<DamageTypePrototype> StructuralType = "Structural";
    private static readonly ProtoId<DamageTypePrototype> HeatType = "Heat";

    /// <summary>Радиус, в котором правый клик «цепляет» метку или сигнатуру.</summary>
    private const float PickRadius = 6f;

    /// <summary>Сколько бойцов рядом — уже «скопление пехоты».</summary>
    private const int InfantryCluster = 4;

    private const float InfantryClusterRadius = 5f;

    private readonly List<PendingStrike> _strikes = new();

    private sealed class PendingStrike
    {
        public MapCoordinates Target;
        public EntityUid? Homing;
        public TimeSpan Impact;
        public DHAircraftMunition Kind;
        public EntityUid Aircraft;
    }

    public static bool IsGuided(DHAircraftMunition munition)
    {
        return munition is DHAircraftMunition.GuidedBomb or DHAircraftMunition.MissileAT or DHAircraftMunition.MissileHE;
    }

    public static bool IsFreeFall(DHAircraftMunition munition)
    {
        return munition is DHAircraftMunition.BombHE or DHAircraftMunition.BombCluster or DHAircraftMunition.BombNapalm or
            DHAircraftMunition.BombMagnesium;
    }

    #region Дисплей управления вооружением

    private void OnSelectHardpoint(Entity<DHAircraftComponent> ent, ref DHAircraftSelectHardpointMessage args)
    {
        if (!IsPilot(ent, args.Actor))
            return;

        ent.Comp.SelectedHardpoint = Math.Clamp(args.Slot, 0, ent.Comp.Hardpoints.Count - 1);
        Dirty(ent);
        UpdateUi(ent);
    }

    private void OnMapMode(Entity<DHAircraftComponent> ent, ref DHAircraftMapModeMessage args)
    {
        if (!IsPilot(ent, args.Actor))
            return;

        ent.Comp.MapMode = args.Mode;
        Dirty(ent);
        UpdateUi(ent);
    }

    /// <summary>Правый клик в режиме «Цель»: ближайшая метка, затем сигнатура, иначе — координаты.</summary>
    private void OnTarget(Entity<DHAircraftComponent> ent, ref DHAircraftTargetMessage args)
    {
        if (!IsPilot(ent, args.Actor))
            return;

        var comp = ent.Comp;
        comp.TargetEntity = null;
        comp.TargetIsInfantry = false;
        comp.TargetIsMark = false;

        if (NearestMark(args.Point) is { } mark)
        {
            comp.TargetPoint = mark.Position;
            comp.TargetIsMark = true;
            comp.TargetLabel = Loc.GetString("dh-aircraft-target-mark", ("code", mark.Code));
        }
        else if (NearestSignature(comp, args.Point) is { } signature)
        {
            comp.TargetPoint = signature.Position;
            comp.TargetLabel = Loc.GetString("dh-aircraft-target-signature",
                ("kind", Loc.GetString("dh-aircraft-signature-" + signature.Kind.ToString().ToLowerInvariant())));
            if (signature.Kind == DHAircraftSignatureKind.Infantry)
                comp.TargetIsInfantry = true;
            else if (comp.SignatureSources.TryGetValue(signature.Id, out var source))
                comp.TargetEntity = source;
        }
        else
        {
            comp.TargetPoint = args.Point;
            comp.TargetLabel = Loc.GetString("dh-aircraft-target-coordinates", ("x", (int) args.Point.X), ("y", (int) args.Point.Y));
        }

        Dirty(ent);
        UpdateUi(ent);
    }

    private (Vector2 Position, int Code)? NearestMark(Vector2 point)
    {
        if (_tacmap.GetSurfaceMap() is not { } surface)
            return null;

        (Vector2, int)? best = null;
        var bestDistance = PickRadius;
        var marks = EntityQueryEnumerator<DHLaserMarkComponent, TransformComponent>();
        while (marks.MoveNext(out _, out var mark, out var xform))
        {
            if (xform.MapUid != surface || mark.Faction is { } faction && faction != DHAssaultFaction.Empire)
                continue;

            var position = _transform.GetWorldPosition(xform);
            var distance = Vector2.Distance(position, point);
            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            best = (position, mark.Code);
        }

        return best;
    }

    private static DHAircraftSignature? NearestSignature(DHAircraftComponent comp, Vector2 point)
    {
        DHAircraftSignature? best = null;
        var bestDistance = PickRadius;
        foreach (var signature in comp.Signatures)
        {
            var distance = Vector2.Distance(signature.Position, point);
            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            best = signature;
        }

        return best;
    }

    private void OnFireAction(Entity<DHAircraftPilotComponent> ent, ref DHAircraftFireActionEvent args)
    {
        if (args.Handled || !TryComp<DHAircraftComponent>(ent.Comp.Aircraft, out var aircraft))
            return;

        args.Handled = true;
        Fire((ent.Comp.Aircraft, aircraft));
    }

    private void OnFireMessage(Entity<DHAircraftComponent> ent, ref DHAircraftFireMessage args)
    {
        if (IsPilot(ent, args.Actor))
            Fire(ent);
    }

    /// <summary>Пуск с выбранного узла: управляемое — из атмосферы по цели; свободнопадающее — сброс на бреющем.</summary>
    private void Fire(Entity<DHAircraftComponent> ent)
    {
        var comp = ent.Comp;
        var slot = comp.SelectedHardpoint;
        if (slot < 0 || slot >= comp.Hardpoints.Count || comp.Hardpoints[slot] == DHAircraftMunition.None)
        {
            Notify(ent, Loc.GetString("dh-aircraft-fire-empty"), true);
            return;
        }

        var kind = comp.Hardpoints[slot];
        if (IsFreeFall(kind))
        {
            Drop(ent);
            return;
        }

        if (comp.State is not (DHAircraftState.Airborne or DHAircraftState.Returning))
        {
            Notify(ent, Loc.GetString("dh-aircraft-fire-atmosphere-only"));
            return;
        }

        if (comp.TargetPoint is not { } point || _tacmap.GetSurfaceMap() is not { } surface ||
            !TryComp<MapComponent>(surface, out var surfaceMap))
        {
            Notify(ent, Loc.GetString("dh-aircraft-fire-no-target"));
            return;
        }

        if (Vector2.Distance(_transform.GetWorldPosition(Transform(ent)), point) > comp.LaunchRange)
        {
            Notify(ent, Loc.GetString("dh-aircraft-fire-out-of-range", ("range", (int) comp.LaunchRange)));
            return;
        }

        var missile = kind is DHAircraftMunition.MissileAT or DHAircraftMunition.MissileHE;
        if (missile && comp.TargetEntity == null && !comp.TargetIsMark)
        {
            Notify(ent, Loc.GetString(comp.TargetIsInfantry ? "dh-aircraft-fire-infantry-needs-mark" : "dh-aircraft-fire-needs-lock"));
            return;
        }

        comp.Hardpoints[slot] = DHAircraftMunition.None;
        Dirty(ent);

        _strikes.Add(new PendingStrike
        {
            Target = new MapCoordinates(point, surfaceMap.MapId),
            Homing = missile ? comp.TargetEntity : null,
            Impact = _timing.CurTime + TimeSpan.FromSeconds(missile ? 4 : 8),
            Kind = kind,
            Aircraft = ent,
        });

        _audio.PlayPvs(comp.MissileSound, ent);
        Notify(ent, Loc.GetString(missile ? "dh-aircraft-fire-missile" : "dh-aircraft-fire-bomb", ("slot", slot + 1)));
        UpdateUi(ent);
    }

    #endregion

    #region Попадание, перехват, разрывы

    private void UpdateStrikes(TimeSpan now)
    {
        for (var i = _strikes.Count - 1; i >= 0; i--)
        {
            var strike = _strikes[i];
            if (now < strike.Impact)
                continue;

            _strikes.RemoveAt(i);

            // Ракета наводится на машину до самого попадания.
            var target = strike.Target;
            if (strike.Homing is { } homing && !TerminatingOrDeleted(homing))
            {
                var coords = _transform.GetMapCoordinates(homing);
                if (coords.MapId == target.MapId)
                    target = coords;
            }

            if (TryIntercept(strike, target, now))
                continue;

            Detonate(strike.Kind, target, Vector2.UnitY, strike.Homing);
        }
    }

    /// <summary>ПВО противника у точки попадания пытается сбить подлетающий боеприпас (ракету — вдвое труднее).</summary>
    private bool TryIntercept(PendingStrike strike, MapCoordinates target, TimeSpan now)
    {
        var missile = strike.Kind is DHAircraftMunition.MissileAT or DHAircraftMunition.MissileHE;
        var query = EntityQueryEnumerator<DHInterceptorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var interceptor, out var xform))
        {
            if (xform.MapID != target.MapId || !xform.Anchored || interceptor.Ammo <= 0 || now < interceptor.NextReady ||
                TryComp<DHScanTargetComponent>(uid, out var scan) && scan.Faction == DHAssaultFaction.Empire ||
                Vector2.Distance(_transform.GetWorldPosition(xform), target.Position) > interceptor.Radius)
            {
                continue;
            }

            interceptor.Ammo--;
            interceptor.NextReady = now + interceptor.Cooldown;
            if (!_random.Prob(missile ? interceptor.Chance * 0.5f : interceptor.Chance))
                continue;

            _audio.PlayPvs(new SoundPathSpecifier("/Audio/_DarkestHour/Aviation/airburst.ogg"), uid,
                AudioParams.Default.WithMaxDistance(40f));
            if (TryComp<DHAircraftComponent>(strike.Aircraft, out var aircraft))
                Notify((strike.Aircraft, aircraft), Loc.GetString("dh-aircraft-fire-intercepted"), true);
            return true;
        }

        return false;
    }

    /// <summary>Разрыв боеприпаса в точке; <paramref name="course"/> — курс самолёта (кассеты и напалм ложатся вдоль него).</summary>
    private void Detonate(DHAircraftMunition kind, MapCoordinates target, Vector2 course, EntityUid? hit)
    {
        var side = new Vector2(-course.Y, course.X);
        switch (kind)
        {
            case DHAircraftMunition.BombHE:
                _explosion.QueueExplosion(target, HeavyExplosion, 320f, 10f, 60f, null);
                break;

            case DHAircraftMunition.BombCluster:
                // Россыпь суббоеприпасов в эллипсе вдоль курса: широко и слабо, по пехоте на открытом.
                for (var i = 0; i < 10; i++)
                {
                    var along = (_random.NextFloat() * 2f - 1f) * 7f;
                    var across = (_random.NextFloat() * 2f - 1f) * 3f;
                    var point = target.Position + course * along + side * across;
                    _explosion.QueueExplosion(new MapCoordinates(point, target.MapId), CannonExplosion, 28f, 6f, 9f, null,
                        canCreateVacuum: false);
                }

                break;

            case DHAircraftMunition.BombNapalm:
                // Сплошная полоса огня вдоль курса.
                for (var i = -2; i <= 2; i++)
                {
                    var point = target.Position + course * (i * 2.5f);
                    _explosion.QueueExplosion(new MapCoordinates(point, target.MapId), FireExplosion, 90f, 3f, 14f, null,
                        canCreateVacuum: false);
                }

                break;

            case DHAircraftMunition.BombMagnesium:
                // Химический огонь отдельными очагами; прожигает броню — по технике рядом с очагом напрямую.
                for (var i = 0; i < 6; i++)
                {
                    var point = target.Position + RandomInDisc(5f);
                    var coordinates = new MapCoordinates(point, target.MapId);
                    _explosion.QueueExplosion(coordinates, FireExplosion, 30f, 4f, 10f, null, canCreateVacuum: false);
                    foreach (var vehicle in _lookup.GetEntitiesInRange<DHVehicleComponent>(coordinates, 2f))
                    {
                        _damage.TryChangeDamage(vehicle.Owner,
                            new DamageSpecifier(_proto.Index(HeatType), FixedPoint2.New(220)), ignoreResistances: true);
                    }
                }

                break;

            case DHAircraftMunition.GuidedBomb:
                _explosion.QueueExplosion(target, HeavyExplosion, 360f, 10f, 60f, null);
                break;

            case DHAircraftMunition.MissileHE:
                _explosion.QueueExplosion(target, HeavyExplosion, 180f, 8f, 35f, null);
                break;

            case DHAircraftMunition.MissileAT:
                // Кумулятивная: небольшой разрыв, но по машине — пробитие.
                _explosion.QueueExplosion(target, HeavyExplosion, 60f, 8f, 25f, null);
                if (hit is { } vehicleUid && !TerminatingOrDeleted(vehicleUid) &&
                    (_transform.GetMapCoordinates(vehicleUid).Position - target.Position).Length() < 2.5f)
                {
                    _damage.TryChangeDamage(vehicleUid,
                        new DamageSpecifier(_proto.Index(StructuralType), FixedPoint2.New(600)));
                }

                break;
        }
    }

    #endregion

    #region РЛС: сигнатуры

    /// <summary>Снимок РЛС: техника и скопления пехоты Республики на поле боя, с разбросом.</summary>
    private void UpdateRadar(Entity<DHAircraftComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;
        if (now < comp.NextScan)
            return;

        comp.NextScan = now + comp.ScanInterval;
        comp.Signatures.Clear();
        comp.SignatureSources.Clear();

        if (_tacmap.GetSurfaceMap() is not { } surface)
            return;

        var id = 0;
        var vehicles = EntityQueryEnumerator<DHVehicleComponent, TransformComponent>();
        while (vehicles.MoveNext(out var uid, out var vehicle, out var xform))
        {
            if (vehicle.Faction != DHAssaultFaction.Republic || xform.MapUid != surface)
                continue;

            var proto = MetaData(uid).EntityPrototype?.ID ?? string.Empty;
            var kind = proto.Contains("Truck") ? DHAircraftSignatureKind.Truck : DHAircraftSignatureKind.Armor;
            AddSignature(comp, ++id, kind, _transform.GetWorldPosition(xform), uid);
        }

        // Скопления пехоты: жадная кластеризация живых бойцов Республики.
        var fighters = new List<Vector2>();
        var query = EntityQueryEnumerator<DHAssaultFighterComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out _, out var fighter, out var mob, out var xform))
        {
            if (fighter.Faction == DHAssaultFaction.Republic && mob.CurrentState == MobState.Alive && xform.MapUid == surface)
                fighters.Add(_transform.GetWorldPosition(xform));
        }

        var used = new bool[fighters.Count];
        for (var i = 0; i < fighters.Count; i++)
        {
            if (used[i])
                continue;

            var group = new List<int>();
            for (var j = 0; j < fighters.Count; j++)
            {
                if (!used[j] && Vector2.Distance(fighters[i], fighters[j]) <= InfantryClusterRadius)
                    group.Add(j);
            }

            if (group.Count < InfantryCluster)
                continue;

            foreach (var j in group)
            {
                used[j] = true;
            }

            var center = group.Aggregate(Vector2.Zero, (sum, j) => sum + fighters[j]) / group.Count;
            AddSignature(comp, ++id, DHAircraftSignatureKind.Infantry, center, null);
        }
    }

    private void AddSignature(DHAircraftComponent comp, int id, DHAircraftSignatureKind kind, Vector2 position, EntityUid? source)
    {
        comp.Signatures.Add(new DHAircraftSignature
        {
            Id = id,
            Kind = kind,
            Position = position + RandomInDisc(comp.ScanJitter),
        });

        if (source is { } uid)
            comp.SignatureSources[id] = uid;
    }

    #endregion
}
