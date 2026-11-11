using Content.Shared.Examine;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._DarkestHour.Characters;

/// <summary>
/// Подробное описание персонажа на самом персонаже (из профиля при появлении, решение автора 2026-11-11, как в CMU).
/// Короткая строка — в обычном осмотре, остальное — в окне «Подробное описание» (клиент).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHCharacterDescriptionComponent : Component
{
    [DataField, AutoNetworkedField]
    public DHCharacterDetails Details = new();

    /// <summary>Описание внешности из профиля (FlavorText).</summary>
    [DataField, AutoNetworkedField]
    public string Appearance = string.Empty;

    [DataField, AutoNetworkedField]
    public int Age;
}

public sealed partial class DHCharacterDescriptionSystem : EntitySystem
{
    private const string ShortExamineColor = "#C8B88A";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHCharacterDescriptionComponent, ExaminedEvent>(OnExamined);
    }

    public void Apply(EntityUid uid, Content.Shared.Preferences.HumanoidCharacterProfile profile)
    {
        var comp = EnsureComp<DHCharacterDescriptionComponent>(uid);
        comp.Details = profile.DHDetails.Clone();
        comp.Appearance = profile.FlavorText;
        comp.Age = profile.Age;
        Dirty(uid, comp);
    }

    private void OnExamined(Entity<DHCharacterDescriptionComponent> ent, ref ExaminedEvent args)
    {
        if (string.IsNullOrWhiteSpace(ent.Comp.Details.ShortExamine))
            return;

        args.PushMarkup($"[color={ShortExamineColor}]{FormattedMessage.EscapeText(ent.Comp.Details.ShortExamine)}[/color]");
    }
}
