using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Recon;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Shared.Map;

namespace Content.Server._DarkestHour.HeavyGuns;

/// <summary>
/// Глобальный скан с авианосца (решение автора 2026-11-04): вся поверхность тела под авианосцем. Отчёт — координаты
/// бункера ПКО и базы Республики (всегда), миномётов, пушек, техники и зениток (если не под маскировочной сетью) и
/// крупных скоплений пехоты (15+ бойцов рядом). Координаты — с небольшим разбросом; отчёт уходит стороне по радио,
/// остаётся на консоли, а на тактической карте стороны появляются отметки (до следующего скана). Перезарядка 7.5 мин.
/// </summary>
public sealed partial class DHHeavyGunSystem
{
    [Dependency] private SharedStealthSystem _stealth = default!;

    private const string ScanContactProto = "DHScanContact";

    /// <summary>Видимость, ниже которой замаскированного скан не берёт.</summary>
    private const float ScanStealthThreshold = 0.35f;

    /// <summary>Одинаковые объекты ближе этого — одна строка отчёта (бункер ПКО из нескольких орудий).</summary>
    private const float ScanMergeRadius = 25f;

    private void InitializeRecon()
    {
        Subs.BuiEvents<DHOrbitalScanConsoleComponent>(DHOrbitalScanUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnScanUiOpened);
            subs.Event<DHOrbitalScanMessage>(OnScan);
        });
    }

    /// <summary>Поверхность, которую видно с места консоли: тело, в атмосфере которого висим, иначе любая в системе.</summary>
    private EntityUid? ScanSurface(EntityUid console)
    {
        var map = Transform(console).MapUid;
        if (TryComp<DHPlanetAtmosphereMapComponent>(map, out var atmosphere))
            return _landing.GetSurface(atmosphere.System, atmosphere.Body);

        if (!_celestial.TryGetSystem(map, out var system, out _))
            return null;

        var surfaces = EntityQueryEnumerator<DHPlanetSurfaceMapComponent>();
        while (surfaces.MoveNext(out var uid, out var surface))
        {
            if (surface.System == system.ID)
                return uid;
        }

        return null;
    }

    private void OnScanUiOpened(Entity<DHOrbitalScanConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateScanUi(ent);
    }

    private void UpdateScanUi(Entity<DHOrbitalScanConsoleComponent> ent)
    {
        var surface = ScanSurface(ent);
        var now = _timing.CurTime;
        var cooldown = Math.Max(0, (int) Math.Ceiling((ent.Comp.NextScan - now).TotalSeconds));
        var ready = surface != null && cooldown == 0;

        var status = surface == null
            ? Loc.GetString("dh-scan-status-no-surface")
            : cooldown > 0
                ? Loc.GetString("dh-scan-status-cooldown", ("minutes", cooldown / 60), ("seconds", cooldown % 60))
                : Loc.GetString("dh-scan-status-ready");

        _ui.SetUiState(ent.Owner, DHOrbitalScanUiKey.Key, new DHOrbitalScanState(
            surface == null ? string.Empty : Name(surface.Value), ready, cooldown, status, ent.Comp.LastReport));
    }

    /// <summary>
    /// Миномёт — стороны того, кто его развернул (на поверхности станция у всех одна — база Республики, по ней
    /// сторону не понять).
    /// </summary>
    private void SetScanFactionFromUser(EntityUid target, EntityUid user)
    {
        if (TryComp<DHScanTargetComponent>(target, out var comp) && FactionOf(user) is { } faction)
            comp.Faction = faction;
    }

    /// <summary>Чей объект для скана.</summary>
    private DHAssaultFaction? ScanFactionOf(EntityUid uid, DHScanTargetComponent target)
    {
        if (target.Faction is { } faction)
            return faction;

        if (TryComp<DHVehicleComponent>(uid, out var vehicle))
            return vehicle.Faction;

        return FactionOf(uid);
    }

    private void OnScan(Entity<DHOrbitalScanConsoleComponent> ent, ref DHOrbitalScanMessage args)
    {
        if (ScanSurface(ent) is not { } surface || _timing.CurTime < ent.Comp.NextScan)
        {
            UpdateScanUi(ent);
            return;
        }

        ent.Comp.NextScan = _timing.CurTime + ent.Comp.Cooldown;
        var side = FactionOf(ent);
        var mapId = Transform(surface).MapID;

        // Сети на этой поверхности: под ними не видно.
        var nets = new List<(Vector2 Position, float Radius)>();
        var netQuery = EntityQueryEnumerator<DHCamoNetComponent, TransformComponent>();
        while (netQuery.MoveNext(out _, out var net, out var netXform))
        {
            if (netXform.MapID == mapId)
                nets.Add((_transform.GetWorldPosition(netXform), net.Radius));
        }

        bool Hidden(Vector2 position) => nets.Any(n => Vector2.Distance(n.Position, position) <= n.Radius);

        // Объекты: (вид, название, точка). Одинаковые рядом — одной строкой.
        var found = new List<(DHScanTargetKind Kind, string Name, Vector2 Position)>();
        var targets = EntityQueryEnumerator<DHScanTargetComponent, TransformComponent>();
        while (targets.MoveNext(out var uid, out var target, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var owner = ScanFactionOf(uid, target);
            if (side != null && owner == side)
                continue;

            var position = _transform.GetWorldPosition(xform);
            if (!target.AlwaysVisible && Hidden(position))
                continue;

            var name = target.Kind switch
            {
                DHScanTargetKind.AntiOrbital => Loc.GetString("dh-scan-kind-anti-orbital"),
                DHScanTargetKind.Base => Loc.GetString("dh-scan-kind-base"),
                DHScanTargetKind.AntiAir => Loc.GetString("dh-scan-kind-anti-air"),
                _ => Name(uid),
            };

            if (found.Any(f => f.Kind == target.Kind && f.Name == name &&
                               Vector2.Distance(f.Position, position) <= ScanMergeRadius))
            {
                continue;
            }

            found.Add((target.Kind, name, position));
        }

        // Пехота: только крупные скопления.
        var infantry = new List<Vector2>();
        var mobs = EntityQueryEnumerator<MobStateComponent, DHAssaultFighterComponent, TransformComponent>();
        while (mobs.MoveNext(out var uid, out var mob, out var fighter, out var xform))
        {
            if (xform.MapID != mapId || mob.CurrentState == MobState.Dead || side != null && fighter.Faction == side)
                continue;

            var position = _transform.GetWorldPosition(xform);
            if (Hidden(position) ||
                HasComp<StealthComponent>(uid) && _stealth.GetVisibility(uid) < ScanStealthThreshold)
            {
                continue;
            }

            infantry.Add(position);
        }

        var clusters = new List<(Vector2 Center, int Count)>();
        var assigned = new bool[infantry.Count];
        for (var i = 0; i < infantry.Count; i++)
        {
            if (assigned[i])
                continue;

            var members = new List<int>();
            for (var j = 0; j < infantry.Count; j++)
            {
                if (!assigned[j] && Vector2.Distance(infantry[i], infantry[j]) <= ent.Comp.ClusterRadius)
                    members.Add(j);
            }

            if (members.Count < ent.Comp.ClusterSize)
                continue;

            var center = Vector2.Zero;
            foreach (var j in members)
            {
                assigned[j] = true;
                center += infantry[j];
            }

            clusters.Add((center / members.Count, members.Count));
        }

        // Отчёт и отметки на тактической карте стороны.
        ClearScanContacts(side);
        var lines = new List<string>();
        foreach (var (kind, name, position) in found.OrderBy(f => f.Kind))
        {
            var point = Jitter(position, ent.Comp.Spread);
            lines.Add(Loc.GetString("dh-scan-line", ("name", name), ("x", (int) MathF.Round(point.X)),
                ("y", (int) MathF.Round(point.Y))));
            SpawnScanContact(side, name, new MapCoordinates(point, mapId));
        }

        foreach (var (center, count) in clusters)
        {
            var point = Jitter(center, ent.Comp.Spread);
            var name = Loc.GetString("dh-scan-kind-infantry", ("count", count));
            lines.Add(Loc.GetString("dh-scan-line", ("name", name), ("x", (int) MathF.Round(point.X)),
                ("y", (int) MathF.Round(point.Y))));
            SpawnScanContact(side, name, new MapCoordinates(point, mapId));
        }

        // Остальные замеченные бойцы (вне скоплений) — точками на тактической карте, без строки в отчёте.
        for (var i = 0; i < infantry.Count; i++)
        {
            if (!assigned[i])
                SpawnScanContact(side, string.Empty, new MapCoordinates(Jitter(infantry[i], ent.Comp.Spread * 0.5f), mapId), true);
        }

        var report = new StringBuilder();
        report.AppendLine(Loc.GetString("dh-scan-report-header", ("surface", Name(surface))));
        if (lines.Count == 0)
            report.AppendLine(Loc.GetString("dh-scan-report-empty"));

        foreach (var line in lines)
        {
            report.AppendLine(line);
        }

        ent.Comp.LastReport = report.ToString().TrimEnd();

        var radio = lines.Count == 0
            ? Loc.GetString("dh-scan-report-empty")
            : string.Join("; ", lines);

        if (side is { } faction)
            _assault.AnnounceToFaction(faction, Loc.GetString("dh-scan-sender"), radio);
        else
            _popup.PopupEntity(radio, ent, args.Actor);

        UpdateScanUi(ent);
    }

    private Vector2 Jitter(Vector2 position, float spread)
    {
        return position + new Vector2(_random.NextFloat(-spread, spread), _random.NextFloat(-spread, spread));
    }

    private void ClearScanContacts(DHAssaultFaction? side)
    {
        var query = EntityQueryEnumerator<DHScanContactComponent>();
        while (query.MoveNext(out var uid, out var contact))
        {
            if (contact.Faction == side)
                QueueDel(uid);
        }
    }

    private void SpawnScanContact(DHAssaultFaction? side, string label, MapCoordinates coordinates, bool blip = false)
    {
        var contact = Spawn(ScanContactProto, coordinates);
        var comp = EnsureComp<DHScanContactComponent>(contact);
        comp.Faction = side;
        comp.Label = label;
        comp.Blip = blip;
    }
}
