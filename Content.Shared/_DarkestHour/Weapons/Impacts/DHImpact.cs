using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DarkestHour.Weapons.Impacts;

// Звуки попаданий по материалам и кровь на стенах (план осады п. 15, решения автора 2026-11-20). Логика — сервер,
// DHImpactSystem; слой брызг на стене — клиент, DHWallBloodVisualizerSystem.

/// <summary>Материал, по которому слышно попадание.</summary>
[Serializable, NetSerializable]
public enum DHImpactMaterial : byte
{
    Concrete,
    Metal,
    Wood,
    Glass,
    Flesh,
}

/// <summary>Явно заданный материал (если не задан — по прототипу, набору модификаторов урона или «живое — плоть»).</summary>
[RegisterComponent]
public sealed partial class DHImpactMaterialComponent : Component
{
    [DataField(required: true)]
    public DHImpactMaterial Material;
}

/// <summary>Стена, дверь или шлюз, на которых остаются брызги крови (до <see cref="Max"/>).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class DHWallBloodComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Count;

    /// <summary>Узор брызг (1–6), выбирается при первом попадании.</summary>
    [DataField, AutoNetworkedField]
    public int Pattern;

    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#800000");

    [DataField]
    public int Max = 3;
}

/// <summary>Цвет крови для эффекта брызг (клиент красит спрайт).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class DHBloodTintComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#800000");
}

[Serializable, NetSerializable]
public enum DHWallBloodLayers : byte
{
    Blood,
}

[Serializable, NetSerializable]
public sealed partial class DHWallBloodCleanDoAfterEvent : SimpleDoAfterEvent;
