using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Medical.Trauma;

// Оторванная или ампутированная часть тела (решение автора 2026-11-11): пришить можно, пока она не иссохла. Вне холода
// она сохнет (по умолчанию 10 минут); в холодильнике, морозильном шкафу или морге время не идёт.

/// <summary>Сколько ещё отделённая часть тела пригодна для пришивания.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class DHLimbDecayComponent : Component
{
    /// <summary>Сколько часть тела остаётся свежей вне холода.</summary>
    [DataField]
    public TimeSpan FreshFor = TimeSpan.FromMinutes(10);

    /// <summary>Сколько она уже пролежала вне холода, секунды.</summary>
    [DataField, AutoNetworkedField]
    public float Elapsed;

    /// <summary>Лежит в холоде (для осмотра).</summary>
    [DataField, AutoNetworkedField]
    public bool Cold;

    /// <summary>Иссохла: пришить нельзя.</summary>
    [DataField, AutoNetworkedField]
    public bool Withered;
}
