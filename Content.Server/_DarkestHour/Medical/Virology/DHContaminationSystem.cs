using Content.Server._DarkestHour.Medical.Disease;
using Content.Server.Power.EntitySystems;
using Content.Shared._DarkestHour.Medical.Disease;
using Content.Shared._DarkestHour.Medical.Surgery;
using Content.Shared._DarkestHour.Medical.Virology;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._DarkestHour.Medical.Virology;

/// <summary>
/// Стерильность: предметы заражаются от больных (инструменты при операции, мазки), заражают взявшего без перчаток
/// и следующего пациента; автоклав и салфетки снимают заражение; анализатор без вытяжного шкафа выпускает аэрозоль.
/// </summary>
public sealed partial class DHContaminationSystem : EntitySystem
{
    [Dependency] private DHDiseaseSystem _disease = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>Шанс заразить пациента грязным инструментом за шаг операции.</summary>
    private const float DirtyToolChance = 0.5f;

    /// <summary>Аэрозоль анализатора: радиус и множитель заразности.</summary>
    private const float AerosolRange = 2f;
    private const float AerosolFactor = 1.5f;

    private readonly HashSet<Entity<DHDiseaseCarrierComponent>> _nearby = new();
    private readonly HashSet<Entity<DHFumeHoodComponent>> _hoods = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHContaminatedComponent, GotEquippedHandEvent>(OnPickedUp);

        SubscribeLocalEvent<DHAutoclaveComponent, InteractUsingEvent>(OnAutoclaveInteract);
        SubscribeLocalEvent<DHAutoclaveComponent, DHAutoclaveDoAfterEvent>(OnAutoclaveDoAfter);

