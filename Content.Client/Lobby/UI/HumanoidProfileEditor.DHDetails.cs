using Content.Client._DarkestHour.Characters;

namespace Content.Client.Lobby.UI;

// DarkestHour: вкладка «Описание» — подробное описание и место рождения (как в CMU, решение автора 2026-11-11).
public sealed partial class HumanoidProfileEditor
{
    private DHCharacterDetailsTab? _dhDetails;

    private void InitDHDetails()
    {
        _dhDetails = new DHCharacterDetailsTab();
        TabContainer.AddChild(_dhDetails);
        TabContainer.SetTabTitle(TabContainer.ChildCount - 1, Loc.GetString("dh-details-tab"));
        _dhDetails.OnChanged += details =>
        {
            if (Profile is null)
                return;

            Profile = Profile.WithDHDetails(details);
            SetDirty();
        };
    }

    private void UpdateDHDetails()
    {
        if (Profile != null)
            _dhDetails?.SetDetails(Profile.DHDetails, Profile.Age);
    }
}
