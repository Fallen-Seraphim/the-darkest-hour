using Content.Shared._DarkestHour.Research.Materials;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._DarkestHour.Research.Materials;

[UsedImplicitly]
public sealed class DHMaterialLabBoundUserInterface : BoundUserInterface
{
    private DHMaterialLabWindow? _window;

    public DHMaterialLabBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DHMaterialLabWindow>();
        _window.OnAttempt += (recipe, lattice, elements, electrons) =>
            SendMessage(new DHMaterialLabAttemptMessage(recipe, lattice, elements, electrons));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DHMaterialLabState lab)
            _window?.UpdateState(lab);
    }
}
