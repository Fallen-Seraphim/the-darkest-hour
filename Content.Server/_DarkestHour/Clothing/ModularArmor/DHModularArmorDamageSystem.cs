using Content.Server._DarkestHour.Medical.Trauma;
using Content.Shared._DarkestHour.Clothing.ModularArmor;
using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared._DarkestHour.Weapons.Ballistics;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Clothing.ModularArmor;

/// <summary>
/// Модульная броня на сервере: защита той части тела, куда пришёлся удар (часть выбирают травмы до брони), износ и
/// поломка модулей, броня шлема только для головы, автоаптечка.
/// </summary>
public sealed partial class DHModularArmorDamageSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DHModularArmorSystem _armor = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DHTraumaSystem _trauma = default!;

    private static readonly SoundSpecifier BreakSound = new SoundCollectionSpecifier("MetalBreak");
    private static readonly SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");

    private TimeSpan _nextAutoMed;

    private static readonly HashSet<ProtoId<DamageTypePrototype>> PhysicalTypes = new() { "Blunt", "Slash", "Piercing" };
    private static readonly ProtoId<DamageTypePrototype> BluntType = "Blunt";

    /// <summary>Цели, чей урон в этом тике уже посчитан по классам брони (попадание пули).</summary>
    private readonly Dictionary<EntityUid, GameTick> _bulletHandled = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHModularArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnSuitDamageModify);
        SubscribeLocalEvent<DHPartArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnPartArmorDamageModify);
        SubscribeLocalEvent<DHTraumaComponent, DHBulletHitEvent>(OnBulletHit);
    }

    /// <summary>Часть тела этого удара: выбирается один раз (первым, кто спросит) и запоминается травмами.</summary>
    private string HitPart(EntityUid target, EntityUid? origin)
    {
        return _trauma.PickHitPart(target, origin);
    }

    /// <summary>Броня части тела: модуль корпуса или шлем. Нет брони DH вообще — <c>false</c>.</summary>
    private bool TryGetPartArmor(EntityUid target, string part, out int level, out Entity<DHArmorModuleComponent>? module)
    {
        level = 0;
        module = null;
        var any = false;

        if (_inventory.TryGetSlotEntity(target, "outerClothing", out var suit) &&
            TryComp<DHModularArmorComponent>(suit, out var suitComp))
        {
            any = true;
            if (DHModularArmor.SlotFor(part) is { } slot &&
                _armor.GetModule(suit.Value, slot) is { } moduleUid &&
                TryComp<DHArmorModuleComponent>(moduleUid, out var moduleComp) &&
                !moduleComp.Broken)
            {
                level = DHModularArmor.Level(moduleComp.Class);
                module = (moduleUid, moduleComp);
            }
        }

        if (_inventory.TryGetSlotEntity(target, "head", out var helmet) &&
            TryComp<DHPartArmorComponent>(helmet, out var partArmor))
        {
            any = true;
            if (partArmor.Parts.Contains(part))
                level = Math.Max(level, partArmor.ArmorClass);
        }

        return any;
    }

    /// <summary>
    /// Пуля по бойцу в модульной броне или шлеме: класс бронепробития пули против класса брони части тела.
    /// Пробила — полный урон, не пробила — малая доля тупым уроном (заброневая травма), модуль изнашивается.
    /// </summary>
    private void OnBulletHit(Entity<DHTraumaComponent> ent, ref DHBulletHitEvent args)
    {
        if (args.Handled)
            return;

        var part = PeekOrPick(ent, args.Shooter);
        if (!TryGetPartArmor(ent, part, out var level, out var module))
            return;

        var ballistics = args.Ballistics;
        var physical = PhysicalTotal(args.Damage);

        if (level <= 0)
        {
            args.Damage = args.Damage * ballistics.UnarmoredMultiplier;
        }
        else if (ballistics.PenetrationClass >= level)
        {
            args.Damage = args.Damage * ballistics.ArmoredMultiplier;
            Wear(ent, module, physical * DHModularArmor.PenetratedWear);
        }
        else
        {
            args.Damage = ConvertPhysical(args.Damage, DHModularArmor.BlockedFraction, toBlunt: true);
            Wear(ent, module, physical * (1f - DHModularArmor.BlockedFraction));
        }

        args.Handled = true;
        _bulletHandled[ent] = _timing.CurTick;
    }

    private void OnSuitDamageModify(Entity<DHModularArmorComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        var target = args.Owner;
        if (BulletHandledNow(target))
            return;

        var part = PeekOrPick(target, args.Args.Origin);
        if (DHModularArmor.SlotFor(part) is not { } slot ||
            _armor.GetModule(ent, slot) is not { } module ||
            !TryComp<DHArmorModuleComponent>(module, out var comp) ||
            comp.Broken)
        {
            return;
        }

        // Не пуля (холодное оружие, взрыв, осколки): броня смягчает физический урон, огонь/кислота/радиация проходят.
        var before = PhysicalTotal(args.Args.Damage);
        args.Args.Damage = ConvertPhysical(args.Args.Damage, DHModularArmor.NonBulletFraction, toBlunt: false);
        Wear(target, (module, comp), before - PhysicalTotal(args.Args.Damage));
    }

    private void OnPartArmorDamageModify(Entity<DHPartArmorComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (ent.Comp.ArmorClass <= 0 || BulletHandledNow(args.Owner))
            return;

        var part = PeekOrPick(args.Owner, args.Args.Origin);
        if (ent.Comp.Parts.Contains(part))
            args.Args.Damage = ConvertPhysical(args.Args.Damage, DHModularArmor.NonBulletFraction, toBlunt: false);
    }

    private bool BulletHandledNow(EntityUid target)
    {
        return _bulletHandled.TryGetValue(target, out var tick) && tick == _timing.CurTick;
    }

    private static float PhysicalTotal(DamageSpecifier damage)
    {
        var total = 0f;
        foreach (var (type, value) in damage.DamageDict)
        {
            if (PhysicalTypes.Contains(type) && value > FixedPoint2.Zero)
                total += value.Float();
        }

        return total;
    }

    /// <summary>Физический урон (только положительный) ×<paramref name="fraction"/>; при toBlunt весь он — тупой.</summary>
    private static DamageSpecifier ConvertPhysical(DamageSpecifier damage, float fraction, bool toBlunt)
    {
        var result = new DamageSpecifier();
        var blunt = FixedPoint2.Zero;
        foreach (var (type, value) in damage.DamageDict)
        {
            if (!PhysicalTypes.Contains(type) || value <= FixedPoint2.Zero)
            {
                result.DamageDict[type] = result.DamageDict.GetValueOrDefault(type) + value;
                continue;
            }

            if (toBlunt)
                blunt += value * fraction;
            else
                result.DamageDict[type] = result.DamageDict.GetValueOrDefault(type) + value * fraction;
        }

        if (blunt > FixedPoint2.Zero)
            result.DamageDict[BluntType] = result.DamageDict.GetValueOrDefault(BluntType) + blunt;

        return result;
    }

    /// <summary>Износ модуля; разбитый перестаёт защищать.</summary>
    private void Wear(EntityUid wearer, Entity<DHArmorModuleComponent>? module, float amount)
    {
        if (module is not { } m || amount <= 0f || m.Comp.Broken)
            return;

        m.Comp.Integrity = MathF.Max(0f, m.Comp.Integrity - amount);
        Dirty(m);

        if (!m.Comp.Broken)
            return;

        _audio.PlayPvs(BreakSound, wearer);
        _popup.PopupEntity(Loc.GetString("dh-armor-module-broken", ("module", Name(m))), wearer, wearer,
            PopupType.LargeCaution);
    }

    /// <summary>
    /// Шлем и корпус спрашивают часть тела в одном и том же ударе: выбирается один раз за тик.
    /// </summary>
    private string PeekOrPick(EntityUid target, EntityUid? origin)
    {
        if (TryComp<DHTraumaComponent>(target, out var trauma) &&
            trauma.PendingHitPart != null && trauma.PendingHitTick == _timing.CurTick)
        {
            return trauma.PendingHitPart;
        }

        return HitPart(target, origin);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Попадание и урон идут одним вызовом, отметки прошлых тиков не нужны.
        _bulletHandled.Clear();

        var now = _timing.CurTime;
        if (now < _nextAutoMed)
            return;

        _nextAutoMed = now + TimeSpan.FromSeconds(1);

        var query = EntityQueryEnumerator<DHArmorSpecialComponent>();
        while (query.MoveNext(out var module, out var special))
        {
            if (special.Kind != DHArmorSpecialKind.AutoMed || now < special.NextUse)
                continue;

            // модуль в слоте корпуса, корпус надет
            if (!TryGetSuitOf(module, out var suit) || _armor.GetWearer(suit) is not { } wearer ||
                !TryComp<DamageableComponent>(wearer, out var damageable) ||
                _damageable.GetTotalDamage((wearer, damageable)).Float() < special.Threshold)
            {
                continue;
            }

            if (!_solutions.TryGetSolution(module, special.Solution, out var solutionEnt, out var solution) ||
                solution.Volume <= 0)
            {
                continue;
            }

            var dose = _solutions.SplitSolution(solutionEnt.Value, FixedPoint2.New(special.Dose));
            if (!_bloodstream.TryAddToBloodstream(wearer, dose))
                continue;

            special.NextUse = now + special.Cooldown;
            Dirty(module, special);
            _audio.PlayPvs(InjectSound, wearer);
            _popup.PopupEntity(Loc.GetString("dh-armor-automed-injected"), wearer, wearer, PopupType.Medium);
        }
    }

    private bool TryGetSuitOf(EntityUid module, out EntityUid suit)
    {
        suit = default;
        var parent = Transform(module).ParentUid;
        if (!HasComp<DHModularArmorComponent>(parent) || _armor.GetModule(parent, DHArmorSlot.Special) != module)
            return false;

        suit = parent;
        return true;
    }
}
