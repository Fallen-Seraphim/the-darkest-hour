namespace Content.Shared.Roles;

public sealed partial class JobPrototype
{
    // DarkestHour: сокращение должности в рации — «[Альфа] [ОФ] Первый лейтенант Иванов» (решение автора 2026-11-19).
    /// <summary>Short job tag shown before the speaker's name on the radio (e.g. «КО»).</summary>
    [DataField("dhAbbreviation")]
    public LocId? DHAbbreviation;
}
