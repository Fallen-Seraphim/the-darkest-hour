using Content.Shared.Examine;
using Content.Shared.Item;
using Content.Shared.Popups;
using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.PowerLoader;

/// <summary>
/// Груз слишком тяжёл для рук (авиабомбы, ракеты, модули штурмовика; решение автора 2026-11-12): поднять, зарядить и
/// снять его можно только погрузчиком. Тащить волоком по полу — можно.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHPowerLoaderOnlyComponent : Component;

public sealed partial class DHPowerLoaderOnlySystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DHPowerLoaderOnlyComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<DHPowerLoaderOnlyComponent, ExaminedEvent>(OnExamined);
    }

    private void OnPickupAttempt(Entity<DHPowerLoaderOnlyComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        args.Cancel();
        if (args.ShowPopup)
            _popup.PopupClient(Loc.GetString("dh-power-loader-only"), ent, args.User);
    }

    private void OnExamined(Entity<DHPowerLoaderOnlyComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("dh-power-loader-only-examine"));
    }
}
