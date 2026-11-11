using System.Numerics;
using Content.Server.Decals;
using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Medical.Trauma;

/// <summary>
/// Отрыв частей тела и кашель кровью (решение автора 2026-11-11).
/// <list type="bullet">
/// <item>Сильный взрыв рядом изредка отрывает руку или ногу, очень сильный — голову.</item>
/// <item>Тяжёлое оружие по разбитой голове изредка обезглавливает. Без головы не живут, и её не пришить.</item>
/// <item>Оторванная часть сохнет вне холода (<see cref="DHLimbDecayComponent"/>); свежую пришивают на операции.</item>
/// <item>Переломы рёбер, пробитые лёгкие и кровотечение в груди вызывают кашель кровью: брызги остаются на полу,
/// как в CM-SS13.</item>
/// </list>
/// </summary>
public sealed partial class DHTraumaSystem
{
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;

    /// <summary>Урон от взрыва за раз, с которого возможен отрыв конечности.</summary>
    private const float ExplosionSeverLimb = 45f;

    /// <summary>Урон от взрыва за раз, с которого возможен отрыв головы.</summary>
    private const float ExplosionSeverHead = 90f;

    private static readonly string[] SplatterDecals =
    {
        "DHDecalBloodSplatter1", "DHDecalBloodSplatter2", "DHDecalBloodSplatter3",
        "DHDecalBloodSplatter4", "DHDecalBloodSplatter5", "DHDecalBloodSplatter6",
    };

    private static readonly string[] FloorDecals =
    {
        "DHDecalBloodFloor1", "DHDecalBloodFloor2", "DHDecalBloodFloor3", "DHDecalBloodFloor4",
        "DHDecalBloodFloor5", "DHDecalBloodFloor6", "DHDecalBloodFloor7",
    };

    private static readonly Vector2 DecalCenter = new(-0.5f, -0.5f);
    private static readonly ProtoId<DamageTypePrototype> KillDamageType = "Blunt";
    private static readonly TimeSpan CoughCooldown = TimeSpan.FromSeconds(8);

    /// <summary>Идёт смертельный урон после обезглавливания: травмы по нему не считаются.</summary>
    private bool _killing;

    private void InitializeGore()
    {
        SubscribeLocalEvent<DHTraumaComponent, BeforeExplodeEvent>(OnBeforeExplode);
    }

    #region Отрыв

    /// <summary>Взрыв задел тело в этот тик: урон, который придёт следом, — от взрыва.</summary>
    private void OnBeforeExplode(Entity<DHTraumaComponent> ent, ref BeforeExplodeEvent args)
    {
        ent.Comp.ExplosionTick = _timing.CurTick;
    }

    /// <summary>Изредка отрывает конечность или голову сильным взрывом.</summary>
    private void TryExplosionSever(Entity<DHTraumaComponent> ent, float brute)
    {
        if (brute < ExplosionSeverLimb)
            return;

        var organs = GetOrgans(ent);

        if (brute >= ExplosionSeverHead && organs.TryGetValue("Head", out var head) &&
            _random.Prob(Math.Clamp((brute - ExplosionSeverHead) / 300f, 0f, 0.15f)))
        {
            Sever(ent, "Head", head);
            return;
        }

        if (!_random.Prob(Math.Clamp((brute - ExplosionSeverLimb) / 200f, 0f, 0.3f)))
            return;

        var present = new List<string>();
        foreach (var limb in Limbs)
        {
            if (organs.ContainsKey(limb))
                present.Add(limb);
        }

        if (present.Count == 0)
            return;

        var picked = present[_random.Next(present.Count)];
        Sever(ent, picked, organs[picked]);
    }

    /// <summary>Тяжёлый удар по разбитой голове: обезглавливание (редко).</summary>
    private bool ShouldDecapitate(DHPartTrauma part, float max, float slash, float blunt)
    {
        return part.Brute >= max * 2f && (slash >= 25f || blunt >= 35f) && _random.Prob(0.15f);
    }

