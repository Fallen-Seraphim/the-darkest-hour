using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Chat;

/// <summary>
/// Метка: сущность может использовать эмоцию «Имперский боевой клич» (Первая Империя).
/// Выдаётся через <c>dhComponents</c> отдела. Сетевая, чтобы клиентское меню эмоций видело её в белом списке.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHImperialWarcryComponent : Component;
