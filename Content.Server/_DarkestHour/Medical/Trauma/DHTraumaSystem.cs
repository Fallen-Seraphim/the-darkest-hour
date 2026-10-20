using System.Linq;
using Content.Server._DarkestHour.Medical.Surgery;
using Content.Server.Chat.Systems;
using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Medical.Trauma;

/// <summary>
/// Травмы: распределяет полученный урон по частям тела (по выбранной атакующим зоне или случайно), из этого — переломы,
/// внутреннее кровотечение, осколки, повреждение органов, отрыв конечностей и боль. Раз в секунду применяет последствия:
/// потерю крови, замедление, выпадение предметов из сломанной руки, болевой шок и потерю сознания.
/// </summary>
public sealed partial class DHTraumaSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DetachableOrganSystem _detachable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private DHVitalOrgansSystem _vital = default!;

    [Dependency] private EntityQuery<OrganComponent> _organQuery = default!;

    private static readonly string[] BruteTypes = { "Blunt", "Slash", "Piercing" };
    private static readonly string[] BurnTypes = { "Heat", "Cold", "Shock", "Caustic" };

    private static readonly (string Part, float Weight)[] RandomWeights =
    {
        ("Torso", 35f), ("Head", 10f),
        ("ArmLeft", 10f), ("ArmRight", 10f), ("LegLeft", 10f), ("LegRight", 10f),
        ("HandLeft", 3.75f), ("HandRight", 3.75f), ("FootLeft", 3.75f), ("FootRight", 3.75f),
    };

    private static readonly SoundSpecifier FractureSound = new SoundPathSpecifier("/Audio/Effects/bone_rattle.ogg");
    private static readonly SoundSpecifier SeverSound = new SoundCollectionSpecifier("gib");
    private static readonly EntProtoId ForcedSleep = "StatusEffectForcedSleeping";

    private const float MeleeRange = 1.6f;
    private const float MeleeAccuracy = 0.85f;
    private const float RangedAccuracy = 0.6f;

    public const float PainMild = 30f;
    public const float PainModerate = 60f;
    public const float PainShock = 90f;
    public const float PainUnconscious = 120f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHTraumaComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<DHTraumaComponent, OrganRemovedFromEvent>(OnOrganRemoved);
        SubscribeLocalEvent<DHTraumaComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DHSplintComponent, AfterInteractEvent>(OnSplintAfterInteract);
        SubscribeLocalEvent<DHTraumaComponent, DHSplintDoAfterEvent>(OnSplintDoAfter);
    }

    #region Damage

    private void OnDamageChanged(Entity<DHTraumaComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta is not { } delta)
            return;

        var brute = Positive(delta, BruteTypes);
        var burn = Positive(delta, BurnTypes);

        if (args.DamageIncreased && brute + burn > 0.5f)
            ApplyHit(ent, delta, brute, burn, args.Origin);

        Sync(ent);
    }

    private static float Positive(DamageSpecifier delta, string[] types)
    {
        var sum = 0f;
        foreach (var type in types)
        {
            if (delta.DamageDict.TryGetValue(type, out var value) && value > 0)
                sum += value.Float();
        }

        return sum;
    }

    private static float Get(DamageSpecifier delta, string type)
    {
        return delta.DamageDict.TryGetValue(type, out var value) && value > 0 ? value.Float() : 0f;
    }

    private void ApplyHit(Entity<DHTraumaComponent> ent, DamageSpecifier delta, float brute, float burn, EntityUid? origin)
    {
        var organs = GetOrgans(ent);
        var far = origin is { } o && o != ent.Owner &&
                  !_transform.InRange(Transform(o).Coordinates, Transform(ent).Coordinates, MeleeRange);

        var category = ResolvePresent(PickPart(ent, origin, far), organs);
        if (category == null)
            return;

        var part = GetPart(ent.Comp, category);
        part.Brute += brute;
        part.Burn += burn;

        var blunt = Get(delta, "Blunt");
        var slash = Get(delta, "Slash");
        var piercing = Get(delta, "Piercing");
        var max = MaxOf(ent.Comp, category);
        var partName = PartName(category);

        // Перелом: от тупых ударов, пуль и уже сильно избитой части.
        if (!part.Fracture && brute >= 8f)
        {
            var chance = blunt / 50f + piercing / 120f + (part.Brute > max ? 0.15f : 0f);
            if (_random.Prob(Math.Clamp(chance, 0f, 0.7f)))
            {
                part.Fracture = true;
                part.Splinted = false;
                _audio.PlayPvs(FractureSound, ent);
                _popup.PopupEntity(Loc.GetString("dh-trauma-fracture-self", ("part", partName)), ent, ent, PopupType.LargeCaution);
            }
        }

        // Внутреннее кровотечение: сильные тупые и проникающие удары.
        if (brute >= 12f)
        {
            var chance = blunt * 0.012f + piercing * 0.015f + slash * 0.006f;
            if (_random.Prob(Math.Clamp(chance, 0f, 0.6f)))
                part.InternalBleeding = MathF.Min(2f, part.InternalBleeding + 0.4f);
        }

        // Осколки и пули остаются в теле при попадании издалека.
        if (far && piercing >= 10f && part.Shrapnel < 5 && _random.Prob(0.3f))
            part.Shrapnel++;

        // Органы внутри головы и торса.
        if (brute >= 10f && DHTraumaConstants.OrgansIn.TryGetValue(category, out var inside))
        {
            var chance = piercing * 0.02f + blunt * 0.008f + slash * 0.006f;
            var present = inside.Where(c => organs.ContainsKey(c)).ToList();
            if (present.Count > 0 && _random.Prob(Math.Clamp(chance, 0f, 0.7f)))
                DamageOrgan(ent, _random.Pick(present), brute * 0.6f);
        }

        ent.Comp.PainSpike = MathF.Min(100f, ent.Comp.PainSpike + brute * 0.8f + burn * 0.6f);

        // Отрыв конечности: часть разбита вдвое сверх прочности и удар рубящий или очень тяжёлый.
        if (category != "Torso" && category != "Head" &&
            part.Brute >= max * 2f && (slash >= 15f || blunt >= 25f) && _random.Prob(0.25f) &&
            organs.TryGetValue(category, out var limb))
        {
            Sever(ent, category, limb);
        }
    }

    /// <summary>Куда пришёлся удар: в выбранную атакующим зону (с точностью) или случайно.</summary>
    private string PickPart(Entity<DHTraumaComponent> ent, EntityUid? origin, bool far)
    {
        if (origin is { } attacker && attacker != ent.Owner &&
            TryComp<DHTargetingComponent>(attacker, out var targeting) &&
            _random.Prob(far ? RangedAccuracy : MeleeAccuracy))
        {
            return targeting.Target;
        }

        var total = RandomWeights.Sum(w => w.Weight);
        var roll = _random.NextFloat() * total;
        foreach (var (part, weight) in RandomWeights)
        {
            roll -= weight;
            if (roll <= 0f)
                return part;
        }

        return "Torso";
    }

    /// <summary>Если части тела нет (отрублена), удар приходится на ближайшую родительскую.</summary>
    private static string? ResolvePresent(string category, Dictionary<string, EntityUid> organs)
    {
        var current = category;
        for (var i = 0; i < 4; i++)
        {
            if (organs.ContainsKey(current))
                return current;

            if (!DHTraumaConstants.ParentOf.TryGetValue(current, out var parent))
                return null;

            current = parent;
        }

        return null;
    }

    /// <summary>Урон частей тела не может превышать общий урон тела (лечение уменьшает его пропорционально).</summary>
    private void Sync(Entity<DHTraumaComponent> ent)
    {
        var groups = _damageable.GetDamagePerGroup(ent.Owner);
        var totalBrute = groups.TryGetValue("Brute", out var b) ? b.Float() : 0f;
        var totalBurn = groups.TryGetValue("Burn", out var u) ? u.Float() : 0f;

        var sumBrute = ent.Comp.Parts.Values.Sum(p => p.Brute);
        var sumBurn = ent.Comp.Parts.Values.Sum(p => p.Burn);

        if (sumBrute > totalBrute + 0.01f)
        {
            var k = sumBrute > 0f ? totalBrute / sumBrute : 0f;
            foreach (var part in ent.Comp.Parts.Values)
                part.Brute *= k;
        }

        if (sumBurn > totalBurn + 0.01f)
        {
            var k = sumBurn > 0f ? totalBurn / sumBurn : 0f;
            foreach (var part in ent.Comp.Parts.Values)
                part.Burn *= k;
        }

        foreach (var (category, part) in ent.Comp.Parts.ToList())
        {
            if (part.IsEmpty)
                ent.Comp.Parts.Remove(category);
        }
    }

    private void DamageOrgan(Entity<DHTraumaComponent> ent, string organ, float amount)
    {
        var before = ent.Comp.Organs.TryGetValue(organ, out var b0) ? b0 : 0f;
        var after = MathF.Min(ent.Comp.OrganMaxDamage, before + amount);
        ent.Comp.Organs[organ] = after;

        if (before < ent.Comp.OrganMaxDamage && after >= ent.Comp.OrganMaxDamage)
        {
            _popup.PopupEntity(Loc.GetString("dh-trauma-organ-failing-self"), ent, ent, PopupType.LargeCaution);
            _vital.Recalculate(ent.Owner);
        }
    }

    private void Sever(Entity<DHTraumaComponent> ent, string category, EntityUid limb)
    {
        if (_detachable.Detach(limb) is not { } detached)
            return;

        _audio.PlayPvs(SeverSound, ent);
        _popup.PopupEntity(Loc.GetString("dh-trauma-severed", ("target", ent.Owner), ("part", PartName(category))),
            ent, PopupType.LargeCaution);
        _bloodstream.TryModifyBleedAmount(ent.Owner, 5f);
        ent.Comp.PainSpike = 100f;

        // Отлетает в сторону.
        _transform.SetCoordinates(detached, Transform(ent).Coordinates.Offset(_random.NextVector2(0.6f)));
    }

    private void OnOrganRemoved(Entity<DHTraumaComponent> ent, ref OrganRemovedFromEvent args)
    {
        if (!_organQuery.TryComp(args.Organ, out var organ) || organ.Category is not { } category)
            return;

        // Вместе с органом или конечностью уходят и их повреждения; новый (пересаженный) орган — целый.
        ent.Comp.Organs.Remove(category);
        ent.Comp.Parts.Remove(category);
    }

    #endregion

    #region Update

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHTraumaComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + comp.Interval;

            if (comp.Parts.Count == 0 && comp.Organs.Count == 0 && comp.PainSpike <= 0f && comp.SpeedModifier >= 1f)
            {
                comp.Pain = 0f;
                continue;
            }

            if (_mobState.IsDead(uid))
                continue;

            Tick((uid, comp), now);
        }
    }

    private void Tick(Entity<DHTraumaComponent> ent, TimeSpan now)
    {
        var comp = ent.Comp;

        // Кровопотеря: внутреннее кровотечение и осколки.
        var bloodLoss = 0f;
        foreach (var part in comp.Parts.Values)
        {
            bloodLoss += part.InternalBleeding + part.Shrapnel * 0.05f;
        }

        if (now < comp.BleedingSlowedUntil)
            bloodLoss *= 0.25f;

        if (bloodLoss > 0f)
            _bloodstream.TryModifyBloodLevel(ent.Owner, FixedPoint2.New(-bloodLoss));

        // Боль.
        comp.PainSpike = MathF.Max(0f, comp.PainSpike - 8f);
        var pain = CalculatePain(comp);
        if (now < comp.PainSuppressionUntil)
            pain = MathF.Max(0f, pain - comp.PainSuppression);
        comp.Pain = pain;

        var incapacitated = _mobState.IsIncapacitated(ent);

        // Переломы ног — замедление; рук — выпадают предметы; черепа — головокружение.
        var speed = 1f;
        speed *= SideSpeed(comp, "LegLeft", "FootLeft");
        speed *= SideSpeed(comp, "LegRight", "FootRight");

        if (!incapacitated)
        {
            DropFromBrokenHand(ent, "ArmLeft", "HandLeft", "left");
            DropFromBrokenHand(ent, "ArmRight", "HandRight", "right");

            if (comp.Parts.TryGetValue("Head", out var head) && head.Fracture && _random.Prob(0.05f))
            {
                _jitter.DoJitter(ent, TimeSpan.FromSeconds(3), true);
                _popup.PopupEntity(Loc.GetString("dh-trauma-skull-dizzy"), ent, ent, PopupType.SmallCaution);
            }

            ApplyPainEffects(ent, pain);
        }

        if (pain >= PainModerate)
            speed *= 0.85f;

        if (MathF.Abs(speed - comp.SpeedModifier) > 0.01f)
        {
            comp.SpeedModifier = speed;
            Dirty(ent);
            _movement.RefreshMovementSpeedModifiers(ent.Owner);
        }
    }

    private float CalculatePain(DHTraumaComponent comp)
    {
        var pain = comp.PainSpike;
        foreach (var (category, part) in comp.Parts)
        {
            var max = MaxOf(comp, category);
            pain += MathF.Min(part.Brute * 0.5f + part.Burn * 0.7f, max);
            if (part.Fracture)
                pain += part.Splinted ? 8f : 25f;
            if (part.InternalBleeding > 0f)
                pain += 10f;
            pain += part.Shrapnel * 6f;
        }

        foreach (var damage in comp.Organs.Values)
        {
            pain += damage * 0.3f;
        }

        return pain;
    }

    private static float SideSpeed(DHTraumaComponent comp, string leg, string foot)
    {
        var worst = 1f;
        foreach (var category in new[] { leg, foot })
        {
            if (comp.Parts.TryGetValue(category, out var part) && part.Fracture)
                worst = MathF.Min(worst, part.Splinted ? 0.85f : 0.6f);
        }

        return worst;
    }

    private void DropFromBrokenHand(Entity<DHTraumaComponent> ent, string arm, string hand, string handId)
    {
        var broken = (ent.Comp.Parts.TryGetValue(arm, out var a) && a.Fracture && !a.Splinted) ||
                     (ent.Comp.Parts.TryGetValue(hand, out var h) && h.Fracture && !h.Splinted);

        if (!broken || !_random.Prob(0.04f))
            return;

        if (_hands.TryDrop(ent.Owner, handId))
            _popup.PopupEntity(Loc.GetString("dh-trauma-hand-drop"), ent, ent, PopupType.SmallCaution);
    }

    private void ApplyPainEffects(Entity<DHTraumaComponent> ent, float pain)
    {
        if (pain >= PainUnconscious)
        {
            // Потеря сознания от боли: держится, пока боль не спадёт (обезболивание, лечение).
            if (!_status.HasEffectComp<ForcedSleepingStatusEffectComponent>(ent))
                _popup.PopupEntity(Loc.GetString("dh-trauma-pain-faint"), ent, ent, PopupType.LargeCaution);

            _status.TryUpdateStatusEffectDuration(ent, ForcedSleep, TimeSpan.FromSeconds(3));
            return;
        }

        if (pain >= PainShock)
        {
            if (_random.Prob(0.12f))
            {
                _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(2));
                _jitter.DoJitter(ent, TimeSpan.FromSeconds(2), true);
                _popup.PopupEntity(Loc.GetString("dh-trauma-pain-shock"), ent, ent, PopupType.LargeCaution);
            }

            if (_random.Prob(0.05f))
                _chat.TryEmoteWithChat(ent, "Scream");
            return;
        }

        if (pain >= PainModerate)
        {
            if (_random.Prob(0.08f))
                _popup.PopupEntity(Loc.GetString("dh-trauma-pain-moderate"), ent, ent, PopupType.MediumCaution);
            return;
        }

        if (pain >= PainMild && _random.Prob(0.05f))
            _popup.PopupEntity(Loc.GetString("dh-trauma-pain-mild"), ent, ent, PopupType.Small);
    }

    #endregion

    #region API

    /// <summary>Кратковременная боль (операция без наркоза и т.п.).</summary>
    public void AddPain(EntityUid uid, float amount)
    {
        if (TryComp<DHTraumaComponent>(uid, out var comp))
            comp.PainSpike = MathF.Min(150f, comp.PainSpike + amount);
    }

    public bool HasFracture(EntityUid uid, string category) =>
        TryComp<DHTraumaComponent>(uid, out var comp) && comp.Parts.TryGetValue(category, out var p) && p.Fracture;

    public bool HasInternalBleeding(EntityUid uid, string category) =>
        TryComp<DHTraumaComponent>(uid, out var comp) && comp.Parts.TryGetValue(category, out var p) && p.InternalBleeding > 0f;

    public bool HasShrapnel(EntityUid uid, string category) =>
        TryComp<DHTraumaComponent>(uid, out var comp) && comp.Parts.TryGetValue(category, out var p) && p.Shrapnel > 0;

    public bool HasOrganDamage(EntityUid uid, string category)
    {
        if (!TryComp<DHTraumaComponent>(uid, out var comp) || !DHTraumaConstants.OrgansIn.TryGetValue(category, out var inside))
            return false;

        return inside.Any(o => comp.Organs.TryGetValue(o, out var d) && d > 0f);
    }

    public void FixFracture(EntityUid uid, string category)
    {
        if (TryComp<DHTraumaComponent>(uid, out var comp) && comp.Parts.TryGetValue(category, out var p))
        {
            p.Fracture = false;
            p.Splinted = false;
        }
    }

    public void StopInternalBleeding(EntityUid uid, string category)
    {
        if (TryComp<DHTraumaComponent>(uid, out var comp) && comp.Parts.TryGetValue(category, out var p))
            p.InternalBleeding = 0f;
    }

    public void RemoveShrapnel(EntityUid uid, string category)
    {
        if (TryComp<DHTraumaComponent>(uid, out var comp) && comp.Parts.TryGetValue(category, out var p))
            p.Shrapnel = 0;
    }

    public void RepairOrgans(EntityUid uid, string category)
    {
        if (!TryComp<DHTraumaComponent>(uid, out var comp) || !DHTraumaConstants.OrgansIn.TryGetValue(category, out var inside))
            return;

        foreach (var organ in inside)
        {
            comp.Organs.Remove(organ);
        }

        _vital.Recalculate(uid);
    }

    /// <summary>Органы, отказавшие от повреждений (для <see cref="DHVitalOrgansSystem"/>).</summary>
    public IEnumerable<string> FailingOrgans(EntityUid uid)
    {
        if (!TryComp<DHTraumaComponent>(uid, out var comp))
            yield break;

        foreach (var (organ, damage) in comp.Organs)
        {
            if (damage >= comp.OrganMaxDamage)
                yield return organ;
        }
    }

    /// <summary>Описание травм части тела для окна хирургии.</summary>
    public List<string> DescribePart(EntityUid uid, string category)
    {
        var result = new List<string>();
        if (!TryComp<DHTraumaComponent>(uid, out var comp))
            return result;

        if (comp.Parts.TryGetValue(category, out var part))
        {
            if (part.Brute >= 1f || part.Burn >= 1f)
                result.Add(Loc.GetString("dh-trauma-desc-damage", ("brute", (int) part.Brute), ("burn", (int) part.Burn)));
            if (part.Fracture)
                result.Add(Loc.GetString(part.Splinted ? "dh-trauma-desc-fracture-splinted" : "dh-trauma-desc-fracture"));
            if (part.InternalBleeding > 0f)
                result.Add(Loc.GetString("dh-trauma-desc-internal-bleeding"));
            if (part.Shrapnel > 0)
                result.Add(Loc.GetString("dh-trauma-desc-shrapnel", ("count", part.Shrapnel)));
        }

        if (DHTraumaConstants.OrgansIn.TryGetValue(category, out var inside))
        {
            foreach (var organ in inside)
            {
                var damage = comp.Organs.TryGetValue(organ, out var d) ? d : 0f;
                if (damage <= 0f)
                    continue;

                var organName = Loc.TryGetString($"dh-trauma-organ-{organ}", out var n) ? n : organ;
                result.Add(Loc.GetString(damage >= comp.OrganMaxDamage ? "dh-trauma-desc-organ-failing" : "dh-trauma-desc-organ-damaged",
                    ("organ", organName), ("percent", (int) (damage / comp.OrganMaxDamage * 100f))));
            }
        }

        return result;
    }

    #endregion

    #region Splint and examine

    private void OnSplintAfterInteract(Entity<DHSplintComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !TryComp<DHTraumaComponent>(target, out var trauma))
            return;

        args.Handled = true;
        if (FindSplintTarget(args.User, trauma) == null)
        {
            _popup.PopupEntity(Loc.GetString("dh-trauma-splint-nothing"), target, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new DHSplintDoAfterEvent(),
            target, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnSplintDoAfter(Entity<DHTraumaComponent> ent, ref DHSplintDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } splint)
            return;

        args.Handled = true;
        if (FindSplintTarget(args.User, ent.Comp) is not { } category)
            return;

        ent.Comp.Parts[category].Splinted = true;
        _popup.PopupEntity(Loc.GetString("dh-trauma-splint-done", ("user", args.User), ("target", ent.Owner),
            ("part", PartName(category))), ent, PopupType.Small);
        QueueDel(splint);
    }

    /// <summary>Сломанная конечность без шины: сначала выбранная зона накладывающего, иначе любая.</summary>
    private string? FindSplintTarget(EntityUid user, DHTraumaComponent trauma)
    {
        if (TryComp<DHTargetingComponent>(user, out var targeting) &&
            targeting.Target.Id is not "Torso" and not "Head" &&
            trauma.Parts.TryGetValue(targeting.Target, out var chosen) && chosen.Fracture && !chosen.Splinted)
        {
            return targeting.Target;
        }

        foreach (var (category, part) in trauma.Parts)
        {
            if (category.Id is not "Torso" and not "Head" && part.Fracture && !part.Splinted)
                return category;
        }

        return null;
    }

    private void OnExamined(Entity<DHTraumaComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        foreach (var (category, part) in ent.Comp.Parts)
        {
            var partName = PartName(category);
            if (part.Fracture && category.Id is not "Torso")
            {
                args.PushMarkup(Loc.GetString(part.Splinted ? "dh-trauma-examine-splinted" : "dh-trauma-examine-fracture",
                    ("target", ent.Owner), ("part", partName)));
            }

            if (part.Shrapnel > 0)
                args.PushMarkup(Loc.GetString("dh-trauma-examine-shrapnel", ("target", ent.Owner), ("part", partName)));
        }

        if (ent.Comp.Pain >= PainShock && !_mobState.IsIncapacitated(ent))
            args.PushMarkup(Loc.GetString("dh-trauma-examine-pain", ("target", ent.Owner)));
    }

    #endregion

    private static float MaxOf(DHTraumaComponent comp, string category)
    {
        return comp.PartMax.TryGetValue(category, out var max) ? max : 60f;
    }

    private static DHPartTrauma GetPart(DHTraumaComponent comp, string category)
    {
        if (!comp.Parts.TryGetValue(category, out var part))
        {
            part = new DHPartTrauma();
            comp.Parts[category] = part;
        }

        return part;
    }

    private Dictionary<string, EntityUid> GetOrgans(EntityUid uid)
    {
        var result = new Dictionary<string, EntityUid>();
        if (!_container.TryGetContainer(uid, BodyComponent.ContainerID, out var container))
            return result;

        foreach (var organ in container.ContainedEntities)
        {
            if (_organQuery.TryComp(organ, out var comp) && comp.Category is { } category)
                result.TryAdd(category, organ);
        }

        return result;
    }

    private static string PartName(string category)
    {
        return Loc.TryGetString($"dh-surgery-part-{category}", out var name) ? name.ToLowerInvariant() : category;
    }
}
