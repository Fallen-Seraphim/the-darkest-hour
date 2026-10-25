using Content.Shared._DarkestHour.Recon;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Recon;

[UsedImplicitly]
public sealed class DHOrbitalScanBoundUserInterface : BoundUserInterface
{
    private DHOrbitalScanWindow? _window;

    public DHOrbitalScanBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHOrbitalScanWindow>();
        _window.OnScan += position => SendMessage(new DHOrbitalScanMessage(position));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHOrbitalScanState scan)
            _window?.UpdateState(scan);
    }
}
