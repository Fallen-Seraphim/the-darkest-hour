using System.Numerics;
using Content.Server._DarkestHour.Levels;
using Content.Shared._DarkestHour.Assault;
using Content.Shared._DarkestHour.Levels;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._DarkestHour.Assault;

/// <summary>
/// Палубы челнока (решение автора 2026-11-27, «Мидуэй» как в CMU-14): кабина — главный грид, к нему привязаны
/// <list type="bullet">
/// <item>ходовая крыша — на уровне выше кабины, пока челнок над полем боя (в ангаре и в переходе убрана);</item>
/// <item>нижний ярус — на уровне ниже кабины, только пока челнок стоит на земле: кабина на стойках на первом уровне,
/// ярус с рампой и стойками — на земле.</item>
/// </list>
/// Убранные ярусы ждут на служебной карте. Перед уборкой всё незакреплённое с крыши переносится в кабину к лестнице,
/// а с нижнего яруса остаётся на земле. Рампа опускается только на земле — с пульта в кабине, снаружи под кормой
/// или кнопкой на консоли пилота; при взлёте поднимается сама.
/// </summary>
public sealed partial class DHDropshipSystem
{
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private DHZTransitSystem _zTransit = default!;
    [Dependency] private Content.Shared.Damage.DamageableSystem _damageable = default!;
    [Dependency] private Content.Shared.Stunnable.SharedStunSystem _stun = default!;
    [Dependency] private Content.Shared.Throwing.ThrowingSystem _throwing = default!;

    private static readonly SoundSpecifier RampSound = new SoundPathSpecifier("/Audio/_CMU14/Dropships/Mohawk/omaha_ramp.ogg");
    private static readonly SoundSpecifier GearSound = new SoundPathSpecifier("/Audio/_CMU14/Dropships/Mohawk/nightcustard_motor_whirring.ogg");

    /// <summary>Уровень кабины на земле: стоит на стойках над нижним ярусом.</summary>
    private const int LandedLevel = 1;

    /// <summary>Переход по краю рампы — не чаще (чтобы не скакать между ярусами).</summary>
    private static readonly TimeSpan RampCooldown = TimeSpan.FromSeconds(0.8);

    private static readonly TimeSpan RampInterval = TimeSpan.FromSeconds(0.1);

    private EntityUid? _deckStorage;
    private int _deckSlots;
    private TimeSpan _nextRamp;
    private readonly Dictionary<EntityUid, TimeSpan> _rampCooldown = new();

    #region Загрузка

    /// <summary>Загрузить ярусы к кабине: рядом с картой кабины лежат «*_underside.yml» и «*_roof.yml».</summary>
    private void LoadDecks(Entity<DHDropshipComponent> ship, ResPath cabinMap)
    {
        var stem = cabinMap.ToString();
        if (!stem.EndsWith(".yml"))
            return;

        stem = stem[..^4];
        ship.Comp.Underside = LoadDeck(new ResPath(stem + "_underside.yml"), ship, Name(ship));
        ship.Comp.Roof = LoadDeck(new ResPath(stem + "_roof.yml"), ship, Name(ship));
    }

    private EntityUid? LoadDeck(ResPath path, EntityUid ship, string shipName)
    {
        if (!_resources.ContentFileExists(path))
            return null;

        var storage = DeckStorage();
        var slot = new Vector2(_deckSlots++ * 80f, 0f);
        if (!_loader.TryLoadGrid(Transform(storage).MapID, path, out var grid, offset: slot))
        {
            Log.Error($"Failed to load dropship deck {path}");
            return null;
        }

        _meta.SetEntityName(grid.Value, $"{shipName} — {Name(grid.Value)}");
        // Ярус едет за кабиной телепортом: без физики и столкновений, иначе поверхность или корпус авианосца под ним
        // расталкивают и разворачивают его (рассинхрон яруса с кабиной).
        if (TryComp<PhysicsComponent>(grid.Value, out var body))
        {
            _physics.SetBodyType(grid.Value, Robust.Shared.Physics.BodyType.Static, body: body);
            _physics.SetCanCollide(grid.Value, false, body: body);
        }

        return grid.Value.Owner;
    }

    /// <summary>Служебная карта для убранных ярусов.</summary>
    private EntityUid DeckStorage()
    {
        if (_deckStorage is { } map && Exists(map))
            return map;

        var uid = _mapSystem.CreateMap(out _);
        _meta.SetEntityName(uid, "dropship deck storage");
        _deckStorage = uid;
        return uid;
    }

    #endregion

    #region Ярусы вслед за кабиной

