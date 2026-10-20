using Content.Shared._DarkestHour.Medical.Surgery;
using Content.Shared.Body;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._DarkestHour.Medical.Surgery;

/// <summary>
/// Нет жизненно важного органа — урон, пока его не пересадят (нет сердца — нет кровообращения, нет печени — отравление).
/// Пересчёт при вставке и извлечении органов; тикают только тела, где органа не хватает.
/// </summary>
public sealed partial class DHVitalOrgansSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IGameTiming _timing = default!;

    [Dependency] private EntityQuery<OrganComponent> _organQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHVitalOrgansComponent, OrganRemovedFromEvent>(OnOrganRemoved);
        SubscribeLocalEvent<DHVitalOrgansComponent, OrganInsertedIntoEvent>(OnOrganInserted);
    }

    private void OnOrganRemoved(Entity<DHVitalOrgansComponent> ent, ref OrganRemovedFromEvent args)
    {
        OnOrgansChanged(ent);
    }

    private void OnOrganInserted(Entity<DHVitalOrgansComponent> ent, ref OrganInsertedIntoEvent args)
    {
        OnOrgansChanged(ent);
    }

    private void OnOrgansChanged(Entity<DHVitalOrgansComponent> ent)
    {
        // До инициализации на карте не считаем (органы ещё не вставлены). Пока органы вставляются по одному, список
        // временно неполный — последняя вставка пересчитает его.
        if (LifeStage(ent) < EntityLifeStage.MapInitialized)
            return;

        Recalculate(ent);
    }

    private void Recalculate(Entity<DHVitalOrgansComponent> ent)
    {
        ent.Comp.Absent.Clear();
        foreach (var category in ent.Comp.Missing.Keys)
        {
            ent.Comp.Absent.Add(category);
        }

        if (_container.TryGetContainer(ent, BodyComponent.ContainerID, out var container))
        {
            foreach (var organ in container.ContainedEntities)
            {
                if (_organQuery.TryComp(organ, out var comp) && comp.Category is { } category)
                    ent.Comp.Absent.Remove(category);
            }
        }

        if (ent.Comp.Absent.Count > 0 && ent.Comp.NextUpdate < _timing.CurTime)
            ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.Interval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<DHVitalOrgansComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Absent.Count == 0 || now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + comp.Interval;
            if (_mobState.IsDead(uid))
                continue;

            foreach (var category in comp.Absent)
            {
                if (comp.Missing.TryGetValue(category, out var damage))
                    _damageable.TryChangeDamage(uid, damage, ignoreResistances: true, interruptsDoAfters: false);
            }
        }
    }
}