        SubscribeLocalEvent<DHDisinfectantComponent, AfterInteractEvent>(OnDisinfectantAfterInteract);
        SubscribeLocalEvent<DHDisinfectantComponent, DHDisinfectDoAfterEvent>(OnDisinfectDoAfter);
    }

    #region API

    /// <summary>Болезни, которые передаются через предметы (контакт, кровь).</summary>
    private bool TransfersByItems(ProtoId<DHDiseasePrototype> id)
    {
        return _proto.TryIndex(id, out var proto) &&
               (proto.Spread.Contains(DHSpreadVector.Contact) || proto.Spread.Contains(DHSpreadVector.Blood));
    }

    /// <summary>Заразить предмет болезнями (только передающимися через предметы).</summary>
    public void Contaminate(EntityUid item, IEnumerable<ProtoId<DHDiseasePrototype>> diseases)
    {
        DHContaminatedComponent? comp = null;
        foreach (var id in diseases)
        {
            if (!TransfersByItems(id))
                continue;

            comp ??= EnsureComp<DHContaminatedComponent>(item);
            if (!comp.Diseases.Contains(id))
                comp.Diseases.Add(id);
        }

        if (comp != null)
            Dirty(item, comp);
    }

    public void Sterilize(EntityUid item)
    {
        RemComp<DHContaminatedComponent>(item);
    }

    /// <summary>
    /// Инвазивный шаг операции инструментом: грязный инструмент заражает пациента, а сам пачкается болезнями пациента.
    /// </summary>
    public void OnSurgeryStep(EntityUid patient, EntityUid tool)
    {
        if (TryComp<DHContaminatedComponent>(tool, out var dirty))
        {
            foreach (var id in dirty.Diseases)
            {
                if (_random.Prob(DirtyToolChance))
                    _disease.Infect(patient, id);
            }
        }

        if (TryComp<DHDiseaseCarrierComponent>(patient, out var carrier) && carrier.Diseases.Count > 0)
            Contaminate(tool, carrier.Diseases.Keys);
    }

    /// <summary>
    /// Анализатор закончил работу. Без работающего вытяжного шкафа рядом образец выходит аэрозолем: заражает
    /// стоящих рядом (защищают маски). true — была утечка.
    /// </summary>
    public bool TryAerosolLeak(EntityUid analyzer, IReadOnlyCollection<ProtoId<DHDiseasePrototype>> samples)
    {
        if (samples.Count == 0)
            return false;

        var coords = Transform(analyzer).Coordinates;
        var position = _transform.GetMapCoordinates(analyzer).Position;
        _hoods.Clear();
        _lookup.GetEntitiesInRange(coords, 3f, _hoods);
        foreach (var hood in _hoods)
        {
            if (!_power.IsPowered(hood.Owner))
                continue;

            var distance = (_transform.GetMapCoordinates(hood.Owner).Position - position).Length();
            if (distance <= hood.Comp.Range)
                return false;
        }

        _nearby.Clear();
        _lookup.GetEntitiesInRange(coords, AerosolRange, _nearby);
        foreach (var target in _nearby)
        {
            var protection = _disease.GetProtection(target, DHSpreadVector.Airborne);
            foreach (var id in samples)
            {
                if (!_proto.TryIndex(id, out var proto) || proto.Spread.Count == 0)
                    continue;

                var chance = proto.Infectivity * AerosolFactor * (1f - protection);
                if (_random.Prob(Math.Clamp(chance, 0f, 1f)))
                    _disease.Infect(target, id);
            }
        }

        _popup.PopupEntity(Loc.GetString("dh-analyzer-aerosol"), analyzer, PopupType.MediumCaution);
        return true;
    }

    #endregion

    private void OnPickedUp(Entity<DHContaminatedComponent> ent, ref GotEquippedHandEvent args)
    {
        var protection = _disease.GetProtection(args.User, DHSpreadVector.Contact);
        foreach (var id in ent.Comp.Diseases)
        {
            if (!_proto.TryIndex(id, out var proto))
                continue;

            if (_random.Prob(Math.Clamp(proto.Infectivity * (1f - protection), 0f, 1f)))
                _disease.Infect(args.User, id);
        }
    }

    #region Autoclave

    private void OnAutoclaveInteract(Entity<DHAutoclaveComponent> ent, ref InteractUsingEvent args)
    {
        // Отвёртки, ломы и платы для разборки машины — мимо, если они не грязные.
        if (args.Handled ||
            !HasComp<DHContaminatedComponent>(args.Used) && !HasComp<DHSurgicalToolComponent>(args.Used))
        {
            return;
        }

        args.Handled = true;
        if (!_power.IsPowered(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("dh-autoclave-no-power"), ent, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new DHAutoclaveDoAfterEvent(),
            ent, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
        _popup.PopupEntity(Loc.GetString("dh-autoclave-start", ("item", args.Used)), ent, args.User);
    }

    private void OnAutoclaveDoAfter(Entity<DHAutoclaveComponent> ent, ref DHAutoclaveDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } item)
            return;

        args.Handled = true;
        if (!_power.IsPowered(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("dh-autoclave-no-power"), ent, args.User);
            return;
        }

        Sterilize(item);
        _popup.PopupEntity(Loc.GetString("dh-autoclave-done", ("item", item)), ent, args.User);
    }

    #endregion

    #region Disinfectant

    private void OnDisinfectantAfterInteract(Entity<DHDisinfectantComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target ||
            !HasComp<ItemComponent>(target) && !HasComp<DHContaminatedComponent>(target))
            return;

        args.Handled = true;
        if (ent.Comp.Uses <= 0)
        {
            _popup.PopupEntity(Loc.GetString("dh-disinfectant-empty"), ent, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new DHDisinfectDoAfterEvent(),
            ent, target: target, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnDisinfectDoAfter(Entity<DHDisinfectantComponent> ent, ref DHDisinfectDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || ent.Comp.Uses <= 0)
            return;

        args.Handled = true;
        ent.Comp.Uses--;
        Dirty(ent);
        Sterilize(target);
        _popup.PopupEntity(Loc.GetString("dh-disinfectant-done", ("target", target)), target, args.User);

        if (ent.Comp.Uses <= 0)
            QueueDel(ent);
    }

    #endregion
}
