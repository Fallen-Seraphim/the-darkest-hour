using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._DarkestHour.Medical.Disease;
using Content.Server._DarkestHour.Medical.Trauma;
using Content.Shared._DarkestHour.Medical.Disease;
using Content.Server.Chat.Systems;
using Content.Shared._DarkestHour.Medical.Surgery;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._DarkestHour.Medical.Surgery;

/// <summary>
/// Хирургия: окно операций на пациенте, шаги с инструментами, наркоз, стерильность, извлечение/пересадка органов,
/// ампутация и приживление конечностей. Работает на новом теле апстрима: части тела и органы — сущности в контейнере
/// тела с категорией (<see cref="OrganComponent.Category"/>).
/// </summary>
public sealed partial class DHSurgerySystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DetachableOrganSystem _detachable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private OrganRelationSystem _organRelation = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SleepingSystem _sleeping = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private DHDiseaseSystem _disease = default!;
    [Dependency] private DHTraumaSystem _trauma = default!;

    [Dependency] private EntityQuery<OrganComponent> _organQuery = default!;
    [Dependency] private EntityQuery<ChildOrganComponent> _childQuery = default!;
    [Dependency] private EntityQuery<ParentOrganComponent> _parentQuery = default!;

    private const string ScreamEmote = "Scream";

    /// <summary>Болезнь от нестерильной операции.</summary>
    private static readonly ProtoId<DHDiseasePrototype> WoundInfection = "DHDiseaseWoundInfection";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHSurgeryPatientComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<DHSurgeryPatientComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<DHSurgeryPatientComponent, DHSurgeryStepDoAfterEvent>(OnStepDoAfter);
        SubscribeLocalEvent<DHSurgeryPatientComponent, OrganRemovedFromEvent>(OnPatientOrganRemoved);
        SubscribeLocalEvent<DHSurgeryPatientComponent, OrganInsertedIntoEvent>(OnPatientOrganInserted);

        Subs.BuiEvents<DHSurgeryPatientComponent>(DHSurgeryUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<DHSurgerySelectPartMessage>(OnSelectPart);
            subs.Event<DHSurgeryStartMessage>(OnStart);
            subs.Event<DHSurgeryStepMessage>(OnStepMessage);
            subs.Event<DHSurgeryCancelMessage>(OnCancel);
        });
    }

    #region Interaction

    private void OnInteractUsing(Entity<DHSurgeryPatientComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || args.User == ent.Owner || !_ui.HasUi(ent, DHSurgeryUiKey.Key))
            return;

        // Хирург уже выбрал часть тела и операцию — инструмент в руке делает следующий шаг.
        if (TryComp<DHSurgeonComponent>(args.User, out var surgeon) &&
            surgeon.Patient == ent.Owner &&
            surgeon.Part is { } part &&
            ent.Comp.Parts.TryGetValue(part, out var progress) &&
            progress.Active is { } active &&
            _proto.TryIndex(active, out var surgery) &&
            NextStep(surgery, progress) is { } stepIndex &&
            _proto.TryIndex(surgery.Steps[stepIndex], out var step) &&
            (step.Tools.Count == 0 || GetToolStats(step, args.Used) != null))
        {
            args.Handled = true;
            TryStartStep(args.User, ent, part, args.Used);
            return;
        }

        // Хирургический инструмент или простыня на пациенте — открыть окно операций.
        if (HasComp<DHSurgicalToolComponent>(args.Used) || HasComp<DHSurgicalDrapesComponent>(args.Used))
        {
            args.Handled = true;
            OpenUi(ent, args.User);
        }
    }

    private void OnGetVerbs(Entity<DHSurgeryPatientComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.User == ent.Owner || !_ui.HasUi(ent, DHSurgeryUiKey.Key))
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("dh-surgery-verb"),
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Objects/Specific/Medical/Surgery/scalpel.rsi"), "scalpel"),
            Act = () => OpenUi(ent, user),
            Priority = -2,
        });
    }

    private void OpenUi(EntityUid patient, EntityUid user)
    {
        var surgeon = EnsureComp<DHSurgeonComponent>(user);
        if (surgeon.Patient != patient)
        {
            surgeon.Patient = patient;
            surgeon.Part = null;
        }

        if (!IsLyingDown(patient))
            _popup.PopupEntity(Loc.GetString("dh-surgery-not-lying", ("target", patient)), patient, user);

        _ui.OpenUi(patient, DHSurgeryUiKey.Key, user);
        UpdateUi(patient, user);
    }

    #endregion

    #region UI messages

    private void OnUiOpened(Entity<DHSurgeryPatientComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent, args.Actor);
    }

    private void OnSelectPart(Entity<DHSurgeryPatientComponent> ent, ref DHSurgerySelectPartMessage args)
    {
        var surgeon = EnsureComp<DHSurgeonComponent>(args.Actor);
        surgeon.Patient = ent;
        surgeon.Part = args.Category;
    }

    private void OnStart(Entity<DHSurgeryPatientComponent> ent, ref DHSurgeryStartMessage args)
    {
        if (!_proto.TryIndex<DHSurgeryPrototype>(args.Surgery, out var surgery) ||
            !_proto.HasIndex<OrganCategoryPrototype>(args.Category))
        {
            return;
        }

        var surgeon = EnsureComp<DHSurgeonComponent>(args.Actor);
        surgeon.Patient = ent;
        surgeon.Part = args.Category;

        var organs = GetOrgans(ent);
        var progress = GetProgress(ent.Comp, args.Category);
        if (!IsAvailable(ent, surgery, args.Category, organs, progress))
            return;

        progress.Active = surgery.ID;
        progress.Done.Clear();
        UpdateUi(ent, args.Actor);
    }

    private void OnStepMessage(Entity<DHSurgeryPatientComponent> ent, ref DHSurgeryStepMessage args)
    {
        var surgeon = EnsureComp<DHSurgeonComponent>(args.Actor);
        surgeon.Patient = ent;
        surgeon.Part = args.Category;

        var item = _hands.GetActiveItem(args.Actor);
        TryStartStep(args.Actor, ent, args.Category, item);
    }

    private void OnCancel(Entity<DHSurgeryPatientComponent> ent, ref DHSurgeryCancelMessage args)
    {
        if (!ent.Comp.Parts.TryGetValue(args.Category, out var progress))
            return;

        progress.Active = null;
        progress.Done.Clear();
        if (progress.IsEmpty)
            ent.Comp.Parts.Remove(args.Category);

        UpdateUi(ent, args.Actor);
    }

    #endregion

    #region Steps

    private void TryStartStep(EntityUid user, EntityUid patientUid, ProtoId<OrganCategoryPrototype> category, EntityUid? item)
    {
        if (!TryComp<DHSurgeryPatientComponent>(patientUid, out var patient) ||
            !patient.Parts.TryGetValue(category, out var progress) ||
            progress.Active is not { } active ||
            !_proto.TryIndex(active, out var surgery))
        {
            _popup.PopupEntity(Loc.GetString("dh-surgery-no-active"), patientUid, user);
            return;
        }

        if (!IsLyingDown(patientUid))
        {
            _popup.PopupEntity(Loc.GetString("dh-surgery-not-lying", ("target", patientUid)), patientUid, user);
            return;
        }

        if (NextStep(surgery, progress) is not { } stepIndex ||
            !_proto.TryIndex(surgery.Steps[stepIndex], out var step))
        {
            return;
        }

        if (item == null)
        {
            _popup.PopupEntity(Loc.GetString("dh-surgery-need-tool", ("tool", ToolHint(step))), patientUid, user);
            return;
        }

        float speed;
        if (step.Tools.Count == 0)
        {
            if (!ValidateEffectItem(surgery, step, category, patientUid, item.Value, user))
                return;

            speed = 1f;
        }
        else if (GetToolStats(step, item.Value) is { } stats)
        {
            speed = stats.Speed;
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("dh-surgery-need-tool", ("tool", ToolHint(step))), patientUid, user);
            return;
        }

        var (surfaceMultiplier, _) = GetSurface(patientUid);
        var delay = TimeSpan.FromSeconds(step.Duration * surfaceMultiplier / MathF.Max(0.1f, speed));

        var ev = new DHSurgeryStepDoAfterEvent(category, surgery.ID, stepIndex);
        var args = new DoAfterArgs(EntityManager, user, delay, ev, patientUid, target: patientUid, used: item)
        {
            BreakOnMove = true,
            BreakOnHandChange = true,
            NeedHand = true,
            BreakOnDamage = false,
            DistanceThreshold = 1.5f,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return;

        var partName = PartName(category);
        _popup.PopupEntity(Loc.GetString("dh-surgery-step-begin",
                ("user", user), ("target", patientUid), ("step", Loc.GetString(step.Name)), ("part", partName)),
            patientUid, PopupType.Small);
    }

    private void OnStepDoAfter(Entity<DHSurgeryPatientComponent> ent, ref DHSurgeryStepDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var user = args.User;
        var category = new ProtoId<OrganCategoryPrototype>(args.Category);

        if (!ent.Comp.Parts.TryGetValue(category, out var progress) ||
            progress.Active?.Id != args.Surgery ||
            !_proto.TryIndex<DHSurgeryPrototype>(args.Surgery, out var surgery) ||
            NextStep(surgery, progress) != args.Step ||
            !_proto.TryIndex(surgery.Steps[args.Step], out var step) ||
            args.Used is not { } item)
        {
            return;
        }

        var success = 1f;
        var cauterizes = false;
        if (step.Tools.Count == 0)
        {
            if (!ValidateEffectItem(surgery, step, category, ent, item, user))
                return;
        }
        else if (GetToolStats(step, item) is { } stats)
        {
            success = stats.Success;
            cauterizes = stats.Cauterizes;
        }
        else
        {
            return;
        }

        // Боль: без наркоза пациент кричит и может дёрнуться — шаг срывается.
        var painful = step.Pain > 0f && !IsAnesthetized(ent);
        if (painful)
        {
            if (TryComp<SleepingComponent>(ent, out var sleeping))
                _sleeping.TryWaking((ent.Owner, sleeping), force: true);

            success *= 1f - step.Pain * DHSurgeryConstants.PainFailFactor;
            _trauma.AddPain(ent, step.Pain * 50f);
            _popup.PopupEntity(Loc.GetString("dh-surgery-pain-self"), ent, ent, PopupType.LargeCaution);
            if (_random.Prob(MathF.Min(1f, step.Pain + 0.2f)))
                _chat.TryEmoteWithChat(ent, ScreamEmote, ignoreActionBlocker: true);
        }

        if (!_random.Prob(Math.Clamp(success, 0f, 1f)))
        {
            if (step.FailDamage != null)
                _damageable.TryChangeDamage(ent.Owner, step.FailDamage, interruptsDoAfters: false, origin: user);

            _popup.PopupEntity(Loc.GetString(painful ? "dh-surgery-step-fail-pain" : "dh-surgery-step-fail",
                    ("user", user), ("target", ent.Owner)),
                ent, PopupType.MediumCaution);
            UpdateUi(ent, user);
            return;
        }

        // Особое действие шага.
        if (!ApplyEffect(surgery, step, category, ent, item, user))
        {
            UpdateUi(ent, user);
            return;
        }

        // Действие могло пересоздать запись (ампутация) — берём заново.
        progress = GetProgress(ent.Comp, category);
        if (step.Effect == DHSurgeryEffect.Amputate)
        {
            progress.Active = null;
            progress.Done.Clear();
            progress.State = DHPartState.None;
        }
        else
        {
            progress.State |= step.Add;
            progress.State &= ~step.Remove;
            progress.Done.Add(args.Step);
        }

        if ((step.Bleed > 0f && !cauterizes) || step.Bleed < 0f)
            _bloodstream.TryModifyBleedAmount(ent.Owner, step.Bleed);

        if (step.Invasive)
            RollInfection(ent, user);

        _audio.PlayPvs(step.Sound, ent);

        _popup.PopupEntity(Loc.GetString("dh-surgery-step-done",
                ("user", user), ("target", ent.Owner), ("step", Loc.GetString(step.Name)), ("part", PartName(category))),
            ent, PopupType.Small);

        if (progress.Active != null && NextStep(surgery, progress) == null)
        {
            progress.Active = null;
            progress.Done.Clear();
            _popup.PopupEntity(Loc.GetString("dh-surgery-complete", ("surgery", Loc.GetString(surgery.Name))), ent, user);
        }

        CleanupProgress(ent);
        UpdateUi(ent, user);
    }

    /// <summary>Индекс следующего несделанного и не пропущенного шага; null — операция закончена.</summary>
    private int? NextStep(DHSurgeryPrototype surgery, DHSurgeryPartProgress progress)
    {
        for (var i = 0; i < surgery.Steps.Count; i++)
        {
            if (progress.Done.Contains(i))
                continue;

            if (!_proto.TryIndex(surgery.Steps[i], out var step))
                continue;

            if (step.OnlyIf != DHPartState.None && (progress.State & step.OnlyIf) != step.OnlyIf)
                continue;

            return i;
        }

        return null;
    }

    private bool IsSkipped(DHSurgeryStepPrototype step, DHSurgeryPartProgress progress)
    {
        return step.OnlyIf != DHPartState.None && (progress.State & step.OnlyIf) != step.OnlyIf;
    }

    private (float Speed, float Success, bool Cauterizes)? GetToolStats(DHSurgeryStepPrototype step, EntityUid item)
    {
        TryComp<DHSurgicalToolComponent>(item, out var surgical);
        TryComp<ToolComponent>(item, out var tool);

        foreach (var entry in step.Tools)
        {
            if (entry.Kind is { } kind && surgical != null && surgical.Kind == kind)
                return (surgical.Speed * entry.Speed, surgical.Success * entry.Success, surgical.Cauterizes);
        }

        if (tool == null || !_toggle.IsActivated(item))
            return null;

        foreach (var entry in step.Tools)
        {
            if (entry.Quality is { } quality && _tool.HasQuality(item, quality, tool))
                return (entry.Speed, entry.Success, false);
        }

        return null;
    }

    private string ToolHint(DHSurgeryStepPrototype step)
    {
        return step.ToolHint is { } hint ? Loc.GetString(hint) : Loc.GetString(step.Name);
    }

    #endregion

    #region Effects

    /// <summary>Проверка предмета для шагов без инструмента (вставка органа, приживление).</summary>
    private bool ValidateEffectItem(DHSurgeryPrototype surgery, DHSurgeryStepPrototype step,
        ProtoId<OrganCategoryPrototype> category, EntityUid patient, EntityUid item, EntityUid user)
    {
        switch (step.Effect)
        {
            case DHSurgeryEffect.InsertOrgan:
                if (_organQuery.TryComp(item, out var organ) && organ.Body == null && organ.Category == surgery.Organ)
                    return true;
                break;
            case DHSurgeryEffect.AttachLimb:
                if (FindDetachedRoot(item, category) != null)
                    return true;
                break;
            default:
                return true;
        }

        _popup.PopupEntity(Loc.GetString("dh-surgery-need-tool", ("tool", ToolHint(step))), patient, user);
        return false;
    }

    private bool ApplyEffect(DHSurgeryPrototype surgery, DHSurgeryStepPrototype step,
        ProtoId<OrganCategoryPrototype> category, Entity<DHSurgeryPatientComponent> patient, EntityUid item, EntityUid user)
    {
        switch (step.Effect)
        {
            case DHSurgeryEffect.RemoveOrgan:
            {
                if (surgery.Organ is not { } organCategory ||
                    !TryGetBodyContainer(patient, out var container) ||
                    !GetOrgans(patient).TryGetValue(organCategory, out var organ))
                {
                    return false;
                }

                _organRelation.Orphan(organ);
                _container.Remove(organ, container);
                _hands.PickupOrDrop(user, organ);
                return true;
            }
            case DHSurgeryEffect.InsertOrgan:
            {
                if (!TryGetBodyContainer(patient, out var container) ||
                    !GetOrgans(patient).TryGetValue(category, out var parent))
                {
                    return false;
                }

                if (_childQuery.TryComp(item, out var child) && child.Parent is { } oldParent && Exists(oldParent))
                    _organRelation.Orphan(item);

                if (!_container.Insert(item, container, force: true))
                    return false;

                if (_parentQuery.HasComp(parent) && _childQuery.HasComp(item))
                    _organRelation.Relate(parent, item);

                return true;
            }
            case DHSurgeryEffect.Amputate:
            {
                if (!GetOrgans(patient).TryGetValue(category, out var part) ||
                    !HasComp<DetachableOrganComponent>(part))
                {
                    return false;
                }

                var parentCategory = _childQuery.TryComp(part, out var child) && child.Parent is { } parentUid
                    ? _organQuery.CompOrNull(parentUid)?.Category
                    : null;

                if (_detachable.Detach(part) is not { } detached)
                    return false;

                _hands.PickupOrDrop(user, detached);

                // Культя: родительская часть остаётся открытой, её нужно прижечь («Закрыть рану»).
                if (parentCategory is { } parentCat)
                    GetProgress(patient.Comp, parentCat).State |= DHPartState.Incised | DHPartState.Clamped;

                return true;
            }
            case DHSurgeryEffect.AttachLimb:
            {
                if (!surgery.AttachTo.TryGetValue(category, out var parentCategory) ||
                    !TryGetBodyContainer(patient, out var container) ||
                    !GetOrgans(patient).TryGetValue(parentCategory, out var parent) ||
                    FindDetachedRoot(item, category) is not { } root ||
                    !TryComp<BodyComponent>(item, out var detachedBody) ||
                    detachedBody.Organs == null)
                {
                    return false;
                }

                foreach (var organ in detachedBody.Organs.ContainedEntities.ToList())
                {
                    _container.Insert(organ, container, force: true);
                }

                if (_parentQuery.HasComp(parent) && _childQuery.HasComp(root))
                    _organRelation.Relate(parent, root);

                QueueDel(item);

                // Шов нужно закрепить и прижечь.
                GetProgress(patient.Comp, category).State |= DHPartState.Incised | DHPartState.Clamped;
                return true;
            }
            case DHSurgeryEffect.FixFracture:
                _trauma.FixFracture(patient, category);
                return true;
            case DHSurgeryEffect.StopInternalBleeding:
                _trauma.StopInternalBleeding(patient, category);
                return true;
            case DHSurgeryEffect.RemoveShrapnel:
                _trauma.RemoveShrapnel(patient, category);
                return true;
            case DHSurgeryEffect.RepairOrgans:
                _trauma.RepairOrgans(patient, category);
                return true;
            default:
                return true;
        }
    }

    /// <summary>Отделённая часть тела (DetachedBody) с органом нужной категории, не связанным с родителем.</summary>
    private EntityUid? FindDetachedRoot(EntityUid item, ProtoId<OrganCategoryPrototype> category)
    {
        if (!TryComp<BodyComponent>(item, out var body) || body.Organs == null || HasComp<MobStateComponent>(item))
            return null;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (_organQuery.TryComp(organ, out var organComp) && organComp.Category == category &&
                (!_childQuery.TryComp(organ, out var child) || child.Parent == null))
            {
                return organ;
            }
        }

        return null;
    }

    #endregion

    #region Patient state

    private void RollInfection(EntityUid patient, EntityUid surgeon)
    {
        var risk = GetSurface(patient).InfectionRisk;
        if (!HasSterile(surgeon, "mask", DHSterileSlot.Mask))
            risk += DHSurgeryConstants.NoMaskInfectionRisk;
        if (!HasSterile(surgeon, "gloves", DHSterileSlot.Gloves))
            risk += DHSurgeryConstants.NoGlovesInfectionRisk;

        if (risk > 0f && _random.Prob(MathF.Min(1f, risk)))
            _disease.Infect(patient, WoundInfection);
    }

    private bool HasSterile(EntityUid surgeon, string slot, DHSterileSlot kind)
    {
        return _inventory.TryGetSlotEntity(surgeon, slot, out var item) &&
               TryComp<DHSterileClothingComponent>(item, out var sterile) &&
               sterile.Slot == kind;
    }

    /// <summary>Наркоз: принудительный сон (закись азота, хлоралгидрат), критическое состояние или смерть.</summary>
    public bool IsAnesthetized(EntityUid patient)
    {
        return _mobState.IsIncapacitated(patient) || _status.HasEffectComp<ForcedSleepingStatusEffectComponent>(patient);
    }

    public bool IsLyingDown(EntityUid patient)
    {
        if (TryComp<BuckleComponent>(patient, out var buckle) && buckle.BuckledTo is { } strapUid &&
            TryComp<StrapComponent>(strapUid, out var strap) && strap.Position == StrapPosition.Down)
        {
            return true;
        }

        return _standing.IsDown(patient);
    }

    /// <summary>Множитель времени и риск инфекции поверхности, на которой лежит пациент.</summary>
    private (float TimeMultiplier, float InfectionRisk) GetSurface(EntityUid patient)
    {
        if (TryComp<BuckleComponent>(patient, out var buckle) && buckle.BuckledTo is { } strap)
        {
            return TryComp<DHSurgicalTableComponent>(strap, out var table)
                ? (table.TimeMultiplier, table.InfectionRisk)
                : (DHSurgeryConstants.BedMultiplier, 0f);
        }

        return (DHSurgeryConstants.FloorMultiplier, DHSurgeryConstants.FloorInfectionRisk);
    }

    private bool TryGetBodyContainer(EntityUid patient, [NotNullWhen(true)] out BaseContainer? container)
    {
        container = null;
        return _container.TryGetContainer(patient, BodyComponent.ContainerID, out container);
    }

    /// <summary>Органы тела по категориям.</summary>
    private Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid> GetOrgans(EntityUid patient)
    {
        var result = new Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid>();
        if (!TryGetBodyContainer(patient, out var container))
            return result;

        foreach (var organ in container.ContainedEntities)
        {
            if (_organQuery.TryComp(organ, out var comp) && comp.Category is { } category)
                result.TryAdd(category, organ);
        }

        return result;
    }

    private static DHSurgeryPartProgress GetProgress(DHSurgeryPatientComponent patient, ProtoId<OrganCategoryPrototype> category)
    {
        if (!patient.Parts.TryGetValue(category, out var progress))
        {
            progress = new DHSurgeryPartProgress();
            patient.Parts[category] = progress;
        }

        return progress;
    }

    private bool IsAvailable(EntityUid patient, DHSurgeryPrototype surgery, ProtoId<OrganCategoryPrototype> category,
        Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid> organs, DHSurgeryPartProgress? progress)
    {
        if (!surgery.Parts.Contains(category))
            return false;

        var present = organs.ContainsKey(category);
        if (present != surgery.PartPresent)
            return false;

        if (!present)
            return surgery.AttachTo.TryGetValue(category, out var parent) && organs.ContainsKey(parent);

        var state = progress?.State ?? DHPartState.None;
        if ((state & surgery.Requires) != surgery.Requires || (state & surgery.Forbids) != 0)
            return false;

        if (surgery.Organ is { } organ && organs.ContainsKey(organ) != surgery.OrganPresent)
            return false;

        return surgery.Injury switch
        {
            DHInjuryKind.Fracture => _trauma.HasFracture(patient, category),
            DHInjuryKind.InternalBleeding => _trauma.HasInternalBleeding(patient, category),
            DHInjuryKind.Shrapnel => _trauma.HasShrapnel(patient, category),
            DHInjuryKind.OrganDamage => _trauma.HasOrganDamage(patient, category),
            _ => true,
        };
    }

    private void OnPatientOrganRemoved(Entity<DHSurgeryPatientComponent> ent, ref OrganRemovedFromEvent args)
    {
        CleanupProgress(ent);
    }

    private void OnPatientOrganInserted(Entity<DHSurgeryPatientComponent> ent, ref OrganInsertedIntoEvent args)
    {
        CleanupProgress(ent);
    }

    /// <summary>Убирает записи об отсутствующих частях тела без идущей операции.</summary>
    private void CleanupProgress(Entity<DHSurgeryPatientComponent> ent)
    {
        var organs = GetOrgans(ent);
        foreach (var (category, progress) in ent.Comp.Parts.ToList())
        {
            if (progress.Active == null && (!organs.ContainsKey(category) || progress.IsEmpty))
                ent.Comp.Parts.Remove(category);
        }
    }

    private string PartName(ProtoId<OrganCategoryPrototype> category)
    {
        return Loc.TryGetString($"dh-surgery-part-{category.Id}", out var name) ? name : category.Id;
    }

    #endregion

    #region UI state

    private void UpdateUi(EntityUid patient, EntityUid? viewer)
    {
        if (!_ui.IsUiOpen(patient, DHSurgeryUiKey.Key))
            return;

        var organs = GetOrgans(patient);
        TryComp<DHSurgeryPatientComponent>(patient, out var patientComp);

        var state = new DHSurgeryBuiState
        {
            PatientName = Name(patient),
        };

        var surgeries = _proto.EnumeratePrototypes<DHSurgeryPrototype>().OrderBy(s => s.Order).ToList();
        foreach (var categoryId in DHSurgeryConstants.PartOrder)
        {
            var category = new ProtoId<OrganCategoryPrototype>(categoryId);
            DHSurgeryPartProgress? progress = null;
            patientComp?.Parts.TryGetValue(category, out progress);

            var part = new DHSurgeryPartData
            {
                Category = categoryId,
                Name = PartName(category),
                Present = organs.ContainsKey(category),
                State = progress?.State ?? DHPartState.None,
                Injuries = _trauma.DescribePart(patient, categoryId),
            };

            if (progress?.Active is { } active && _proto.TryIndex(active, out var activeProto))
            {
                part.ActiveId = activeProto.ID;
                part.ActiveName = Loc.GetString(activeProto.Name);
                var next = NextStep(activeProto, progress);
                for (var i = 0; i < activeProto.Steps.Count; i++)
                {
                    if (!_proto.TryIndex(activeProto.Steps[i], out var step))
                        continue;

                    part.ActiveSteps.Add(new DHSurgeryStepData
                    {
                        Name = Loc.GetString(step.Name),
                        ToolHint = step.ToolHint is { } hint ? Loc.GetString(hint) : null,
                        Done = progress.Done.Contains(i),
                        Skipped = !progress.Done.Contains(i) && i != next && IsSkipped(step, progress) && (next == null || i < next),
                    });
                }
            }

            foreach (var surgery in surgeries)
            {
                if (surgery.ID == part.ActiveId || !IsAvailable(patient, surgery, category, organs, progress))
                    continue;

                part.Options.Add(new DHSurgeryOptionData
                {
                    Id = surgery.ID,
                    Name = Loc.GetString(surgery.Name),
                    Description = surgery.Description is { } desc ? Loc.GetString(desc) : null,
                });
            }

            // Отсутствующая часть без вариантов (например, кисть без руки) — не показываем.
            if (!part.Present && part.Options.Count == 0 && part.ActiveId == null)
                continue;

            state.Parts.Add(part);
        }

        if (!IsLyingDown(patient))
            state.Warnings.Add(Loc.GetString("dh-surgery-warning-standing"));
        if (!IsAnesthetized(patient))
            state.Warnings.Add(Loc.GetString("dh-surgery-warning-awake"));

        var surface = GetSurface(patient);
        if (surface.TimeMultiplier >= DHSurgeryConstants.FloorMultiplier)
            state.Warnings.Add(Loc.GetString("dh-surgery-warning-floor"));
        else if (surface.TimeMultiplier > 1f)
            state.Warnings.Add(Loc.GetString("dh-surgery-warning-bed"));

        if (viewer is { } surgeon)
        {
            if (!HasSterile(surgeon, "mask", DHSterileSlot.Mask))
                state.Warnings.Add(Loc.GetString("dh-surgery-warning-no-mask"));
            if (!HasSterile(surgeon, "gloves", DHSterileSlot.Gloves))
                state.Warnings.Add(Loc.GetString("dh-surgery-warning-no-gloves"));
        }

        _ui.SetUiState(patient, DHSurgeryUiKey.Key, state);
    }

    #endregion
}
