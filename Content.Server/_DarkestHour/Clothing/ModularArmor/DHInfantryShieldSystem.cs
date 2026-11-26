using System.Numerics;
using Content.Shared._DarkestHour.Clothing.ModularArmor;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Clothing.ModularArmor;

/// <summary>
/// Пехотный щит ЭМБ-5 (план осады п. 28): поле гасит пули до брони (спрашивает модульная броня при попадании пули),
/// падает после пяти попаданий и поднимается через 30 секунд без попаданий; пока лежит — пули в грудь бьют по
/// излучателю, и тот может сгореть насовсем.
/// </summary>
public sealed partial class DHInfantryShieldSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly SoundSpecifier HitSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Hits/energy_meat1.ogg");
    private static readonly SoundSpecifier DownSound = new SoundPathSpecifier("/Audio/_DarkestHour/Effects/shield-down.ogg"); // звуки автора 2026-11-26
    private static readonly SoundSpecifier UpSound = new SoundPathSpecifier("/Audio/_DarkestHour/Effects/shield-up.ogg");
    private static readonly SoundSpecifier BurnSound = new SoundPathSpecifier("/Audio/Effects/sparks4.ogg");

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHInfantryShieldComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<DHInfantryShieldComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<DHInfantryShieldComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DHInfantryShieldComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnEquipped(Entity<DHInfantryShieldComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & SlotFlags.OUTERCLOTHING) == 0)
            return;

        DeleteField(ent);
        ent.Comp.Field = Spawn(ent.Comp.FieldPrototype, new EntityCoordinates(args.EquipTarget, Vector2.Zero));
        SetFieldState(ent, CurrentState(ent.Comp));
    }

    private void OnUnequipped(Entity<DHInfantryShieldComponent> ent, ref GotUnequippedEvent args)
    {
        DeleteField(ent);
    }

    private void OnShutdown(Entity<DHInfantryShieldComponent> ent, ref ComponentShutdown args)
    {
        DeleteField(ent);
    }

    private void DeleteField(Entity<DHInfantryShieldComponent> ent)
    {
        if (ent.Comp.Field is { } field && !TerminatingOrDeleted(field))
            QueueDel(field);

        ent.Comp.Field = null;
    }

    private void OnExamined(Entity<DHInfantryShieldComponent> ent, ref ExaminedEvent args)
    {
        var comp = ent.Comp;
        if (comp.Burned)
        {
            args.PushMarkup(Loc.GetString("dh-infantry-shield-examine-burned"));
            return;
        }

        args.PushMarkup(Loc.GetString("dh-infantry-shield-examine",
            ("charges", comp.Charges),
            ("max", comp.MaxCharges),
            ("integrity", (int) MathF.Ceiling(comp.EmitterIntegrity / comp.EmitterMaxIntegrity * 100f))));
    }

    private static DHInfantryShieldState CurrentState(DHInfantryShieldComponent comp)
    {
        if (comp.Burned)
            return DHInfantryShieldState.Broken;

        return comp.Charges > 0 ? DHInfantryShieldState.Up : DHInfantryShieldState.Down;
    }

    private void SetFieldState(Entity<DHInfantryShieldComponent> ent, DHInfantryShieldState state, bool hit = false)
    {
        if (ent.Comp.Field is not { } field || !TryComp<DHInfantryShieldFieldComponent>(field, out var visual))
            return;

        var now = _timing.CurTime;
        if (visual.State != state)
            visual.StateChanged = now;

        visual.State = state;
        if (hit)
            visual.LastHit = now;

        Dirty(field, visual);
    }

    /// <summary>Щит носителя (экзоскелет в слоте верхней одежды).</summary>
    private bool TryGetShield(EntityUid wearer, out Entity<DHInfantryShieldComponent> shield)
    {
        shield = default;
        if (!_inventory.TryGetSlotEntity(wearer, "outerClothing", out var suit) ||
            !TryComp<DHInfantryShieldComponent>(suit, out var comp))
        {
            return false;
        }

        shield = (suit.Value, comp);
        return true;
    }

    /// <summary>
    /// Пуля по носителю: поднятое поле гасит её целиком (<c>true</c>). Лежащее поле не мешает, но пуля в грудь бьёт
    /// по излучателю: <paramref name="physical"/> урона пули, а если нагрудник её держит — малая доля.
    /// </summary>
    public bool TryAbsorbBullet(EntityUid wearer, string part, float physical, bool penetrates)
    {
        if (!TryGetShield(wearer, out var shield) || shield.Comp.Burned)
            return false;

        var comp = shield.Comp;
        var now = _timing.CurTime;
        if (comp.Charges > 0)
        {
            comp.Charges--;
            comp.NextRecharge = now + comp.RechargeTime;
            Dirty(shield);
            _audio.PlayPvs(HitSound, wearer, AudioParams.Default.WithVolume(-4f));

            if (comp.Charges == 0)
            {
                _audio.PlayPvs(DownSound, wearer);
                _popup.PopupEntity(Loc.GetString("dh-infantry-shield-down"), wearer, wearer, PopupType.MediumCaution);
            }

            SetFieldState(shield, CurrentState(comp), hit: true);
            return true;
        }

        // Поле лежит: попадания продлевают перезарядку, пули в грудь — по излучателю.
        comp.NextRecharge = now + comp.RechargeTime;
        if (part == "Torso")
        {
            comp.EmitterIntegrity = MathF.Max(0f, comp.EmitterIntegrity - physical * (penetrates ? 1f : comp.BlockedEmitterWear));
            if (comp.EmitterIntegrity <= 0f)
            {
                comp.Burned = true;
                _audio.PlayPvs(BurnSound, wearer);
                _audio.PlayPvs(DownSound, wearer);
                _popup.PopupEntity(Loc.GetString("dh-infantry-shield-burned"), wearer, wearer, PopupType.LargeCaution);
                SetFieldState(shield, DHInfantryShieldState.Broken);
            }
        }

        Dirty(shield);
        return false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + TimeSpan.FromSeconds(0.5);
        var query = EntityQueryEnumerator<DHInfantryShieldComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Burned || comp.Charges >= comp.MaxCharges || now < comp.NextRecharge)
                continue;

            var wasDown = comp.Charges == 0;
            comp.Charges = comp.MaxCharges;
            Dirty(uid, comp);
            SetFieldState((uid, comp), DHInfantryShieldState.Up);

            if (wasDown && comp.Field is { } field && Transform(field).ParentUid is { Valid: true } wearer)
            {
                _audio.PlayPvs(UpSound, wearer);
                _popup.PopupEntity(Loc.GetString("dh-infantry-shield-up"), wearer, wearer, PopupType.Medium);
            }
        }
    }
}
