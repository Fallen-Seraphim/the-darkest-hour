using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Chat;

/// <summary>
/// Метка: сущность может использовать эмоцию «Боевой клич» (Республика и военпол).
/// Выдаётся через <c>dhComponents</c> отдела. Сетевая, чтобы клиентское меню эмоций видело её в белом списке.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DHRepublicWarcryComponent : Component;
