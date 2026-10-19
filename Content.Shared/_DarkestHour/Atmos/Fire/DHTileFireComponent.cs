using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._DarkestHour.Atmos.Fire;

/// <summary>
/// Огонь, горящий на тайле. Параметры берутся из <see cref="DHFlammableReagentPrototype"/>.
/// Логика — на сервере (DHFireSystem), клиент только красит спрайт.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class DHTileFireComponent : Component
{
    /// <summary>Реагент, который горит (ID прототипа dhFlammableReagent).</summary>
    [DataField, AutoNetworkedField]
    public string? Reagent;

    [DataField]
    public float Intensity = 3f;

    [DataField]
    public float FireStacks = 1f;

    [DataField]
    public float StructuralDamage;

    [DataField]
    public float HotspotTemperature = 700f;

    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#ee6515");

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan SpawnedAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan EndTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextTick;

    /// <summary>Как часто огонь наносит урон и поджигает.</summary>
    [DataField]
    public TimeSpan TickTime = TimeSpan.FromSeconds(1);

    /// <summary>Первые мгновения огонь горит в полную силу (самый большой спрайт).</summary>
    [DataField]
    public TimeSpan BigFireTime = TimeSpan.FromSeconds(0.5);

    /// <summary>Сколько секунд горения гасит единица воды.</summary>
    [DataField]
    public float ExtinguishPerUnit = 2f;
}

[Serializable, NetSerializable]
public enum DHTileFireVisuals : byte
{
    /// <summary>Сила пламени 1–4 (спрайт).</summary>
    Level,

    /// <summary>Цвет пламени.</summary>
    Color,
}

[Serializable, NetSerializable]
public enum DHTileFireLayers : byte
{
    Base,
}
