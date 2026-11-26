using Content.Shared.Humanoid;
using Content.Shared.Throwing;
using Content.Shared.Trigger.Components;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Weapons;

/// <summary>
/// Крик при броске гранаты, как в RMC-14 / cmss13 (решение автора 2026-11-26): бросил взведённую гранату — боец
/// кричит «Граната!» мужским или женским голосом. Не чаще раза в 4 с.
/// </summary>
public sealed partial class DHGrenadeShoutSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly SoundSpecifier Male = new SoundCollectionSpecifier("DHGrenadeThrowMale",
        AudioParams.Default.WithVariation(0.05f));

    private static readonly SoundSpecifier Female = new SoundCollectionSpecifier("DHGrenadeThrowFemale",
        AudioParams.Default.WithVariation(0.05f));

    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(4);

    private readonly Dictionary<EntityUid, TimeSpan> _next = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanoidProfileComponent, ThrowEvent>(OnThrow);
        SubscribeLocalEvent<HumanoidProfileComponent, ComponentShutdown>((uid, _, _) => _next.Remove(uid));
    }

    private void OnThrow(Entity<HumanoidProfileComponent> ent, ref ThrowEvent args)
    {
        if (!HasComp<ActiveTimerTriggerComponent>(args.Thrown))
            return;

        var now = _timing.CurTime;
        if (_next.TryGetValue(ent, out var next) && now < next)
            return;

        _next[ent] = now + Cooldown;
        _audio.PlayPvs(ent.Comp.Sex == Sex.Female ? Female : Male, ent);
    }
}
