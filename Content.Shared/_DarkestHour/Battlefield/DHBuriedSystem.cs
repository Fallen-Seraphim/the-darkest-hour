using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Events;

namespace Content.Shared._DarkestHour.Battlefield;

/// <summary>Боец под завалом не двигается (предсказывается и на клиенте).</summary>
public sealed partial class DHBuriedSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHBuriedComponent, UpdateCanMoveEvent>(OnCanMove);
        SubscribeLocalEvent<DHBuriedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DHBuriedComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnCanMove(Entity<DHBuriedComponent> ent, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnStartup(Entity<DHBuriedComponent> ent, ref ComponentStartup args)
    {
        _blocker.UpdateCanMove(ent);
    }

    private void OnShutdown(Entity<DHBuriedComponent> ent, ref ComponentShutdown args)
    {
        _blocker.UpdateCanMove(ent);
    }
}
