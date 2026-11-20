using Content.Server._DarkestHour.Assault;
using Content.Shared._DarkestHour.Medical.Implants;
using Content.Shared._DarkestHour.Medical.Trauma;
using Content.Shared._DarkestHour.Overwatch;
using Content.Shared.Actions;
using Content.Shared.Body;
using Content.Shared.Movement.Systems;
using Content.Shared.Overlays;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Physics.Components;

namespace Content.Server._DarkestHour.Medical.Implants;

/// <summary>
/// Сложные импланты (план осады п. 29, решения автора 2026-11-20):
/// <list type="bullet">
/// <item>«Сокол» — глаза-имплант (пересаживаются как обычные глаза): способности «Приближение» и ночное зрение;</item>
/// <item>«Тиски» — кибер-рука (протез, приживляется как конечность): ближний бой ×1,25, разброс оружия ×0,75;</item>
/// <item>«Опора» — кибер-нога (протез): бег ×1,08, без хромоты и переломов;</item>
/// <item>«Тактик» — нейролинк в голове: способность открывает наблюдение стороны (бойцы и тактическая карта) без
/// планшета; приказов отдавать нельзя.</item>
/// </list>
/// Работают — растёт отторжение: «Сокол» — пока включено приближение или ночное зрение, «Тиски» — пока в руках оружие,
/// «Опора» — пока носитель бежит, «Тактик» — пока открыт нейролинк.
/// </summary>
public sealed partial class DHImplantSystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContentEyeSystem _contentEye = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const float FalconZoom = 1.6f;
    private const float ViseMelee = 1.25f;
    private const float ViseSpread = 0.75f;
    private const float SupportSpeed = 1.08f;

    private void InitializeAdvanced()
    {
        SubscribeLocalEvent<DHImplantComponent, OrganGotInsertedEvent>(OnOrganInserted);
        SubscribeLocalEvent<DHImplantComponent, OrganGotRemovedEvent>(OnOrganRemoved);
        SubscribeLocalEvent<DHImplantComponent, DHFalconZoomActionEvent>(OnFalconZoom);
        SubscribeLocalEvent<DHImplantComponent, DHTacticActionEvent>(OnTactic);
    }

    private void OnOrganInserted(Entity<DHImplantComponent> ent, ref OrganGotInsertedEvent args)
    {
        Attach(ent, args.Target);
    }

    private void OnOrganRemoved(Entity<DHImplantComponent> ent, ref OrganGotRemovedEvent args)
    {
        Detach(ent);
    }

    /// <summary>Имплант встал в тело: выдать его способности и бонусы.</summary>
    private void Attach(EntityUid uid, EntityUid host)
    {
        if (!TryComp<DHImplantComponent>(uid, out var implant) || LifeStage(host) < EntityLifeStage.MapInitialized)
            return;

        EnsureComp<DHImplantedComponent>(host);
        implant.Host = host;
        implant.Toggled = false;

        switch (implant.Kind)
        {
            case DHImplantKind.Falcon:
                if (!HasComp<NightVisionComponent>(host))
                {
                    AddComp(host, new NightVisionComponent { Enabled = false, Action = "ActionToggleNightVision" });
                    implant.GrantedNightVision = true;
                }
                break;
            case DHImplantKind.Tactic:
                if (TryComp<DHOverwatchConsoleComponent>(uid, out var console) &&
                    TryComp<DHAssaultFighterComponent>(host, out var fighter))
                {
                    console.Faction = fighter.Faction;
                }
                break;
            case DHImplantKind.Vise:
                RefreshCyber(host, null);
                break;
        }

        if (implant.Action != null)
            _actions.AddAction(host, ref implant.ActionEntity, implant.Action, uid);
    }

    /// <summary>Имплант вынут: забрать способности и бонусы.</summary>
    private void Detach(EntityUid uid)
    {
        if (!TryComp<DHImplantComponent>(uid, out var implant) || implant.Host is not { } host)
            return;

        implant.Host = null;
        if (TerminatingOrDeleted(host))
            return;

        if (implant.ActionEntity is { } action)
        {
            _actions.RemoveAction(host, action);
            implant.ActionEntity = null;
        }

        switch (implant.Kind)
        {
            case DHImplantKind.Falcon:
                if (implant.Toggled)
                    _contentEye.ResetZoom(host);
                if (implant.GrantedNightVision)
                    RemComp<NightVisionComponent>(host);
                implant.GrantedNightVision = false;
                break;
            case DHImplantKind.Tactic:
                _ui.CloseUi(uid, DHOverwatchUiKey.Key);
                break;
            case DHImplantKind.Vise:
                RefreshCyber(host, uid);
                break;
        }

        implant.Toggled = false;
    }

    /// <summary>Бонусы кибер-рук: пересчитать (без <paramref name="except"/> — его как раз вынимают).</summary>
    private void RefreshCyber(EntityUid host, EntityUid? except)
    {
        if (!TryComp<DHTraumaComponent>(host, out var trauma))
            return;

        var vise = false;
        foreach (var implant in Implants(host))
        {
            if (implant.Owner != except && implant.Comp.Kind == DHImplantKind.Vise)
                vise = true;
        }

        var melee = vise ? ViseMelee : 1f;
        var spread = vise ? ViseSpread : 1f;
        if (MathF.Abs(trauma.MeleeModifier - melee) < 0.001f && MathF.Abs(trauma.GunSpreadModifier - spread) < 0.001f)
            return;

        trauma.MeleeModifier = melee;
        trauma.GunSpreadModifier = spread;
        Dirty(host, trauma);

        foreach (var held in _hands.EnumerateHeld(host))
        {
            if (HasComp<GunComponent>(held))
                _gun.RefreshModifiers(held);
        }
    }

    /// <summary>Бег на кибер-ноге «Опора» (травмы спрашивают при расчёте скорости).</summary>
    public float SpeedBonus(EntityUid host, bool canWalk)
    {
        return canWalk && HasImplant(host, DHImplantKind.Support) ? SupportSpeed : 1f;
    }

    private void OnFalconZoom(Entity<DHImplantComponent> ent, ref DHFalconZoomActionEvent args)
    {
        if (args.Handled || ent.Comp.Host is not { } host)
            return;

        args.Handled = true;
        ent.Comp.Toggled = !ent.Comp.Toggled;
        if (ent.Comp.Toggled)
            _contentEye.SetZoom(host, SharedContentEyeSystem.DefaultZoom * FalconZoom, ignoreLimits: true);
        else
            _contentEye.ResetZoom(host);
    }

    private void OnTactic(Entity<DHImplantComponent> ent, ref DHTacticActionEvent args)
    {
        if (args.Handled || ent.Comp.Host is not { } host)
            return;

        args.Handled = true;
        _ui.TryToggleUi(ent.Owner, DHOverwatchUiKey.Key, host);
    }

    /// <summary>Отметить работающие сложные импланты (для отторжения).</summary>
    private void TickAdvanced(EntityUid host)
    {
        foreach (var implant in Implants(host))
        {
            switch (implant.Comp.Kind)
            {
                case DHImplantKind.Falcon:
                    if (implant.Comp.Toggled || TryComp<NightVisionComponent>(host, out var nv) && nv.Enabled)
                        implant.Comp.Active = true;
                    break;
                case DHImplantKind.Vise:
                    foreach (var held in _hands.EnumerateHeld(host))
                    {
                        if (HasComp<GunComponent>(held) || HasComp<MeleeWeaponComponent>(held))
                            implant.Comp.Active = true;
                    }
                    break;
                case DHImplantKind.Support:
                    if (TryComp<PhysicsComponent>(host, out var physics) && physics.LinearVelocity.LengthSquared() > 0.25f)
                        implant.Comp.Active = true;
                    break;
                case DHImplantKind.Tactic:
                    if (_ui.IsUiOpen(implant.Owner, DHOverwatchUiKey.Key))
                        implant.Comp.Active = true;
                    break;
            }
        }
    }
}
