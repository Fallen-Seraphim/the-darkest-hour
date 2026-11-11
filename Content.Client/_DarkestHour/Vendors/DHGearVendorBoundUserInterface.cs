using Content.Shared._DarkestHour.Vendors;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Vendors;

public sealed class DHGearVendorBoundUserInterface : BoundUserInterface
{
    private DHGearVendorWindow? _window;

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

        // Лимиты у каждого бойца свои — состояние сервер шлёт лично, по запросу.
        SendMessage(new DHGearVendorRefreshMessage());
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (message is DHGearVendorStateMessage state)
            _window?.UpdateState(state);
    }
}
