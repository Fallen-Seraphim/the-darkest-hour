using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Chat;

/// <summary>
/// Цвет реплик персонажа над головой — по фракции (решение автора 2026-11-11): Республика — зелёный, Империя —
/// янтарный, военная полиция — серый, корпораты LA-V — фиолетовый, ОФСК — красный. Без компонента — нейтральный.
/// Выставляет сервер при появлении бойца стороны (режимы «Штурм» и «Восстание»).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHSpeechColorComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.White;
}

public static class DHSpeechColors
{
    public static readonly Color Republic = Color.FromHex("#6FE08A");
    public static readonly Color Empire = Color.FromHex("#FFB02E");
    public static readonly Color Police = Color.FromHex("#B9C0C8");
    public static readonly Color Vanguard = Color.FromHex("#B58CFF");
    public static readonly Color Front = Color.FromHex("#FF5A4A");

    /// <summary>Нейтральный цвет (гражданские и все без стороны).</summary>
    public static readonly Color Neutral = Color.FromHex("#ECE6D6");
}
