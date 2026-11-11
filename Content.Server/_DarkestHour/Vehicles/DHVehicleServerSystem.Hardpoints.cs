using Content.Shared._DarkestHour.Vehicles;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._DarkestHour.Vehicles;

/// <summary>
/// Модули техники (hardpoints), как в CM-SS13 / RMC: установка предметом по машине, снятие гаечным ключом,
/// ремонт сваркой, доля урона по машине на каждый модуль, броня забирает часть урона на себя, разбитые модули не
/// работают (колёса — машина стоит, орудие — молчит, гранатомёты — нет дыма).
/// </summary>
public sealed partial class DHVehicleServerSystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private DHVehicleSystem _vehicleShared = default!;

    private static readonly ProtoId<ToolQualityPrototype> WrenchQuality = "Anchoring";
    private static readonly ProtoId<ToolQualityPrototype> WeldingQuality = "Welding";

    private void InitializeHardpoints()
    {
        SubscribeLocalEvent<DHVehicleHardpointsComponent, MapInitEvent>(OnHardpointsMapInit);
        SubscribeLocalEvent<DHVehicleHardpointsComponent, InteractUsingEvent>(OnHardpointsInteractUsing);
        SubscribeLocalEvent<DHVehicleHardpointsComponent, DHHardpointInstallDoAfterEvent>(OnInstallDoAfter);
        SubscribeLocalEvent<DHVehicleHardpointsComponent, DHHardpointRemoveDoAfterEvent>(OnRemoveDoAfter);
        SubscribeLocalEvent<DHVehicleHardpointsComponent, DHHardpointRepairDoAfterEvent>(OnVehicleRepairDoAfter);
        SubscribeLocalEvent<DHVehicleHardpointsComponent, GetVerbsEvent<Verb>>(OnHardpointVerbs);
        SubscribeLocalEvent<DHVehicleHardpointsComponent, DamageModifyEvent>(OnVehicleDamageModify);
        SubscribeLocalEvent<DHVehicleHardpointsComponent, DamageChangedEvent>(OnVehicleDamaged);
        SubscribeLocalEvent<DHVehicleHardpointsComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<DHVehicleHardpointsComponent, ExaminedEvent>(OnHardpointsExamined);

        SubscribeLocalEvent<DHHardpointComponent, InteractUsingEvent>(OnModuleInteractUsing);
        SubscribeLocalEvent<DHHardpointComponent, DHHardpointRepairDoAfterEvent>(OnModuleRepairDoAfter);
        SubscribeLocalEvent<DHHardpointComponent, ExaminedEvent>(OnModuleExamined);
    }

    #region Установка и снятие

    private void OnHardpointsMapInit(Entity<DHVehicleHardpointsComponent> ent, ref MapInitEvent args)
    {
        foreach (var (slot, proto) in ent.Comp.Starting)
        {
            if (!_proto.HasIndex(proto))
            {
                Log.Error($"Vehicle {ToPrettyString(ent)}: unknown hardpoint {proto}, skipped");
                continue;
            }

            var module = Spawn(proto, Transform(ent).Coordinates);
            if (!TryComp<DHHardpointComponent>(module, out var hardpoint) || hardpoint.Slot != slot ||
                !Install(ent, (module, hardpoint)))
            {
                Log.Error($"Vehicle {ToPrettyString(ent)}: hardpoint {proto} does not fit slot {slot}");
                QueueDel(module);
            }
        }

        RefreshHardpoints(ent);
    }

    public Entity<DHHardpointComponent>? GetModule(Entity<DHVehicleHardpointsComponent> ent, DHHardpointSlot slot)
    {
        if (!_container.TryGetContainer(ent, DHHardpoints.Container(slot), out var container) ||
            container.ContainedEntities.Count == 0)
        {
            return null;
        }

        var module = container.ContainedEntities[0];
        return TryComp<DHHardpointComponent>(module, out var comp) ? (module, comp) : null;
    }

    private bool CanInstall(Entity<DHVehicleHardpointsComponent> ent, Entity<DHHardpointComponent> module,
        out string reason)
    {
        reason = string.Empty;
        if (!module.Comp.Families.Contains(ent.Comp.Family) || !ent.Comp.Slots.ContainsKey(module.Comp.Slot))
        {
            reason = Loc.GetString("dh-hardpoint-wrong-vehicle");
            return false;
        }

        if (GetModule(ent, module.Comp.Slot) != null)
        {
            reason = Loc.GetString("dh-hardpoint-slot-taken");
            return false;
        }

        return true;
    }

    private bool Install(Entity<DHVehicleHardpointsComponent> ent, Entity<DHHardpointComponent> module)
    {
        if (!CanInstall(ent, module, out _))
            return false;

        var container = _container.EnsureContainer<ContainerSlot>(ent, DHHardpoints.Container(module.Comp.Slot));
        if (!_container.Insert(module.Owner, container))
            return false;

        if (module.Comp.Gun is { } gunProto && TryComp<DHVehicleComponent>(ent, out var vehicle) &&
            _proto.HasIndex(gunProto))
        {
            var slotName = DHHardpoints.SlotName(module.Comp.Slot);
            var weaponContainer = _container.EnsureContainer<ContainerSlot>(ent, WeaponContainerPrefix + slotName);
            var weapon = Spawn(gunProto, Transform(ent).Coordinates);
            var gun = EnsureComp<DHVehicleGunComponent>(weapon);
            gun.Vehicle = ent;
            gun.Slot = slotName;
            gun.Disabled = module.Comp.Broken;
            Dirty(weapon, gun);
            _container.Insert(weapon, weaponContainer);
            vehicle.WeaponEntities[slotName] = weapon;

            if (module.Comp.StoredAmmo is { } stored && TryComp<BasicEntityAmmoProviderComponent>(weapon, out var ammo))
                _gun.UpdateBasicEntityAmmoCount((weapon, ammo), Math.Min(stored, ammo.Capacity ?? stored));
        }

        RefreshHardpoints(ent);
        return true;
    }

    private EntityUid? Uninstall(Entity<DHVehicleHardpointsComponent> ent, DHHardpointSlot slot)
    {
        if (GetModule(ent, slot) is not { } module)
            return null;

        var slotName = DHHardpoints.SlotName(slot);
        if (TryComp<DHVehicleComponent>(ent, out var vehicle) &&
            vehicle.WeaponEntities.TryGetValue(slotName, out var weapon))
        {
            // Кто стрелял из этого орудия — отпускает пульт; патроны остаются в модуле.
            var stations = EntityQueryEnumerator<DHVehicleStationComponent>();
            while (stations.MoveNext(out var stationUid, out var station))
            {
                if (station.Vehicle == ent.Owner && station.Weapon == slotName)
                    _vehicleShared.ReleaseStation((stationUid, station));
            }

            if (TryComp<BasicEntityAmmoProviderComponent>(weapon, out var ammo))
                module.Comp.StoredAmmo = ammo.Count;

            vehicle.WeaponEntities.Remove(slotName);
            QueueDel(weapon);
        }

        var container = _container.GetContainer(ent, DHHardpoints.Container(slot));
        _container.Remove(module.Owner, container);
        RefreshHardpoints(ent);
        return module.Owner;
    }

    private void OnHardpointsInteractUsing(Entity<DHVehicleHardpointsComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<DHHardpointComponent>(args.Used, out var module))
            return;

        args.Handled = true;
        if (!CanInstall(ent, (args.Used, module), out var reason))
        {
            _popup.PopupEntity(reason, ent, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.InstallTime,
            new DHHardpointInstallDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
            DistanceThreshold = 3f,
        });
    }

    private void OnInstallDoAfter(Entity<DHVehicleHardpointsComponent> ent, ref DHHardpointInstallDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used ||
            !TryComp<DHHardpointComponent>(used, out var module))
        {
            return;
        }

        args.Handled = true;
        if (Install(ent, (used, module)))
            _popup.PopupEntity(Loc.GetString("dh-hardpoint-installed", ("module", used)), ent, args.User);
    }

    private void OnRemoveDoAfter(Entity<DHVehicleHardpointsComponent> ent, ref DHHardpointRemoveDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (Uninstall(ent, args.Slot) is { } module)
        {
            _hands.PickupOrDrop(args.User, module);
            _popup.PopupEntity(Loc.GetString("dh-hardpoint-removed", ("module", module)), ent, args.User);
        }
    }

    #endregion

    #region Меню: снять и починить

    private void OnHardpointVerbs(Entity<DHVehicleHardpointsComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using is not { } tool)
            return;

        var user = args.User;
        var wrench = _tool.HasQuality(tool, WrenchQuality.Id);
        var welder = _tool.HasQuality(tool, WeldingQuality.Id);
        if (!wrench && !welder)
            return;

        foreach (var slot in ent.Comp.Slots.Keys)
        {
            if (GetModule(ent, slot) is not { } module)
                continue;

            var slotCopy = slot;
            var name = Name(module);
            if (wrench)
            {
                args.Verbs.Add(new Verb
                {
                    Text = Loc.GetString("dh-hardpoint-verb-remove", ("module", name)),
                    Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
                    Act = () =>
                    {
                        _tool.UseTool(tool, user, ent, (float) ent.Comp.InstallTime.TotalSeconds, WrenchQuality,
                            new DHHardpointRemoveDoAfterEvent(slotCopy));
                    },
                });
            }

            if (welder && module.Comp.Integrity < module.Comp.MaxIntegrity)
            {
                args.Verbs.Add(new Verb
                {
                    Text = Loc.GetString("dh-hardpoint-verb-repair", ("module", name),
                        ("percent", Percent(module.Comp))),
                    Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/zap.svg.192dpi.png")),
                    Act = () =>
                    {
                        _tool.UseTool(tool, user, ent, (float) ent.Comp.RepairTime.TotalSeconds, WeldingQuality,
                            new DHHardpointRepairDoAfterEvent(slotCopy), fuel: 5);
                    },
                });
            }
        }
    }

    private void OnVehicleRepairDoAfter(Entity<DHVehicleHardpointsComponent> ent, ref DHHardpointRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Slot is not { } slot || GetModule(ent, slot) is not { } module)
            return;

        args.Handled = true;
        RepairModule(module);
        RefreshHardpoints(ent);
        args.Repeat = module.Comp.Integrity < module.Comp.MaxIntegrity;
    }

    /// <summary>Снятый модуль чинят сваркой прямо в руках или на полу.</summary>
    private void OnModuleInteractUsing(Entity<DHHardpointComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, WeldingQuality.Id))
            return;

        args.Handled = true;
        if (ent.Comp.Integrity >= ent.Comp.MaxIntegrity)
        {
            _popup.PopupEntity(Loc.GetString("dh-hardpoint-intact"), ent, args.User);
            return;
        }

        _tool.UseTool(args.Used, args.User, ent, 3f, WeldingQuality, new DHHardpointRepairDoAfterEvent(null), fuel: 5);
    }

    private void OnModuleRepairDoAfter(Entity<DHHardpointComponent> ent, ref DHHardpointRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Slot != null)
            return;

        args.Handled = true;
        RepairModule(ent);
        args.Repeat = ent.Comp.Integrity < ent.Comp.MaxIntegrity;
    }

    private void RepairModule(Entity<DHHardpointComponent> module)
    {
        module.Comp.Integrity = Math.Min(module.Comp.MaxIntegrity, module.Comp.Integrity + module.Comp.RepairPerCycle);
        Dirty(module);
    }

    #endregion

    #region Урон и последствия

    private static float PositiveTotal(DamageSpecifier damage)
    {
        var total = 0f;
        foreach (var value in damage.DamageDict.Values)
        {
            if (value > 0)
                total += value.Float();
        }

        return total;
    }

    /// <summary>Броня забирает свою долю урона на себя — корпусу она не достаётся.</summary>
    private void OnVehicleDamageModify(Entity<DHVehicleHardpointsComponent> ent, ref DamageModifyEvent args)
    {
        if (!ent.Comp.Slots.ContainsKey(DHHardpointSlot.Armor) ||
            GetModule(ent, DHHardpointSlot.Armor) is not { } armor || armor.Comp.Broken || armor.Comp.ArmorAbsorb <= 0f)
        {
            return;
        }

        var total = PositiveTotal(args.Damage);
        if (total <= 0f)
            return;

        var absorbed = MathF.Min(total * armor.Comp.ArmorAbsorb, armor.Comp.Integrity);
        armor.Comp.Integrity = MathF.Max(0f, armor.Comp.Integrity - absorbed);
        Dirty(armor);
        args.Damage *= 1f - absorbed / total;

        if (armor.Comp.Broken)
        {
            _popup.PopupEntity(Loc.GetString("dh-hardpoint-broken", ("module", armor.Owner)), ent, PopupType.MediumCaution);
            RefreshHardpoints(ent);
        }
    }

    /// <summary>
    /// Остальные модули получают свою долю урона по машине. Модуль на нуле разбит и не работает, пока его не заварят.
    /// </summary>
    private void OnVehicleDamaged(Entity<DHVehicleHardpointsComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is not { } delta)
            return;

        var total = PositiveTotal(delta);
        if (total <= 0f)
            return;

        var changed = false;
        foreach (var slot in ent.Comp.Slots.Keys)
        {
            if (GetModule(ent, slot) is not { } module || module.Comp.Broken || module.Comp.ArmorAbsorb > 0f)
                continue;

            var taken = total * module.Comp.DamageShare;
            if (taken <= 0f)
                continue;

            module.Comp.Integrity = Math.Max(0f, module.Comp.Integrity - taken);
            Dirty(module);
            changed = true;

            if (module.Comp.Broken)
                _popup.PopupEntity(Loc.GetString("dh-hardpoint-broken", ("module", module.Owner)), ent,
                    PopupType.MediumCaution);
        }

        if (changed)
            RefreshHardpoints(ent);
    }

    private void OnRefreshSpeed(Entity<DHVehicleHardpointsComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var speed = ent.Comp.CanDrive ? ent.Comp.SpeedMultiplier : 0f;
        args.ModifySpeed(speed, speed);
    }

    /// <summary>Пересчитать последствия модулей: езда, орудия, дым, слои спрайта.</summary>
    public void RefreshHardpoints(Entity<DHVehicleHardpointsComponent> ent)
    {
        var canDrive = false;
        var speed = 1f;
        var smoke = false;

        TryComp<DHVehicleComponent>(ent, out var vehicle);
        foreach (var (slot, layer) in ent.Comp.Slots)
        {
            var module = GetModule(ent, slot);
            var state = string.Empty;
            if (module is { } m)
            {
                state = m.Comp.Broken ? m.Comp.DamagedVehicleState ?? m.Comp.VehicleState ?? string.Empty
                    : m.Comp.VehicleState ?? string.Empty;

                if (slot == DHHardpointSlot.Wheels && !m.Comp.Broken)
                {
                    canDrive = true;
                    speed = m.Comp.SpeedMultiplier;
                }

                if (m.Comp.Smoke && !m.Comp.Broken)
                    smoke = true;
            }

            if (vehicle != null &&
                vehicle.WeaponEntities.TryGetValue(DHHardpoints.SlotName(slot), out var weapon) &&
                TryComp<DHVehicleGunComponent>(weapon, out var gun))
            {
                var disabled = module is not { } gm || gm.Comp.Broken;
                if (gun.Disabled != disabled)
                {
                    gun.Disabled = disabled;
                    Dirty(weapon, gun);
                }
            }

            if (!string.IsNullOrEmpty(layer))
                _appearance.SetData(ent, DHHardpoints.Visual(slot), state);
        }

        // Без места под колёса (старые машины) — ездит как раньше.
        if (!ent.Comp.Slots.ContainsKey(DHHardpointSlot.Wheels))
            canDrive = true;

        if (vehicle != null && ent.Comp.Slots.ContainsKey(DHHardpointSlot.Support))
            vehicle.Smoke = smoke;

        if (ent.Comp.CanDrive != canDrive || Math.Abs(ent.Comp.SpeedMultiplier - speed) > 0.001f)
        {
            ent.Comp.CanDrive = canDrive;
            ent.Comp.SpeedMultiplier = speed;
            Dirty(ent);
            _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        }
    }

    #endregion

    #region Осмотр

    private static int Percent(DHHardpointComponent module)
    {
        return module.MaxIntegrity <= 0f ? 0 : (int) MathF.Round(module.Integrity / module.MaxIntegrity * 100f);
    }

    private void OnHardpointsExamined(Entity<DHVehicleHardpointsComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(DHVehicleHardpointsComponent), -1))
        {
            args.PushMarkup(Loc.GetString("dh-hardpoint-examine-header"));
            foreach (var slot in ent.Comp.Slots.Keys)
            {
                var slotName = Loc.GetString("dh-hardpoint-slot-" + DHHardpoints.SlotName(slot).Replace('_', '-'));
                if (GetModule(ent, slot) is not { } module)
                {
                    args.PushMarkup(Loc.GetString("dh-hardpoint-examine-empty", ("slot", slotName)));
                    continue;
                }

                args.PushMarkup(Loc.GetString("dh-hardpoint-examine-module", ("slot", slotName),
                    ("module", Name(module)), ("percent", Percent(module.Comp)),
                    ("color", PercentColor(Percent(module.Comp)))));
            }
        }
    }

    private void OnModuleExamined(Entity<DHHardpointComponent> ent, ref ExaminedEvent args)
    {
        var percent = Percent(ent.Comp);
        args.PushMarkup(Loc.GetString("dh-hardpoint-examine-integrity", ("percent", percent),
            ("color", PercentColor(percent))));
    }

    private static string PercentColor(int percent)
    {
        return percent switch
        {
            > 75 => "green",
            > 50 => "yellow",
            > 25 => "orange",
            _ => "red",
        };
    }

    #endregion
}
