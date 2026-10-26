using System.Linq;
using System.Numerics;
using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.HeavyGuns;
using Content.Shared._DarkestHour.Recon;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Shared.Map;
using Robust.Shared.Spawners;

namespace Content.Server._DarkestHour.HeavyGuns;

/// <summary>
/// Орбитальный скан с авианосца: в круге на поверхности тела под ним отмечает живых (кроме тех, кто под
/// маскировочной сетью или замер в маскхалате), сообщает итог стороне по радио; долгая перезарядка.
/// </summary>
public sealed partial class DHHeavyGunSystem
{
    [Dependency] private SharedStealthSystem _stealth = default!;

    private const string ScanBlipProto = "DHOrbitalScanBlip";

    /// <summary>Видимость, ниже которой замаскированного скан не берёт.</summary>
    private const float ScanStealthThreshold = 0.35f;

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
                ? Loc.GetString("dh-scan-status-cooldown", ("seconds", cooldown))
                : Loc.GetString("dh-scan-status-ready");

        var marks = new List<DHOrbitalScanMarkEntry>();
        if (surface != null)
        {
            var found = new List<DHLaserMarkEntry>();
            CollectMarks(surface.Value, FactionOf(ent), Vector2.Zero, null, float.MaxValue, found);
            marks.AddRange(found.Select(m => new DHOrbitalScanMarkEntry(m.Code, m.Position)));
        }

        _ui.SetUiState(ent.Owner, DHOrbitalScanUiKey.Key, new DHOrbitalScanState(
            surface == null ? string.Empty : Name(surface.Value), ready, cooldown, ent.Comp.Radius, status, marks));
    }

    private void OnScan(Entity<DHOrbitalScanConsoleComponent> ent, ref DHOrbitalScanMessage args)
    {
        if (ScanSurface(ent) is not { } surface || _timing.CurTime < ent.Comp.NextScan ||
            !float.IsFinite(args.Position.X) || !float.IsFinite(args.Position.Y))
        {
            UpdateScanUi(ent);
            return;
        }

        ent.Comp.NextScan = _timing.CurTime + ent.Comp.Cooldown;
        var side = FactionOf(ent);
        var mapId = Transform(surface).MapID;
        var center = new MapCoordinates(args.Position, mapId);

        // Сети на этой поверхности: под ними не видно.
        var nets = new List<(Vector2 Position, float Radius)>();
        var netQuery = EntityQueryEnumerator<DHCamoNetComponent, TransformComponent>();
        while (netQuery.MoveNext(out _, out var net, out var netXform))
        {
            if (netXform.MapID == mapId)
                nets.Add((_transform.GetWorldPosition(netXform), net.Radius));
        }

        var enemies = new List<Vector2>();
        var friends = 0;
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(center, ent.Comp.Radius))
        {
            if (mob.Comp.CurrentState == MobState.Dead)
                continue;

            var position = _transform.GetWorldPosition(mob);
            if (nets.Any(n => Vector2.Distance(n.Position, position) <= n.Radius))
                continue;

            if (HasComp<StealthComponent>(mob) && _stealth.GetVisibility(mob) < ScanStealthThreshold)
            {
                continue;
            }

            if (side != null && FactionOf(mob) == side)
            {
                friends++;
                continue;
            }

            enemies.Add(position);
            var blip = Spawn(ScanBlipProto, new MapCoordinates(position, mapId));
            EnsureComp<TimedDespawnComponent>(blip).Lifetime = (float) ent.Comp.BlipLifetime.TotalSeconds;
        }

        var x = (int) MathF.Round(args.Position.X);
        var y = (int) MathF.Round(args.Position.Y);
        var list = string.Join(", ", enemies.Take(8).Select(p => $"({(int) MathF.Round(p.X)}, {(int) MathF.Round(p.Y)})"));
        var text = enemies.Count == 0
            ? Loc.GetString("dh-scan-result-clear", ("x", x), ("y", y), ("friends", friends))
            : Loc.GetString("dh-scan-result", ("x", x), ("y", y), ("count", enemies.Count), ("friends", friends),
                ("list", list));

        if (side is { } faction)
            _assault.AnnounceToFaction(faction, Loc.GetString("dh-scan-sender"), text);
        else
            _popup.PopupEntity(text, ent, args.Actor);

        UpdateScanUi(ent);
    }
}
