using Content.Shared._DarkestHour.Vendors;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Vendors;

public sealed partial class DHGearVendorBoundUserInterface : BoundUserInterface
{
    [Dependency] private IPlayerManager _player = default!;

    private DHGearVendorWindow? _window;
    private bool _requested;

    public DHGearVendorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHGearVendorWindow>();
        if (EntMan.TryGetComponent(Owner, out DHGearVendorComponent? vendor))
        {
            _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
            _window.SetVendor(vendor);
        }

        _window.OnVend += (section, entry) => SendMessage(new DHGearVendorVendMessage(section, entry));

        // Состояние обычно уже пришло вместе с открытием; если нет — просим сервер обновить.
        if (State is DHGearVendorBuiState state)
            UpdateState(state);
        else
            SendMessage(new DHGearVendorRefreshMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not DHGearVendorBuiState vendor || _window == null)
            return;

        if (_player.LocalEntity is not { } local)
            return;

        // В состоянии — все, у кого открыто окно; берём своё.
        if (vendor.Users.TryGetValue(EntMan.GetNetEntity(local), out var mine))
            _window.UpdateState(mine);
        else if (!_requested)
        {
            _requested = true;
            SendMessage(new DHGearVendorRefreshMessage());
        }
    }
}