    /// <summary>Поставить ярусы туда, где сейчас кабина: крыша — на уровень выше, нижний ярус — на землю (если стоим).</summary>
    private void SyncDecks(Entity<DHDropshipComponent> ship)
    {
        var comp = ship.Comp;
        if (comp.Roof == null && comp.Underside == null)
            return;

        var xform = Transform(ship);
        var (position, rotation) = _transform.GetWorldPositionRotation(xform);
        var overField = comp.Surface is { } surface && Exists(surface) && comp.Level >= 0 &&
                        xform.MapUid == LevelMap(surface, comp.Level);

        EntityUid? roofMap = null;
        EntityUid? groundMap = null;
        if (overField)
        {
            roofMap = LevelMap(comp.Surface!.Value, comp.Level + 1);
            if (comp.Level == LandedLevel && comp.State is DHDropshipState.Landed or DHDropshipState.Departing)
                groundMap = LevelMap(comp.Surface!.Value, 0);
        }
        else if (comp.Hangar != null && comp.State is DHDropshipState.Docked or DHDropshipState.Departing &&
                 _zLevels.TryGetLevel(xform.MapUid, out var level))
        {
            // В ангаре авианосца (2026-11-27): кабина — в вырезе полётной палубы, нижний ярус — на полу ангара.
            roofMap = _zLevels.LevelMap(level.Stack, level.Level + 1);
            groundMap = _zLevels.LevelMap(level.Stack, level.Level - 1);
        }

        if (comp.Roof is { } roof && Exists(roof))
            PlaceDeck(ship, roof, roofMap, position, rotation, false);
        if (comp.Underside is { } underside && Exists(underside))
            PlaceDeck(ship, underside, groundMap, position, rotation, true);
    }

    private void PlaceDeck(Entity<DHDropshipComponent> ship, EntityUid deck, EntityUid? map, Vector2 position,
        Angle rotation, bool underside)
    {
        var xform = Transform(deck);
        if (map == null)
        {
            if (xform.MapUid == DeckStorage())
                return;

            Evict(ship, deck, underside);
            var slot = new Vector2((deck.Id % 1000) * 80f, 200f);
            _transform.SetCoordinates(deck, xform, new EntityCoordinates(DeckStorage(), slot), Angle.Zero);
            StopDeck(deck);
            return;
        }

        if (xform.MapUid != map)
        {
            _transform.SetCoordinates(deck, xform, new EntityCoordinates(map.Value, position), rotation);
            StopDeck(deck);
            return;
        }

        var (current, currentRotation) = _transform.GetWorldPositionRotation(xform);
        if ((current - position).LengthSquared() > 0.0001f || !currentRotation.EqualsApprox(rotation, 0.0001))
            _transform.SetWorldPositionRotation(deck, position, rotation, xform);
    }

    private void StopDeck(EntityUid deck)
    {
        if (!TryComp<PhysicsComponent>(deck, out var physics))
            return;

        _physics.SetLinearVelocity(deck, Vector2.Zero, body: physics);
        _physics.SetAngularVelocity(deck, 0f, body: physics);
    }

    /// <summary>
    /// Перед уборкой яруса — снять с него людей и вещи: с нижнего яруса они остаются на земле (челнок поднялся),
    /// с крыши — в кабину, к лестнице на крышу (иначе улетели бы вместе с убранной крышей).
    /// </summary>
    private void Evict(Entity<DHDropshipComponent> ship, EntityUid deck, bool underside)
    {
        var loose = new List<EntityUid>();
        var deckMap = Transform(deck).MapID;
        var children = Transform(deck).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (Transform(child).Anchored)
                continue;

            if (HasComp<MobStateComponent>(child) || HasComp<ItemComponent>(child) || HasComp<PullableComponent>(child))
                loose.Add(child);
        }

        if (loose.Count == 0)
            return;

        MapCoordinates? cabinSpot = null;
        if (!underside)
            cabinSpot = CabinLadder(ship) ?? _transform.GetMapCoordinates(ship);

