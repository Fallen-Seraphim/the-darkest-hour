using Robust.Shared.GameStates;

namespace Content.Shared._DarkestHour.Explosion;

// DarkestHour (решение автора 2026-11-01): взрывы как в RMC-14 — вместо ванильных огненных клеток спрайт разрыва,
// ударная волна (искажение экрана), разлёт искр-осколков, подпалина на земле и звуки CM-SS13.

/// <summary>Ударная волна взрыва: клиент искажает картинку вокруг (шейдер RMC-14 <c>RMCShockWave</c>).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DHExplosionShockWaveComponent : Component
{
    /// <summary>Как быстро волна гаснет: меньше — дольше видна.</summary>
    [DataField, AutoNetworkedField]
    public float FalloffPower = 40f;

    /// <summary>Резкость искажения.</summary>
    [DataField, AutoNetworkedField]
    public float Sharpness = 10f;

    /// <summary>Ширина волны.</summary>
    [DataField, AutoNetworkedField]
    public float Width = 0.8f;
}
