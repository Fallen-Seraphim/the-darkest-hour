using System.Linq;
using Content.Shared.Preferences;

namespace Content.Client.Lobby;

// DarkestHour: «личное дело» в лобби (план осады п. 23, решения автора 2026-11-20): стрелки листают сохранённых
// персонажей, сводка о выбранном.
public sealed partial class LobbyUIController
{
    /// <summary>Выбрать соседнего сохранённого персонажа: −1 — предыдущего, +1 — следующего.</summary>
    private void DHCycleCharacter(int delta)
    {
        if (_preferencesManager.Preferences is not { } prefs || prefs.Characters.Count < 2)
            return;

        var slots = prefs.Characters.Keys.OrderBy(k => k).ToList();
        var position = slots.IndexOf(prefs.SelectedCharacterIndex);
        if (position < 0)
            position = 0;

        var next = slots[((position + delta) % slots.Count + slots.Count) % slots.Count];
        _preferencesManager.SelectCharacter(next);
        RefreshLobbyPreview();
    }

    private void DHFillPersonnelFile(HumanoidCharacterProfile? profile)
    {
        if (PreviewPanel == null)
            return;

        var prefs = _preferencesManager.Preferences;
        var slots = prefs?.Characters.Keys.OrderBy(k => k).ToList() ?? new List<int>();
        var slot = prefs?.SelectedCharacterIndex ?? 0;
        PreviewPanel.SetProfile(profile, slot, Math.Max(0, slots.IndexOf(slot)), slots.Count);
    }
}