        foreach (var uid in loose)
        {
            if (underside)
            {
                // Остаётся там, где стоял нижний ярус: на земле или на полу ангара.
                var at = _transform.GetMapCoordinates(uid);
                _transform.SetMapCoordinates(uid, new MapCoordinates(at.Position, deckMap));
            }
            else if (cabinSpot is { } spot)
            {
                _transform.SetMapCoordinates(uid, spot);
            }
        }
    }

    /// <summary>Лестница на крышу в кабине.</summary>
    private MapCoordinates? CabinLadder(EntityUid ship)
    {
        var children = Transform(ship).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (HasComp<DHZLadderComponent>(child))
                return _transform.GetMapCoordinates(child);
        }

        return null;
    }

    #endregion

    #region Рампа

    private void OnRampControl(Entity<DHDropshipRampControlComponent> ent, ref Content.Shared.Interaction.InteractHandEvent args)
    {
        if (args.Handled || ShipOfDeckPart(ent) is not { } ship)
            return;

        args.Handled = true;
        SetRamp(ship, !ship.Comp.RampLowered, args.User);
    }

    private void OnRampMessage(Entity<DHDropshipConsoleComponent> ent, ref DHDropshipRampMessage args)
    {
        if (ShipOf(ent) is not { } ship)
            return;

        SetRamp(ship, args.Lower, args.Actor);
        UpdateUi(ent, ship);
    }

    /// <summary>Кабина, к которой относится деталь: сама кабина или её ярус.</summary>
    private Entity<DHDropshipComponent>? ShipOfDeckPart(EntityUid part)
    {
        if (Transform(part).GridUid is not { } grid)
            return null;

        if (TryComp<DHDropshipComponent>(grid, out var direct))
            return (grid, direct);

        var ships = EntityQueryEnumerator<DHDropshipComponent>();
        while (ships.MoveNext(out var uid, out var comp))
        {
            if (comp.Underside == grid || comp.Roof == grid)
                return (uid, comp);
        }

        return null;
    }

    /// <summary>Рядов у рампы «Мидуэя» (от петель к краю).</summary>
    private const int RampStages = 3;

    /// <summary>Шаг рампы: ряд за рядом, как в RuCM.</summary>
    private static readonly TimeSpan RampStepDelay = TimeSpan.FromSeconds(0.7);

    /// <summary>
    /// Опустить или поднять рампу (решение автора 2026-11-27 — «реальное опускание, как в RuCM»): рампа идёт ряд за
    /// рядом, со звуком; опускаясь, бьёт и сбивает тех, кто стоит под ней. Переходы между ярусами — только когда
    /// опущена целиком. <paramref name="force"/> — сразу (взлёт, стыковка).
    /// </summary>
    public void SetRamp(Entity<DHDropshipComponent> ship, bool lower, EntityUid? user, bool force = false)
    {
        var comp = ship.Comp;
        if (lower && (comp.State is not (DHDropshipState.Landed or DHDropshipState.Docked) || !UndersideDeployed(comp)))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("dh-dropship-ramp-not-landed"), user.Value, user.Value);
            return;
        }

        if (force)
        {
            comp.RampMoving = null;
            ApplyRampStage(ship, lower ? RampStages : 0);
            comp.RampLowered = lower;
            return;
        }

        if (comp.RampMoving == lower || comp.RampMoving == null && comp.RampLowered == lower)
            return;

        // Поднимается — по рампе больше не ходят между ярусами.
        comp.RampLowered = false;
        comp.RampMoving = lower;
        comp.RampNextStep = _timing.CurTime;
        _audio.PlayPvs(RampSound, ship);
        if (comp.Underside is { } underside && Exists(underside) && Transform(underside).MapUid != DeckStorage())
            _audio.PlayPvs(RampSound, underside);

        Announce(ship, Loc.GetString(lower ? "dh-dropship-ramp-lowering" : "dh-dropship-ramp-raising"));
    }

    /// <summary>Шаги движущихся рамп.</summary>
    private void UpdateRampMotion(TimeSpan now)
    {
        var ships = EntityQueryEnumerator<DHDropshipComponent>();
        while (ships.MoveNext(out var uid, out var comp))
        {
            if (comp.RampMoving is not { } lower || now < comp.RampNextStep)
                continue;

            comp.RampNextStep = now + RampStepDelay;
            var stage = Math.Clamp(comp.RampStage + (lower ? 1 : -1), 0, RampStages);
            ApplyRampStage((uid, comp), stage);
            if (stage != (lower ? RampStages : 0))
                continue;

            comp.RampMoving = null;
            comp.RampLowered = lower;
            Announce((uid, comp), Loc.GetString(lower ? "dh-dropship-ramp-lowered" : "dh-dropship-ramp-raised"));
        }
    }

    /// <summary>Показать ряды рампы до <paramref name="stage"/>; переборки — с первого ряда.</summary>
    private void ApplyRampStage(Entity<DHDropshipComponent> ship, int stage)
    {
        var comp = ship.Comp;
        var previous = comp.RampStage;
        comp.RampStage = stage;
        foreach (var grid in new[] { (EntityUid?) ship.Owner, comp.Underside })
        {
            if (grid is not { } g || !Exists(g))
                continue;

            var children = Transform(g).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (!TryComp<DHDropshipRampSegmentComponent>(child, out var segment))
                    continue;

                var shown = segment.Stage < stage;
                _appearance.SetData(child, DHDropshipRampVisuals.Lowered, shown);
                if (segment.Solid && TryComp<PhysicsComponent>(child, out var physics))
                    _physics.SetCanCollide(child, shown, body: physics);

                // Опускающийся ряд бьёт стоящих под ним: удар, сбивает с ног, отбрасывает.
                if (shown && segment.Stage >= previous && !segment.Solid && g == comp.Underside)
                    CrushUnderRamp(ship, child);
            }
        }
    }

    private void CrushUnderRamp(EntityUid ship, EntityUid segment)
    {
        var coords = _transform.GetMapCoordinates(segment);
        foreach (var victim in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 0.45f))
        {
            _damageable.TryChangeDamage(victim.Owner,
                new Content.Shared.Damage.DamageSpecifier { DamageDict = { ["Blunt"] = 40 } }, origin: ship);
            _stun.TryKnockdown(victim.Owner, TimeSpan.FromSeconds(5), true);
            var angle = _random.NextFloat() * MathF.Tau;
            _throwing.TryThrow(victim.Owner, new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 3f, 3f, playSound: false);
            _popup.PopupEntity(Loc.GetString("dh-dropship-ramp-crush"), victim.Owner, victim.Owner, PopupType.MediumCaution);
        }
    }

    /// <summary>Переходы по краям опущенной рампы: из кабины к корме — вниз, с нижнего яруса к носу — наверх.</summary>
    private void UpdateRamps(TimeSpan now)
    {
        if (now < _nextRamp)
            return;

        _nextRamp = now + RampInterval;
        var moves = new List<(EntityUid Mob, MapCoordinates To)>();
        var ships = EntityQueryEnumerator<DHDropshipComponent>();
        while (ships.MoveNext(out var shipUid, out var comp))
        {
            if (!comp.RampLowered || comp.Underside is not { } underside || !Exists(underside) ||
                Transform(underside).MapUid is not { } groundMap || groundMap == DeckStorage())
            {
                continue;
            }

            var cabinMap = Transform(shipUid).MapID;
            var lowerMap = Transform(groundMap).MapID;
            CollectRampMoves(shipUid, lowerMap, now, moves);
            CollectRampMoves(underside, cabinMap, now, moves);
        }

        foreach (var (mob, to) in moves)
        {
            _rampCooldown[mob] = now + RampCooldown;
            _zTransit.MoveTo(mob, to);
        }

        if (_rampCooldown.Count > 64)
        {
            foreach (var (uid, until) in new Dictionary<EntityUid, TimeSpan>(_rampCooldown))
            {
                if (now >= until)
                    _rampCooldown.Remove(uid);
            }
        }
    }

    private void CollectRampMoves(EntityUid grid, MapId other, TimeSpan now, List<(EntityUid, MapCoordinates)> moves)
    {
        var rotation = _transform.GetWorldRotation(grid);
        var children = Transform(grid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (!TryComp<DHDropshipRampEntryComponent>(child, out var entry))
                continue;

            // Нос челнока — локальный +Y: наверх идут к носу, вниз — к корме.
            var direction = rotation.RotateVec(entry.Up ? new Vector2(0f, 1f) : new Vector2(0f, -1f));
            var at = _transform.GetMapCoordinates(child);
            foreach (var found in _lookup.GetEntitiesInRange<MobStateComponent>(at, 0.55f, LookupFlags.Dynamic))
            {
                var mob = found.Owner;
                if (_rampCooldown.TryGetValue(mob, out var until) && now < until)
                    continue;

                if (!TryComp<PhysicsComponent>(mob, out var physics) ||
                    Vector2.Dot(_physics.GetMapLinearVelocity(mob, physics), direction) < 0.2f)
                {
                    continue;
                }

                moves.Add((mob, new MapCoordinates(_transform.GetWorldPosition(mob), other)));
            }
        }
    }

    /// <summary>Нижний ярус стоит под кабиной (на земле или на полу ангара), а не убран.</summary>
    private bool UndersideDeployed(DHDropshipComponent comp)
    {
        return comp.Underside is { } underside && Exists(underside) && Transform(underside).MapUid is { } map &&
               map != DeckStorage();
    }

    #endregion

    #region Шасси

    /// <summary>Стойки выпущены (посадка): звук у нижнего яруса и в кабине.</summary>
    private void GearDown(Entity<DHDropshipComponent> ship)
    {
        _audio.PlayGlobal(GearSound, Filter.Empty().AddInGrid(ship), true);
        if (ship.Comp.Underside is { } underside && Exists(underside))
        {
            var children = Transform(underside).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (HasComp<DHDropshipGearComponent>(child))
                {
                    _audio.PlayPvs(GearSound, child);
                    break;
                }
            }
        }

        Announce(ship, Loc.GetString("dh-dropship-gear-down"));
    }

    #endregion
}
