using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._DarkestHour.Ranks;

/// <summary>
/// Знаки различия на надетой форме или броне (решение автора 2026-11-13; шевроны и погоны — CMU/cmss13). Ставит
/// сервер по званию владельца (RankSystem), снимаются вместе с одеждой. Владелец может их спрятать.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class DHRankInsigniaComponent : Component
{
    [DataField, AutoNetworkedField]
    public SpriteSpecifier.Rsi? Sprite;

    [DataField, AutoNetworkedField]
    public bool Hidden;
}
