using System.Diagnostics.CodeAnalysis;
using Content.Shared._DarkestHour.Atmos.Fire;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._DarkestHour.Weapons.Ranged.Flamer;

/// <summary>
/// Общая часть огнемёта: бак, запальник, счётчик горючего, выдача «выстрела» ванильному GunSystem.
/// Сама струя огня считается на сервере (DHFlamerSystem).
/// По образцу SharedRMCFlamerSystem из RMC-14 (MIT).
/// </summary>
public abstract partial class SharedDHFlamerSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] protected IPrototypeManager Proto = default!;
    [Dependency] protected SharedSolutionContainerSystem SolutionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHFlamerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DHFlamerComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<DHFlamerComponent, GetAmmoCountEvent>(OnGetAmmoCount);
        SubscribeLocalEvent<DHFlamerComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<DHFlamerComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<DHFlamerComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<DHFlamerComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<DHFlamerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<DHFlamerComponent, ExaminedEvent>(OnFlamerExamined);

        SubscribeLocalEvent<DHFlamerTankComponent, ExaminedEvent>(OnTankExamined);
    }

    private void OnMapInit(Entity<DHFlamerComponent> ent, ref MapInitEvent args)
    {
        UpdateAppearance(ent);
    }

    private void OnInserted(Entity<DHFlamerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.ContainerId)
            UpdateAppearance(ent);
    }

    private void OnRemoved(Entity<DHFlamerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.ContainerId)
            UpdateAppearance(ent);
    }

    private void OnTakeAmmo(Entity<DHFlamerComponent> ent, ref TakeAmmoEvent args)
    {
        if (args.Shots <= 0 || !ent.Comp.Ignited)
            return;

        if (!TryGetFuel(ent, out var solution, out _) ||
            solution.Value.Comp.Solution.Volume < ent.Comp.CostPer)
        {
            return;
        }

        // Один выстрел за раз: струя сама по себе длинная, её длину и расход считает сервер.
        var shot = SpawnAtPosition(ent.Comp.ShotPrototype, args.Coordinates);
        if (!TryComp(shot, out HitscanAmmoComponent? hitscan))
        {
            Log.Error($"{ent.Comp.ShotPrototype} has no {nameof(HitscanAmmoComponent)}");
            Del(shot);
            return;
        }

        args.Ammo.Add((shot, hitscan));
    }

    private void OnGunShot(Entity<DHFlamerComponent> ent, ref GunShotEvent args)
    {
        // Клиент предсказывает выстрел и спавнит свою копию «выстрела» — убираем её.
        if (!_net.IsClient)
            return;

        foreach (var (uid, _) in args.Ammo)
        {
            if (uid != null && IsClientSide(uid.Value))
                QueueDel(uid.Value);
        }
    }

    private void OnGetAmmoCount(Entity<DHFlamerComponent> ent, ref GetAmmoCountEvent args)
    {
        if (!TryGetTankSolution(ent, out var solution, out _))
            return;

        var sol = solution.Value.Comp.Solution;
        args.Count = (sol.Volume / ent.Comp.CostPer).Int();
        args.Capacity = (sol.MaxVolume / ent.Comp.CostPer).Int();
    }

    private void OnAttemptShoot(Entity<DHFlamerComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled)
            return;

        if (!ent.Comp.Ignited)
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("dh-flamer-not-ignited");
            return;
        }

        if (!TryGetTankSolution(ent, out var solution, out _) ||
            solution.Value.Comp.Solution.Volume < ent.Comp.CostPer)
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("dh-flamer-empty");
            return;
        }

        if (!TryGetFuel(ent, out _, out _))
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("dh-flamer-bad-fuel");
        }
    }

    private void OnGetAltVerbs(Entity<DHFlamerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Ignited ? "dh-flamer-verb-extinguish" : "dh-flamer-verb-ignite"),
            Act = () => ToggleIgniter(ent, user),
            Priority = 10,
        });
    }

    public void ToggleIgniter(Entity<DHFlamerComponent> ent, EntityUid? user)
    {
        ent.Comp.Ignited = !ent.Comp.Ignited;
        Dirty(ent);

        _audio.PlayPredicted(ent.Comp.Ignited ? ent.Comp.IgniteSound : ent.Comp.ExtinguishSound, ent, user);
        _appearance.SetData(ent, DHFlamerVisuals.Ignited, ent.Comp.Ignited);
    }

    private void OnFlamerExamined(Entity<DHFlamerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(DHFlamerComponent)))
        {
            args.PushMarkup(Loc.GetString(ent.Comp.Ignited ? "dh-flamer-examine-ignited" : "dh-flamer-examine-not-ignited"));
            args.PushMarkup(Loc.GetString("dh-flamer-examine-toggle"));

            if (!TryGetTankSolution(ent, out var solution, out _))
            {
                args.PushMarkup(Loc.GetString("dh-flamer-examine-no-tank"));
                return;
            }

            PushFuelExamine(solution.Value.Comp.Solution, ent.Comp.MaxRange, ref args);
        }
    }

    private void OnTankExamined(Entity<DHFlamerTankComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange ||
            !SolutionContainer.TryGetSolution(ent.Owner, ent.Comp.SolutionId, out _, out var solution))
        {
            return;
        }

        using (args.PushGroup(nameof(DHFlamerTankComponent)))
        {
            PushFuelExamine(solution, null, ref args);
        }
    }

    private void PushFuelExamine(Solution solution, int? maxRange, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("dh-flamer-examine-fuel",
            ("volume", solution.Volume.Int()),
            ("max", solution.MaxVolume.Int())));

        if (!TryGetDominantFuel(solution, 0f, out var fuel, out var fraction))
        {
            if (solution.Volume > FixedPoint2.Zero)
                args.PushMarkup(Loc.GetString("dh-flamer-examine-not-flammable"));
            return;
        }

        var name = Proto.TryIndex<ReagentPrototype>(fuel.ID, out var reagent) ? reagent.LocalizedName : fuel.ID;
        var range = maxRange is { } max ? Math.Min(max, fuel.Range) : fuel.Range;
        args.PushMarkup(Loc.GetString("dh-flamer-examine-fuel-type",
            ("name", name),
            ("percent", (int) MathF.Round(fraction * 100)),
            ("intensity", fuel.Intensity),
            ("duration", fuel.Duration),
            ("range", range),
            ("spread", fuel.Spread),
            ("structural", fuel.StructuralDamage > 0)));
    }

    protected void UpdateAppearance(Entity<DHFlamerComponent> ent)
    {
        if (!TryComp(ent, out AppearanceComponent? appearance))
            return;

        var loaded = TryGetTankSolution(ent, out var solution, out _);
        var volume = loaded ? solution!.Value.Comp.Solution.Volume.Int() : 0;
        var max = loaded ? solution!.Value.Comp.Solution.MaxVolume.Int() : 0;

        _appearance.SetData(ent, AmmoVisuals.MagLoaded, loaded, appearance);
        _appearance.SetData(ent, AmmoVisuals.HasAmmo, volume > 0, appearance);
        _appearance.SetData(ent, AmmoVisuals.AmmoCount, volume, appearance);
        _appearance.SetData(ent, AmmoVisuals.AmmoMax, max, appearance);
        _appearance.SetData(ent, DHFlamerVisuals.Ignited, ent.Comp.Ignited, appearance);
    }

    /// <summary>Раствор в баке огнемёта (бак вставлен в слот или сам огнемёт — бак).</summary>
    public bool TryGetTankSolution(Entity<DHFlamerComponent> flamer,
        [NotNullWhen(true)] out Entity<SolutionComponent>? solution,
        [NotNullWhen(true)] out EntityUid? tank)
    {
        solution = null;
        tank = null;

        DHFlamerTankComponent? tankComp;
        if (TryComp(flamer, out tankComp))
        {
            tank = flamer.Owner;
        }
        else if (_container.TryGetContainer(flamer, flamer.Comp.ContainerId, out var container) &&
                 container.ContainedEntities.Count > 0 &&
                 TryComp(container.ContainedEntities[0], out tankComp))
        {
            tank = container.ContainedEntities[0];
        }
        else
        {
            return false;
        }

        if (!SolutionContainer.TryGetSolution(tank.Value, tankComp.SolutionId, out solution, out _))
        {
            tank = null;
            return false;
        }

        return true;
    }

    /// <summary>Бак с достаточно горючей смесью и её основной горючий реагент.</summary>
    public bool TryGetFuel(Entity<DHFlamerComponent> flamer,
        [NotNullWhen(true)] out Entity<SolutionComponent>? solution,
        [NotNullWhen(true)] out DHFlammableReagentPrototype? fuel)
    {
        fuel = null;
        if (!TryGetTankSolution(flamer, out solution, out _))
            return false;

        return TryGetDominantFuel(solution.Value.Comp.Solution, flamer.Comp.MinFlammableFraction, out fuel, out _);
    }

    /// <summary>
    /// Самый обильный горючий реагент в растворе. False, если горючих меньше <paramref name="minFraction"/>.
    /// </summary>
    public bool TryGetDominantFuel(Solution solution, float minFraction,
        [NotNullWhen(true)] out DHFlammableReagentPrototype? fuel,
        out float fraction)
    {
        fuel = null;
        fraction = 0f;
        if (solution.Volume <= FixedPoint2.Zero)
            return false;

        var flammable = FixedPoint2.Zero;
        var best = FixedPoint2.Zero;
        foreach (var (reagent, quantity) in solution.Contents)
        {
            if (!Proto.TryIndex<DHFlammableReagentPrototype>(reagent.Prototype, out var proto))
                continue;

            flammable += quantity;
            if (quantity <= best)
                continue;

            best = quantity;
            fuel = proto;
        }

        if (fuel == null)
            return false;

        fraction = (flammable / solution.Volume).Float();
        if (fraction < minFraction)
        {
            fuel = null;
            return false;
        }

        return true;
    }
}
