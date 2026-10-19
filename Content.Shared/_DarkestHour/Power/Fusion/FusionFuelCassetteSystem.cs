using Content.Shared.Examine;

namespace Content.Shared._DarkestHour.Power.Fusion;

public sealed class FusionFuelCassetteSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FusionFuelCassetteComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<FusionFuelCassetteComponent> ent, ref ExaminedEvent args)
    {
        var percent = ent.Comp.MaxFuel > 0 ? ent.Comp.Fuel / ent.Comp.MaxFuel * 100f : 0f;
        args.PushMarkup(Loc.GetString("fusion-fuel-cassette-examine", ("percent", MathF.Round(percent))));
    }

    /// <summary>
    /// Burns fuel from the cassette. Returns the fuel left.
    /// </summary>
    public float Burn(Entity<FusionFuelCassetteComponent> ent, float amount)
    {
        ent.Comp.Fuel = MathF.Max(0f, ent.Comp.Fuel - amount);
        Dirty(ent);
        return ent.Comp.Fuel;
    }
}