    /// <summary>После отрыва: кровь вокруг, часть тела начинает сохнуть, без головы — смерть.</summary>
    private void AfterSever(Entity<DHTraumaComponent> ent, string category, EntityUid detached)
    {
        EnsureComp<DHLimbDecayComponent>(detached);
        SplatterBlood(ent, 3, 1.2f, null);

        if (category != "Head")
            return;

        // Без головы не живут (решение автора 2026-11-11): сразу смерть, дефибриллятор не поможет.
        EnsureComp<UnrevivableComponent>(ent);
        if (_thresholds.TryGetThresholdForState(ent, MobState.Dead, out var threshold))
        {
            var missing = threshold.Value - _damageable.GetTotalDamage(ent.Owner) + 1;
            if (missing > FixedPoint2.Zero)
            {
                var kill = new DamageSpecifier(_proto.Index(KillDamageType), missing);
                _killing = true;
                try
                {
                    _damageable.TryChangeDamage(ent.Owner, kill, ignoreResistances: true, interruptsDoAfters: false);
                }
                finally
                {
                    _killing = false;
                }
            }
        }
    }

    #endregion

    #region Кашель кровью

    /// <summary>
    /// Насколько тяжело ранена грудь: сломанные рёбра, пробитые лёгкие, кровотечение в груди, разбитый торс.
    /// С 1,5 — кашель кровью.
    /// </summary>
    private static float ChestSeverity(DHTraumaComponent comp)
    {
        var score = 0f;
        if (comp.Parts.TryGetValue("Torso", out var torso))
        {
            if (torso.Fracture)
                score += torso.Splinted ? 0.5f : 1f;
            if (torso.InternalBleeding > 0f)
                score += 1f;
            if (torso.Brute >= MaxOf(comp, "Torso"))
                score += 0.5f;
        }

        if (comp.Organs.TryGetValue("Lungs", out var lungs) && lungs >= 15f)
            score += lungs >= 40f ? 2f : 1.5f;

        return score;
    }

    private void TickBloodCough(Entity<DHTraumaComponent> ent, TimeSpan now)
    {
        if (now < ent.Comp.NextBloodCough || _mobState.IsDead(ent))
            return;

        var severity = ChestSeverity(ent.Comp);
        if (severity < 1.5f || !_random.Prob(MathF.Min(0.2f, severity * 0.04f)))
            return;

        ent.Comp.NextBloodCough = now + CoughCooldown;

        _chat.TryEmoteWithChat(ent, "Cough", ignoreActionBlocker: true, forceEmote: true);
        _popup.PopupEntity(Loc.GetString("dh-trauma-cough-blood", ("target", ent.Owner)), ent,
            Filter.PvsExcept(ent), true, PopupType.SmallCaution);
        _popup.PopupEntity(Loc.GetString("dh-trauma-cough-blood-self"), ent, ent, PopupType.MediumCaution);

        _bloodstream.TryModifyBloodLevel(ent.Owner, FixedPoint2.New(-2));

        // Брызги — перед собой, туда, куда смотрит.
        var rotation = _transform.GetWorldRotation(ent);
        if (Transform(ent).GridUid is { } gridUid)
            rotation -= _transform.GetWorldRotation(gridUid);
        var forward = rotation.ToWorldVec();
        SplatterBlood(ent, severity >= 2.5f ? 2 : 1, 0.35f, forward * 0.7f);
    }

    #endregion

    /// <summary>Кровавые брызги на полу вокруг тела (декали CM-SS13).</summary>
    private void SplatterBlood(EntityUid uid, int count, float spread, Vector2? shift)
    {
        var coordinates = _transform.GetMoverCoordinates(uid);
        if (Transform(uid).GridUid is not { } grid || coordinates.EntityId != grid)
            return;

        var center = coordinates.Position + (shift ?? Vector2.Zero);
        for (var i = 0; i < count; i++)
        {
            var offset = _random.NextVector2(spread);
            var at = new EntityCoordinates(grid, center + offset + DecalCenter);
            var pool = i == 0 && shift == null ? FloorDecals : SplatterDecals;
            var decal = pool[_random.Next(pool.Length)];
            _decals.TryAddDecal(decal, at, out _, rotation: _random.NextAngle(), cleanable: true);
        }
    }
}
