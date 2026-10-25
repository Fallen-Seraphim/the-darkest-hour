using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._DarkestHour.Shuttles.Rift;
using Content.Shared.Atmos;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Shuttles.Rift;

/// <summary>
/// Корабль в облаках газового гиганта: за бортом — газ слоя с давлением и температурой глубины (пробоина впустит
/// его внутрь, в открытом космосе — баротравма и жар/холод), корпус экранирует радиацию, а глубже рабочего давления
/// корпус трещит и начинает сминаться.
/// </summary>
public sealed partial class DHCelestialSystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private TagSystem _tags = default!;

    /// <summary>Доля радиации, проходящей сквозь корпус корабля к тем, кто на борту.</summary>
    private const float HullShielding = 0.35f;

    /// <summary>Давление, которое корпус держит без последствий, кПа.</summary>
    private const float HullRatedPressure = 1200f;

    /// <summary>Сколько ступеней глубины: газ за бортом пересчитывается при смене ступени, а не каждую секунду.</summary>
    private const int DepthSteps = 12;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly SoundSpecifier CreakSound = new SoundCollectionSpecifier("MetalSlam",
        AudioParams.Default.WithVolume(-2f).WithVariation(0.2f));

    /// <summary>Ступень глубины, для которой грид уже получил газ за бортом.</summary>
    private readonly Dictionary<EntityUid, int> _gridSteps = new();

    private float HullFactor(TransformComponent xform)
    {
        return xform.GridUid != null ? HullShielding : 1f;
    }

    private void UpdateGasLayers()
    {
        var seen = new HashSet<EntityUid>();

        var grids = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (grids.MoveNext(out var uid, out _, out var xform))
        {
            if (!TryGetSystem(xform.MapUid, out var system, out var origin))
                continue;

            var center = _transform.GetWorldPosition(xform);
            if (TryComp<PhysicsComponent>(uid, out var physics))
                center += _transform.GetWorldRotation(xform).RotateVec(physics.LocalCenter);

            var pos = center - origin;
            DHCelestialBody? layerBody = null;
            var depth = 0f;
            foreach (var body in system.Bodies)
            {
                if (DHCelestial.LayerDepth(body, Vector2.Distance(pos, body.Position)) is not { } d)
                    continue;

                layerBody = body;
                depth = d;
                break;
            }

            if (layerBody == null)
                continue;

            seen.Add(uid);
            EnterLayer(uid, layerBody, depth);
        }

        // Вышли из слоёв (поднялись, прыгнули, сели) — газ за бортом снова как у карты.
        var layers = EntityQueryEnumerator<DHGasLayerComponent>();
        var left = new List<EntityUid>();
        while (layers.MoveNext(out var uid, out _))
        {
            if (!seen.Contains(uid))
                left.Add(uid);
        }

        foreach (var uid in left)
        {
            RemComp<DHGasLayerComponent>(uid);
            _gridSteps.Remove(uid);
            _atmos.SetGridOutsideMixture(uid, null);
        }
    }

    private void EnterLayer(EntityUid grid, DHCelestialBody body, float depth)
    {
        var pressure = DHCelestial.LayerPressure(body, depth);
        var temperature = DHCelestial.LayerTemperatureAt(body, depth);

        var layer = EnsureComp<DHGasLayerComponent>(grid);
        if (layer.Color != body.Color || MathF.Abs(layer.Depth - depth) > 0.01f)
        {
            layer.Color = body.Color;
            layer.Depth = depth;
            layer.Pressure = pressure;
            layer.Temperature = temperature;
            Dirty(grid, layer);
        }

        var step = (int) MathF.Round(depth * DepthSteps);
        if (!_gridSteps.TryGetValue(grid, out var old) || old != step)
        {
            _gridSteps[grid] = step;
            var stepDepth = (float) step / DepthSteps;
            _atmos.SetGridOutsideMixture(grid, LayerMixture(body, DHCelestial.LayerPressure(body, stepDepth),
                DHCelestial.LayerTemperatureAt(body, stepDepth)));
        }

        if (pressure > HullRatedPressure)
            StressHull(grid, body, pressure);
    }

    /// <summary>Газ слоя на тайл: моли из давления и температуры (pV = nRT), состав — доли слоя.</summary>
    private static GasMixture LayerMixture(DHCelestialBody body, float pressure, float temperature)
    {
        var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = temperature };
        var total = 0f;
        foreach (var fraction in body.LayerGases.Values)
        {
            total += fraction;
        }

        if (total <= 0f)
            return mix;

        var moles = pressure * Atmospherics.CellVolume / (Atmospherics.R * temperature);
        foreach (var (gas, fraction) in body.LayerGases)
        {
            mix.AdjustMoles(gas, moles * fraction / total);
        }

        return mix;
    }

    /// <summary>
    /// Глубже рабочего давления корпус трещит (тряска, скрежет у всех на борту), а стены по краям корпуса
    /// сминаются: чем больше перегрузка, тем чаще и сильнее.
    /// </summary>
    private void StressHull(EntityUid grid, DHCelestialBody body, float pressure)
    {
        var overload = Math.Clamp((pressure - HullRatedPressure) / HullRatedPressure, 0f, 2f);
        if (!_random.Prob(0.25f + 0.35f * overload))
            return;

        var walls = new List<EntityUid>();
        var query = EntityQueryEnumerator<TagComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tags, out var xform))
        {
            if (xform.GridUid == grid && xform.Anchored && _tags.HasTag(tags, WallTag))
                walls.Add(uid);
        }

        if (walls.Count > 0)
        {
            var damage = new DamageSpecifier();
            damage.DamageDict["Structural"] = FixedPoint2.New(20f + 60f * overload);
            for (var i = 0; i < 1 + (int) (overload * 2f); i++)
            {
                _damageable.TryChangeDamage(_random.Pick(walls), damage, true);
            }
        }

        var mobs = EntityQueryEnumerator<Content.Shared.Mobs.Components.MobStateComponent, TransformComponent>();
        while (mobs.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            _recoil.KickCamera(uid, _random.NextVector2(0.4f + overload * 0.6f));
            _audio.PlayEntity(CreakSound, uid, uid);
            _popup.PopupEntity(Loc.GetString("dh-gas-layer-hull-creak", ("body", Loc.GetString(body.Name))), uid, uid,
                PopupType.MediumCaution);
        }
    }
}
