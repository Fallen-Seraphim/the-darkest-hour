using Content.Server.Atmos.EntitySystems;
using Content.Shared._DarkestHour.Aviation;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Shared.Player;

namespace Content.Server._DarkestHour.Aviation;

/// <summary>
/// В воздухе (решение автора 2026-11-12). Высота у штурмовика условная (не Z-уровни): над полем боя он на той же карте,
/// что и земля, поэтому разрывы своих же бомб и пушки, пожары и горячий воздух под ним доставали самолёт и пилота.
/// Пока самолёт в воздухе, он, кресло, фонарь и пилот помечены <see cref="DHAirborneComponent"/>: взрывы их не
/// трогают (ни урона, ни поджога, ни отброса), огонь с земли не цепляется, а пилот дышит воздухом кабины.
/// Сбить самолёт можно только ПВО.
/// </summary>
public sealed partial class DHAircraftSystem
{
    [Dependency] private FlammableSystem _flammable = default!;

    private void InitializeAirborne()
    {
        SubscribeLocalEvent<DHAirborneComponent, IgnitedEvent>(OnAirborneIgnited);
        SubscribeLocalEvent<DHAirborneComponent, AtmosExposedGetAirEvent>(OnAirborneGetAir);
    }

    private static bool IsAirborne(DHAircraftComponent comp)
    {
        return comp.State is DHAircraftState.Airborne or DHAircraftState.Pass or DHAircraftState.Returning;
    }

    /// <summary>Расставить и снять метки «в воздухе» по состоянию самолётов (каждый тик: самолётов единицы).</summary>
    private void UpdateAirborne()
    {
        var query = EntityQueryEnumerator<DHAircraftComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var airborne = IsAirborne(comp);
            SetAirborne(uid, airborne);
            if (comp.Seat is { } seat)
                SetAirborne(seat, airborne);
            if (comp.Canopy is { } canopy)
                SetAirborne(canopy, airborne);

            UpdateCockpitAudio(comp, airborne ? Pilot(comp) : null);
        }

        var pilots = EntityQueryEnumerator<DHAircraftPilotComponent>();
        while (pilots.MoveNext(out var uid, out var pilot))
        {
            SetAirborne(uid, TryComp<DHAircraftComponent>(pilot.Aircraft, out var plane) && IsAirborne(plane));
        }
    }

    /// <summary>Гул в кабине: играет пилоту, пока самолёт в воздухе; сел, вылез или сменился игрок — выключается.</summary>
    private void UpdateCockpitAudio(DHAircraftComponent comp, EntityUid? pilot)
    {
        ICommonSession? listener = null;
        if (pilot is { } p && TryComp<ActorComponent>(p, out var actor))
            listener = actor.PlayerSession;

        if (listener == comp.CockpitListener && (listener == null || comp.CockpitAudio is { } playing && Exists(playing)))
            return;

        comp.CockpitAudio = _audio.Stop(comp.CockpitAudio);
        comp.CockpitListener = listener;
        if (listener == null)
            return;

        comp.CockpitAudio = _audio.PlayGlobal(comp.CockpitSound, Filter.SinglePlayer(listener), false)?.Entity;
    }

    private void SetAirborne(EntityUid uid, bool airborne)
    {
        if (airborne)
            EnsureComp<DHAirborneComponent>(uid);
        else
            RemComp<DHAirborneComponent>(uid);
    }

    /// <summary>Вспыхнуть в воздухе не от чего — гасим сразу (поджог от разрывов и пожаров под самолётом).</summary>
    private void OnAirborneIgnited(Entity<DHAirborneComponent> ent, ref IgnitedEvent args)
    {
        if (HasComp<FlammableComponent>(ent))
            _flammable.Extinguish(ent);
    }

    /// <summary>Кабина герметична: пилот дышит своим воздухом, а не жаром и дымом с земли под самолётом.</summary>
    private void OnAirborneGetAir(Entity<DHAirborneComponent> ent, ref AtmosExposedGetAirEvent args)
    {
        if (args.Handled)
            return;

        var air = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
        air.AdjustMoles(Gas.Oxygen, Atmospherics.OxygenMolesStandard);
        air.AdjustMoles(Gas.Nitrogen, Atmospherics.NitrogenMolesStandard);
        args.Gas = air;
        args.Handled = true;
    }
}
