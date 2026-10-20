using Content.Client.Overlays;
using Content.Shared._DarkestHour.Medical.Disease;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._DarkestHour.Medical.Disease;

/// <summary>
/// Значок болезни над больным, если у смотрящего медицинский HUD (тот же, что показывает значки здоровья).
/// </summary>
public sealed partial class DHShowDiseaseIconsSystem : EntitySystem
{
    [Dependency] private ShowHealthIconsSystem _healthIcons = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<StatusIconPrototype> SickIcon = "DHDiseaseIconSick";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DHDiseaseCarrierComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStatusIcons(Entity<DHDiseaseCarrierComponent> ent, ref GetStatusIconsEvent args)
    {
        if (!_healthIcons.IsActive || !ent.Comp.Symptomatic)
            return;

        if (_proto.TryIndex(SickIcon, out var icon))
            args.StatusIcons.Add(icon);
    }
}
