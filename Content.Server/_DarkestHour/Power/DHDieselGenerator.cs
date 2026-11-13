using Content.Server.Power.Components;
using Content.Shared._DarkestHour.Power;
using Content.Shared.Audio;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Power;

/// <summary>
/// Резервный дизель-генератор (решение автора 2026-11-13): выдаёт ток в сеть ВН, пока заведён и есть топливо (сварочное,
/// заливается в бак из канистры или бочки). Заводят и глушат руками. На базе «Терминуса» их два — весь резерв базы.
/// </summary>
[RegisterComponent]
public sealed partial class DHDieselGeneratorComponent : Component
{
    [DataField]
    public float SupplyRate = 40000f;

    [DataField]
    public string Solution = "tank";

    [DataField]
    public string Fuel = "WeldingFuel";

    /// <summary>Расход топлива, ед./с (бак на 1000 — около 40 минут).</summary>
    [DataField]
    public float FuelPerSecond = 0.4f;

    [DataField]
    public SoundSpecifier StartSound = new SoundPathSpecifier("/Audio/Machines/generator-tug-1.ogg");

    [ViewVariables]
    public bool Running;
}

public sealed partial class DHDieselGeneratorSystem : EntitySystem
{
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private TimeSpan _next;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHDieselGeneratorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DHDieselGeneratorComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<DHDieselGeneratorComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<DHDieselGeneratorComponent> ent, ref MapInitEvent args)
    {
        SetRunning(ent, false);
    }

    private void OnActivate(Entity<DHDieselGeneratorComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        if (ent.Comp.Running)
        {
            SetRunning(ent, false);
            _popup.PopupEntity(Loc.GetString("dh-diesel-stopped"), ent, args.User);
        }
        else if (Fuel(ent) <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("dh-diesel-no-fuel"), ent, args.User);
            return;
        }
        else
        {
            SetRunning(ent, true);
            _popup.PopupEntity(Loc.GetString("dh-diesel-started"), ent, args.User);
        }

        _audio.PlayPvs(ent.Comp.StartSound, ent);
    }

    private void OnExamined(Entity<DHDieselGeneratorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.Running ? "dh-diesel-examine-running" : "dh-diesel-examine-stopped"));
        args.PushMarkup(Loc.GetString("dh-diesel-examine-fuel", ("fuel", Fuel(ent).Int())));
    }

    private FixedPoint2 Fuel(Entity<DHDieselGeneratorComponent> ent)
    {
        return _solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution)
            ? solution.GetTotalPrototypeQuantity(ent.Comp.Fuel)
            : FixedPoint2.Zero;
    }

    private void SetRunning(Entity<DHDieselGeneratorComponent> ent, bool running)
    {
        ent.Comp.Running = running;
        if (TryComp<PowerSupplierComponent>(ent, out var supplier))
            supplier.MaxSupply = running ? ent.Comp.SupplyRate : 0f;

        _ambient.SetAmbience(ent, running);
        _appearance.SetData(ent, DHDieselGeneratorVisuals.Running, running);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _next)
            return;

        _next = now + Interval;
        var query = EntityQueryEnumerator<DHDieselGeneratorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Running)
                continue;

            var need = FixedPoint2.New(comp.FuelPerSecond * (float) Interval.TotalSeconds);
            if (!_solution.TryGetSolution(uid, comp.Solution, out var soln, out _) ||
                _solution.RemoveReagent(soln.Value, comp.Fuel, need) < need)
            {
                SetRunning((uid, comp), false);
                _popup.PopupEntity(Loc.GetString("dh-diesel-out-of-fuel"), uid, PopupType.MediumCaution);
            }
        }
    }
}
