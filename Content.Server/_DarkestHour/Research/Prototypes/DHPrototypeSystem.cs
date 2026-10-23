using Content.Server.Power.Components;
using Content.Server.Research.Systems;
using Content.Shared.Construction.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Interaction.Events;
using Content.Shared.Lathe;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Research.Prototypes;
using Content.Shared.Tools.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Research.Prototypes;

/// <summary>
/// Опытные образцы: пометка при печати, отказы оружия/инструментов/машин, доводка рецепта до серийного.
/// </summary>
public sealed partial class DHPrototypeSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ResearchSystem _research = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly SoundSpecifier JamSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Empty/empty.ogg");
    private static readonly SoundSpecifier SparkSound = new SoundCollectionSpecifier("sparks");
    private static readonly EntProtoId SparkEffect = "EffectSparks";
    private static readonly TimeSpan MachineCheckInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MachineOutage = TimeSpan.FromSeconds(15);

    /// <summary>Машины, отключённые сбоем опытной платы, и когда их включить.</summary>
    private readonly Dictionary<EntityUid, TimeSpan> _outages = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _outages.Clear());
        SubscribeLocalEvent<LatheComponent, DHLatheProducedEvent>(OnProduced);
        SubscribeLocalEvent<DHPrototypeItemComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DHPrototypeItemComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<DHPrototypeItemComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<DHPrototypeItemComponent, ToolUseAttemptEvent>(OnToolUseAttempt);
    }

    /// <summary>Начать пробный период для рецептов технологии (вызывается при изучении технологии игроком).</summary>
    public void StartTrial(EntityUid server, TechnologyPrototype technology)
    {
        var maturity = EnsureComp<DHRecipeMaturityComponent>(server);
        var until = _timing.CurTime + maturity.TrialPeriod;
        foreach (var recipe in technology.RecipeUnlocks)
        {
            maturity.MatureAt[recipe] = until;
        }
    }

    public bool IsTrial(EntityUid server, ProtoId<LatheRecipePrototype> recipe)
    {
        return TryComp<DHRecipeMaturityComponent>(server, out var maturity) &&
               maturity.MatureAt.TryGetValue(recipe, out var until) && _timing.CurTime < until;
    }

    /// <summary>
    /// Доводка по испытанному образцу (сканер материаловедения): если образец отработал (наработка от 3 или был отказ),
    /// рецепт сразу становится серийным. Возвращает ключ локали с итогом.
    /// </summary>
    public string Refine(EntityUid server, Entity<DHPrototypeItemComponent> item, out bool refined)
    {
        refined = false;
        if (item.Comp.Recipe is not { } recipe || !IsTrial(server, recipe))
            return "dh-prototype-report-already-serial";

        if (item.Comp.Uses < 3 && item.Comp.Failures == 0)
            return "dh-prototype-report-untested";

        Comp<DHRecipeMaturityComponent>(server).MatureAt[recipe] = _timing.CurTime;
        refined = true;
        return "dh-prototype-report-refined";
    }

    private void OnProduced(Entity<LatheComponent> ent, ref DHLatheProducedEvent args)
    {
        if (!_research.TryGetClientServer(ent, out var server, out _) || !IsTrial(server.Value, args.Recipe))
            return;

        // Отказы есть только у оружия, инструментов и плат машин — прочее (листы, патроны, одежда) печатается как обычно.
        var result = args.Result;
        if (!HasComp<GunComponent>(result) && !HasComp<ToolComponent>(result) &&
            !HasComp<MachineBoardComponent>(result) && !HasComp<ComputerBoardComponent>(result))
        {
            return;
        }

        var comp = EnsureComp<DHPrototypeItemComponent>(result);
        comp.Recipe = args.Recipe;
    }

    private void OnExamined(Entity<DHPrototypeItemComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(DHPrototypeItemComponent)))
        {
            args.PushMarkup(Loc.GetString("dh-prototype-examine", ("uses", ent.Comp.Uses), ("failures", ent.Comp.Failures)));
            if (ent.Comp.Jammed)
                args.PushMarkup(Loc.GetString("dh-prototype-examine-jammed"));
        }
    }

    private void OnShotAttempted(Entity<DHPrototypeItemComponent> ent, ref ShotAttemptedEvent args)
    {
        if (ent.Comp.Jammed)
        {
            args.Cancel();
            _popup.PopupEntity(Loc.GetString("dh-prototype-jammed"), ent, args.User);
            return;
        }

        ent.Comp.Uses++;
        if (!_random.Prob(ent.Comp.FailChance))
            return;

        ent.Comp.Jammed = true;
        ent.Comp.Failures++;
        args.Cancel();
        _audio.PlayPvs(JamSound, ent);
        _popup.PopupEntity(Loc.GetString("dh-prototype-jam"), ent, args.User, PopupType.MediumCaution);
    }

    private void OnUseInHand(Entity<DHPrototypeItemComponent> ent, ref UseInHandEvent args)
    {
        if (!ent.Comp.Jammed)
            return;

        ent.Comp.Jammed = false;
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("dh-prototype-unjam"), ent, args.User);
    }

    private void OnToolUseAttempt(Entity<DHPrototypeItemComponent> ent, ref ToolUseAttemptEvent args)
    {
        ent.Comp.Uses++;
        if (!_random.Prob(ent.Comp.FailChance))
            return;

        ent.Comp.Failures++;
        args.Cancel();
        Spark(ent);
        _popup.PopupEntity(Loc.GetString("dh-prototype-tool-fail"), ent, args.User, PopupType.MediumCaution);

        // каждый третий отказ инструмента — удар током
        if (_random.Prob(0.33f))
        {
            var damage = new DamageSpecifier { DamageDict = { ["Shock"] = 5 } };
            _damageable.TryChangeDamage(args.User, damage, origin: ent);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // вернуть питание машинам после сбоя
        if (_outages.Count > 0)
        {
            var done = new List<EntityUid>();
            foreach (var (machine, until) in _outages)
            {
                if (now < until)
                    continue;

                done.Add(machine);
                if (!TerminatingOrDeleted(machine))
                    _power.SetPowerDisabled(machine, false);
            }

            foreach (var machine in done)
            {
                _outages.Remove(machine);
            }
        }

        // опытные платы в машинах
        var query = EntityQueryEnumerator<DHPrototypeItemComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextMachineCheck)
                continue;

            comp.NextMachineCheck = now + MachineCheckInterval;

            if (!HasComp<MachineBoardComponent>(uid) && !HasComp<ComputerBoardComponent>(uid))
                continue;

            if (!_container.TryGetContainingContainer((uid, null, null), out var container) ||
                !TryComp<ApcPowerReceiverComponent>(container.Owner, out var receiver) ||
                !receiver.Powered || _outages.ContainsKey(container.Owner))
            {
                continue;
            }

            comp.Uses++;
            if (!_random.Prob(comp.FailChance))
                continue;

            comp.Failures++;
            var machine = container.Owner;
            Spark(machine);
            _popup.PopupEntity(Loc.GetString("dh-prototype-machine-fail", ("machine", machine)), machine, PopupType.MediumCaution);
            _power.SetPowerDisabled(machine, true);
            _outages[machine] = now + MachineOutage;
        }
    }

    private void Spark(EntityUid uid)
    {
        _audio.PlayPvs(SparkSound, uid);
        Spawn(SparkEffect, Transform(uid).Coordinates);
    }
}
