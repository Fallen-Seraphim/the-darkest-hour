using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server._DarkestHour.Weapons;

/// <summary>
/// Пуля, выпущенная прицельно по сущности, хранит её в <see cref="TargetedProjectileComponent.Target"/> и шлёт клиентам.
/// Если цель удалили, пока пуля летит (сломанная баррикада, разорванное тело), сервер сыпал ошибкой
/// «Can't resolve MetaDataComponent» при отправке состояния. Снимаем компонент с таких пуль до рассылки.
/// </summary>
public sealed partial class DHTargetedProjectileCleanupSystem : EntitySystem
{
    private readonly List<EntityUid> _stale = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _stale.Clear();
        var query = EntityQueryEnumerator<TargetedProjectileComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (TerminatingOrDeleted(comp.Target))
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
        {
            RemComp<TargetedProjectileComponent>(uid);
        }
    }
}
