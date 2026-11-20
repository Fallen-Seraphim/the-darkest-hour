using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._DarkestHour.Roles;

/// <summary>Сокращение должности бойца для рации (ставится при появлении, см. <see cref="JobPrototype.DHAbbreviation"/>).</summary>
[RegisterComponent]
public sealed partial class DHJobAbbreviationComponent : Component
{
    [DataField]
    public string Abbreviation = string.Empty;
}

/// <summary>Имя говорящего в рации — уже со званием; поднимается после TransformSpeakerNameEvent.</summary>
[ByRefEvent]
public record struct DHRadioSpeakerNameEvent(string Name);

/// <summary>
/// Сокращения должностей в рации (решение автора 2026-11-19): перед именем со званием — тег должности, например
/// «[КО] Майор Кор». Только в рации; в обычной речи и при осмотре — как раньше.
/// </summary>
public sealed partial class DHJobAbbreviationSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawn);
        SubscribeLocalEvent<DHJobAbbreviationComponent, DHRadioSpeakerNameEvent>(OnRadioName);
    }

    private void OnSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null || !_proto.TryIndex<JobPrototype>(ev.JobId, out var job) || job.DHAbbreviation is not { } abbreviation)
            return;

        EnsureComp<DHJobAbbreviationComponent>(ev.Mob).Abbreviation = Loc.GetString(abbreviation);
    }

    private void OnRadioName(Entity<DHJobAbbreviationComponent> ent, ref DHRadioSpeakerNameEvent args)
    {
        // Голос под маской (не своё имя) должность не выдаёт.
        if (string.IsNullOrEmpty(ent.Comp.Abbreviation) || !args.Name.EndsWith(Name(ent)))
            return;

        args.Name = $"[{ent.Comp.Abbreviation}] {args.Name}";
    }
}
