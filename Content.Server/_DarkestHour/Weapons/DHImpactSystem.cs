using System.Numerics;
using Content.Server.Decals;
using Content.Shared._DarkestHour.Weapons.Ballistics;
using Content.Shared._DarkestHour.Weapons.Impacts;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Fluids;
using Content.Shared.Forensics.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Weapons;

/// <summary>
/// Пуля попала (вызывается из ProjectileSystem перед стандартным звуком попадания). <see cref="Handled"/> — звук уже
/// проигран, стандартный не нужен.
/// </summary>
[ByRefEvent]
public struct DHProjectileImpactEvent(EntityUid target, DamageSpecifier? damage, Vector2 velocity)
{
    public readonly EntityUid Target = target;
    public readonly DamageSpecifier? Damage = damage;
    public readonly Vector2 Velocity = velocity;
    public bool Handled = false;
}

/// <summary>
/// Звук и кровь попадания пули (план осады п. 15, решения автора 2026-11-20):
/// <list type="bullet">
/// <item>по человеку: броня не пробита — звон пластины, крови нет; пробита (или брони нет) — звук плоти, брызги на
/// месте и, если пуля мощная, позади цели — на полу или на стене/двери за ним;</item>
/// <item>по предмету и постройке — звук материала: бетон, металл, дерево, стекло.</item>
/// </list>
/// </summary>
public sealed class DHImpactSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly AudioParams ImpactParams = AudioParams.Default.WithVolume(-2f).WithVariation(0.08f);

    private static readonly SoundSpecifier FleshSound = new SoundCollectionSpecifier("DHImpactFlesh", ImpactParams);
    private static readonly SoundSpecifier ArmorSound = new SoundCollectionSpecifier("DHImpactArmor", ImpactParams);

    private static readonly Dictionary<DHImpactMaterial, SoundSpecifier> MaterialSounds = new()
    {
        { DHImpactMaterial.Concrete, new SoundCollectionSpecifier("DHImpactConcrete", ImpactParams) },
        { DHImpactMaterial.Metal, new SoundCollectionSpecifier("DHImpactMetal", ImpactParams) },
        { DHImpactMaterial.Wood, new SoundCollectionSpecifier("DHImpactWood", ImpactParams) },
        { DHImpactMaterial.Glass, new SoundCollectionSpecifier("DHImpactGlass", ImpactParams) },
        { DHImpactMaterial.Flesh, FleshSound },
    };

    private static readonly EntProtoId BloodImpactEffect = "DHBloodImpactEffect";

    private static readonly HashSet<ProtoId<DamageTypePrototype>> PhysicalTypes = new() { "Blunt", "Slash", "Piercing" };
    private const int SplatterDecals = 6;

    /// <summary>С какого физического урона пуля даёт брызги и позади цели (навылет).</summary>
    private const float ExitDamage = 30f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHBallisticsComponent, DHProjectileImpactEvent>(OnImpact);
        SubscribeLocalEvent<DHWallBloodComponent, InteractUsingEvent>(OnWallInteractUsing);
        SubscribeLocalEvent<DHWallBloodComponent, DHWallBloodCleanDoAfterEvent>(OnWallCleaned);
    }

    private void OnImpact(Entity<DHBallisticsComponent> ent, ref DHProjectileImpactEvent args)
    {
        var target = args.Target;
        args.Handled = true;

        if (HasComp<MobStateComponent>(target))
        {
            if (ent.Comp.LastOutcome == DHBulletArmorOutcome.Blocked)
            {
                // Броня не пробита: звон пластины, крови нет.
                _audio.PlayPvs(ArmorSound, target);
                return;
            }

            _audio.PlayPvs(FleshSound, target);
            Bleed(target, args);
            return;
        }

        _audio.PlayPvs(MaterialSounds[MaterialOf(target)], target);
    }

    #region Кровь

    private void Bleed(EntityUid target, DHProjectileImpactEvent args)
    {
        if (!TryGetBloodColor(target, out var color))
            return;

        var direction = args.Velocity.LengthSquared() > 0.0001f ? Vector2.Normalize(args.Velocity) : Vector2.UnitY;
        var xform = Transform(target);
        var position = _transform.GetWorldPosition(xform);

        // Облачко брызг на цели, по направлению пули.
        var effect = Spawn(BloodImpactEffect, _transform.GetMapCoordinates(target, xform));
        _transform.SetWorldRotation(effect, direction.ToWorldAngle());
        var tint = EnsureComp<DHBloodTintComponent>(effect);
        tint.Color = color;
        Dirty(effect, tint);

        // Пятно под ногами.
        Splatter(xform, position + direction * 0.3f, color, direction);

        // Навылет: мощная пуля — брызги позади цели (на стене или двери, если она там).
        var physical = PhysicalTotal(args.Damage);
        if (physical < ExitDamage && !_random.Prob(0.25f))
            return;

        var behind = position + direction * (1f + _random.NextFloat() * 0.6f);
        if (TryGetWallAt(xform, behind, out var wall))
            StainWall(wall, color);
        else
            Splatter(xform, behind, color, direction);
    }

    private void Splatter(TransformComponent xform, Vector2 worldPosition, Color color, Vector2 direction)
    {
        if (xform.MapUid is not { } map)
            return;

        var coords = _transform.ToCoordinates(new MapCoordinates(worldPosition, Transform(map).MapID));
        var id = $"DHBloodSplatter{_random.Next(1, SplatterDecals + 1)}";
        _decals.TryAddDecal(id, coords.Offset(new Vector2(-0.5f, -0.5f)), out _, color.WithAlpha(0.85f),
            direction.ToWorldAngle() + Angle.FromDegrees(_random.NextFloat() * 60f - 30f), cleanable: true);
    }

    private bool TryGetWallAt(TransformComponent near, Vector2 worldPosition, out Entity<DHWallBloodComponent> wall)
    {
        wall = default;
        if (near.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var tile = _map.WorldToTile(grid, gridComp, worldPosition);
        foreach (var uid in _map.GetAnchoredEntities(grid, gridComp, tile))
        {
            if (TryComp<DHWallBloodComponent>(uid, out var blood))
            {
                wall = (uid, blood);
                return true;
            }
        }

        return false;
    }

    public void StainWall(Entity<DHWallBloodComponent> wall, Color color)
    {
        if (wall.Comp.Count >= wall.Comp.Max)
            return;

        if (wall.Comp.Pattern < 1)
            wall.Comp.Pattern = _random.Next(1, SplatterDecals + 1);

        wall.Comp.Count++;
        wall.Comp.Color = color;
        Dirty(wall);
    }

    private void OnWallInteractUsing(Entity<DHWallBloodComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Count <= 0 ||
            !HasComp<CleansForensicsComponent>(args.Used) && !HasComp<AbsorbentComponent>(args.Used))
        {
            return;
        }

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(2),
            new DHWallBloodCleanDoAfterEvent(), ent, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnWallCleaned(Entity<DHWallBloodComponent> ent, ref DHWallBloodCleanDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Count = 0;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("dh-wall-blood-cleaned"), ent, args.User);
    }

    private bool TryGetBloodColor(EntityUid uid, out Color color)
    {
        color = default;
        if (!TryComp<BloodstreamComponent>(uid, out var blood) || blood.BloodReferenceSolution.Contents.Count == 0)
            return false;

        var reagent = blood.BloodReferenceSolution.Contents[0].Reagent.Prototype;
        if (!_proto.TryIndex<ReagentPrototype>(reagent, out var proto))
            return false;

        color = proto.SubstanceColor;
        return true;
    }

    private static float PhysicalTotal(DamageSpecifier? damage)
    {
        if (damage == null)
            return 0f;

        var total = 0f;
        foreach (var (type, value) in damage.DamageDict)
        {
            if (PhysicalTypes.Contains(type) && value > 0)
                total += value.Float();
        }

        return total;
    }

    #endregion

    #region Материал

    public DHImpactMaterial MaterialOf(EntityUid target)
    {
        if (TryComp<DHImpactMaterialComponent>(target, out var explicitMaterial))
            return explicitMaterial.Material;

        // По названию прототипа: бетон, кирпич, камень, дерево, стекло.
        var id = MetaData(target).EntityPrototype?.ID ?? string.Empty;
        if (Contains(id, "Wood") || Contains(id, "Plank"))
            return DHImpactMaterial.Wood;
        if (Contains(id, "Window") || Contains(id, "Glass"))
            return DHImpactMaterial.Glass;
        if (Contains(id, "Concrete") || Contains(id, "Brick") || Contains(id, "Rock") || Contains(id, "Stone") ||
            Contains(id, "Sandbag"))
        {
            return DHImpactMaterial.Concrete;
        }

        // По набору модификаторов урона.
        if (TryComp<DamageableComponent>(target, out var damageable) && damageable.DamageModifierSetId is { } set)
        {
            var name = set.Id;
            if (Contains(name, "Glass"))
                return DHImpactMaterial.Glass;
            if (Contains(name, "Wood") || Contains(name, "Card"))
                return DHImpactMaterial.Wood;
            if (Contains(name, "Rock") || Contains(name, "Sandbag"))
                return DHImpactMaterial.Concrete;
            if (Contains(name, "Metal") || Contains(name, "Steel") || Contains(name, "Plasteel") ||
                Contains(name, "Plastitanium") || Contains(name, "Brass") || Contains(name, "Electronic") ||
                Contains(name, "DHBarricade"))
            {
                return DHImpactMaterial.Metal;
            }
        }

        return DHImpactMaterial.Concrete;
    }

    private static bool Contains(string text, string part)
    {
        return text.Contains(part, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
