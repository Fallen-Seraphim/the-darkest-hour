using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.HeavyGuns;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Popups;

namespace Content.Server._DarkestHour.HeavyGuns;

/// <summary>
/// РЛС: ПВО и ПКО без своей РЛС на гриде или карте слепнут (ПВО бьёт хуже, ПКО не видит орбиту); о челноке
/// противника на подлёте к ЗП её карты сторона узнаёт по радио. От урона замолкает, сварка чинит.
/// </summary>
public sealed partial class DHHeavyGunSystem
{
    /// <summary>Во сколько раз хуже бьёт ПВО без своей РЛС.</summary>
    private const float RadarBlindFactor = 0.4f;

    [Dependency] private DamageableSystem _damageableRadar = default!;

    private void InitializeRadar()
    {
        SubscribeLocalEvent<DHRadarComponent, MapInitEvent>(OnRadarMapInit);
        SubscribeLocalEvent<DHRadarComponent, DamageChangedEvent>(OnRadarDamaged);
        SubscribeLocalEvent<DHRadarComponent, ExaminedEvent>(OnRadarExamined);
    }

    private void OnRadarMapInit(Entity<DHRadarComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent, DHRadarVisuals.Active, ent.Comp.Active);
    }

    private void OnRadarDamaged(Entity<DHRadarComponent> ent, ref DamageChangedEvent args)
    {
        var broken = _damageableRadar.GetTotalDamage((ent.Owner, args.Damageable)).Float() >= ent.Comp.BreakDamage;
        if (broken == !ent.Comp.Active)
            return;

        ent.Comp.Active = !broken;
        _appearance.SetData(ent, DHRadarVisuals.Active, ent.Comp.Active);
        _popup.PopupEntity(Loc.GetString(broken ? "dh-radar-broken" : "dh-radar-repaired"), ent,
            broken ? PopupType.LargeCaution : PopupType.Medium);
    }

    private void OnRadarExamined(Entity<DHRadarComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Active ? "dh-radar-examine-active" : "dh-radar-examine-broken"));
    }

    /// <summary>
    /// РЛС работает: цела и запитана (на «Терминусе» — пока Республика держит АЭС (с резервом) и точку РЛС/связи,
    /// цепочка целей, решение автора 2026-11-19).
    /// </summary>
    public bool RadarWorking(EntityUid uid, DHRadarComponent radar)
    {
        return radar.Active && !Unpowered(uid);
    }

    /// <summary>Есть ли живая РЛС своей стороны на том же гриде (корабль) или той же карте (поверхность).</summary>
    public bool RadarCovers(EntityUid uid)
    {
        var xform = Transform(uid);
        var side = FactionOf(uid);
        var query = EntityQueryEnumerator<DHRadarComponent, TransformComponent>();
        while (query.MoveNext(out var radarUid, out var radar, out var radarXform))
        {
            if (!RadarWorking(radarUid, radar) || !SameSide(side, radar.Faction))
                continue;

            if (xform.GridUid != null && radarXform.GridUid == xform.GridUid ||
                StackGround(radarXform.MapUid) == StackGround(xform.MapUid))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Карта уровня 0 стопки Z-уровней (РЛС на горе, большой Терминус, видит всю стопку).</summary>
    private EntityUid? StackGround(EntityUid? map)
    {
        if (map is { } m && _zLevels.TryGetLevel(m, out var level))
            return _zLevels.LevelMap(level.Stack, 0) ?? m;

        return map;
    }

    /// <summary>Множитель шанса ПВО: 1 с РЛС, меньше — вслепую.</summary>
    private float RadarFactor(EntityUid uid)
    {
        return RadarCovers(uid) ? 1f : RadarBlindFactor;
    }

    /// <summary>
    /// Челнок <paramref name="attacker"/> стороны идёт на ЗП: живые РЛС другой стороны на карте этой ЗП
    /// предупреждают свою сторону по радио.
    /// </summary>
    public void WarnIncomingDropship(EntityUid zone, DHAssaultFaction attacker, string shipName)
    {
        var map = StackGround(Transform(zone).MapUid);
        var warned = new HashSet<DHAssaultFaction>();
        var query = EntityQueryEnumerator<DHRadarComponent, TransformComponent>();
        while (query.MoveNext(out var radarUid, out var radar, out var xform))
        {
            if (!RadarWorking(radarUid, radar) || radar.Faction == attacker || StackGround(xform.MapUid) != map ||
                !warned.Add(radar.Faction))
                continue;

            _assault.AnnounceToFaction(radar.Faction, Loc.GetString("dh-radar-sender"),
                Loc.GetString("dh-radar-incoming-dropship", ("ship", shipName), ("zone", Name(zone))));
        }
    }
}
