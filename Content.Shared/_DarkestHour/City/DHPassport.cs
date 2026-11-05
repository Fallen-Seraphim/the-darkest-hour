using Content.Shared.Examine;
using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.City;

/// <summary>
/// Паспорт гражданина Республики (решение автора 2026-11-05): вместо жетонов у жителей Лост-Энджелса. Данные
/// (дата и место рождения, пол, раса, номер) заполняются при появлении владельца; видны при осмотре.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHPassportComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Number = string.Empty;

    [DataField, AutoNetworkedField]
    public string Holder = string.Empty;

    [DataField, AutoNetworkedField]
    public string BirthDate = string.Empty;

    [DataField, AutoNetworkedField]
    public string BirthPlace = string.Empty;

    [DataField, AutoNetworkedField]
    public string Sex = string.Empty;

    [DataField, AutoNetworkedField]
    public string Species = string.Empty;

    [DataField, AutoNetworkedField]
    public string Issued = string.Empty;
}

public sealed partial class DHPassportSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHPassportComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<DHPassportComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Number.Length == 0)
        {
            args.PushMarkup(Loc.GetString("dh-passport-blank"));
            return;
        }

        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(DHPassportComponent)))
        {
            args.PushMarkup(Loc.GetString("dh-passport-examine",
                ("number", ent.Comp.Number),
                ("holder", ent.Comp.Holder),
                ("birth", ent.Comp.BirthDate),
                ("place", ent.Comp.BirthPlace),
                ("sex", ent.Comp.Sex),
                ("species", ent.Comp.Species),
                ("issued", ent.Comp.Issued)));
        }
    }
}
